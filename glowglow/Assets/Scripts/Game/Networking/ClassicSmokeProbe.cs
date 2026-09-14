using System;
using System.IO;
using UnityEngine;

namespace GlowGlow.Online
{
    // Development-only two-process exercise of the real Classic scene and weapon runtimes.
    public sealed class ClassicSmokeProbe : MonoBehaviour, IPlayerInputSource
    {
        private float deadline, started = -1, lastCapture;
        private int trackedRound;
        private bool done, requested;
        private Vector2 initial;
        private float moved;
        private bool sawHits, sawCooldown, sawUpgrade;
        private int completed;
        private string output;
        private OnlineSession Session => OnlineSession.Current;
        private void Start()
        {
            deadline = Time.unscaledTime + 90;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-online-result");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "classic-smoke.txt");
        }
        public PlayerCommand ReadCommand(Vector2 position)
        {
            if (Session?.Match == null) return new PlayerCommand { SelectedSlot = -1 };
            var target = Session.LocalSlot == 0 ? Session.Match.PlayerTwo : Session.Match.PlayerOne;
            float age = started < 0 ? 0 : Time.unscaledTime - started;
            return new PlayerCommand { Move = age < .5f ? Vector2.up : Vector2.zero,
                Aim = ((Vector2)target.transform.position - position).normalized,
                AimPosition = target.transform.position, Fire = age > 1,
                FirePressed = age > 1 && Time.frameCount % 30 == 0,
                DashPressed = age < .2f,
                SelectedSlot = Session.LocalPlayer.Hand == null ? -1
                    : (int)(age / 5) % Session.LocalPlayer.Hand.Drawn.Count };
        }
        private void Update()
        {
            if (done) return;
            if (Time.unscaledTime > deadline) { Finish(false, "timeout"); return; }
            if (Session == null || Session.Interrupted) { Finish(false, Session?.Status ?? "no session"); return; }
            var match = Session.Match;
            if (match == null) return;
            if (match.IsPlaying)
            {
                if (trackedRound != Session.Round)
                {
                    trackedRound = Session.Round; started = Time.unscaledTime; initial = Session.LocalPlayer.transform.position;
                    requested = false;
                }
                moved = Mathf.Max(moved, Vector2.Distance(initial, Session.LocalPlayer.transform.position));
                sawHits |= match.PlayerOne.HitsTaken > 0 || match.PlayerTwo.HitsTaken > 0;
                foreach (var player in new[] { match.PlayerOne, match.PlayerTwo })
                {
                    if (player.Hand == null) { Finish(false, "missing Classic hand"); return; }
                    foreach (var weapon in player.Hand.Drawn)
                    {
                        sawCooldown |= weapon.CooldownRemaining > .1f;
                        sawUpgrade |= weapon.Upgrades.Count > 0;
                    }
                }
                if (Time.unscaledTime - started > 3 && lastCapture < started)
                {
                    lastCapture = Time.unscaledTime;
                    Capture(output + "-round" + trackedRound + ".png");
                }
            }
            else if (!match.IsDrafting && started >= 0 && !requested)
            {
                requested = true;
                completed++;
                Debug.Log($"CLASSIC_ROUND_FINISHED round={trackedRound} hits={match.PlayerOne.HitsTaken}/{match.PlayerTwo.HitsTaken}");
                if (completed >= 2)
                {
                    bool pass = moved > .4f && sawHits && sawCooldown && sawUpgrade
                        && (Session.IsHost || Session.ReceivedVisualFrames > 10);
                    Finish(pass, $"rounds={completed} movement={moved:F2} hits={sawHits} cooldown={sawCooldown} upgrade={sawUpgrade} visuals={Session.ReceivedVisualFrames}");
                }
                else Session.RequestRematch();
            }
        }
        private void Finish(bool pass, string details)
        {
            done = true;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            int winner = Session?.Match?.Winner == null ? -1 : Session.Match.Winner.PlayerIndex - 1;
            string result = (pass ? "PASS " : "FAIL ") + $"winnerSlot={winner} " + details;
            File.WriteAllText(output, result);
            Debug.Log("CLASSIC_SMOKE " + result);
            // Both peers need time to consume the final snapshot before transport shutdown.
            Invoke(nameof(Quit), 3);
        }
        private void Quit() => Application.Quit();
        private static void Capture(string path)
        {
            ScreenCapture.CaptureScreenshot(path + ".screen.png");
            var main = Camera.main;
            if (main == null) return;
            var camera = new GameObject("Classic Capture", typeof(Camera)).GetComponent<Camera>();
            camera.CopyFrom(main); camera.enabled = false;
            camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                for (int i = 0; i < canvases.Length; i++)
                {
                    modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera; distances[i] = canvases[i].planeDistance;
                    if (modes[i] == RenderMode.ScreenSpaceOverlay)
                    { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = 1; }
                }
                foreach (var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                foreach (var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
                RenderTexture.active = old; camera.targetTexture = null;
                texture.Release(); Destroy(texture); Destroy(pixels); Destroy(camera.gameObject);
            }
        }
    }
}

