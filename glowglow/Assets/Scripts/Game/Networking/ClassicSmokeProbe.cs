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
        private bool stress;
        private bool mineStress;
        private readonly MineProjectile[] stressMines = new MineProjectile[12];
        private int stressWave, peakVisuals, baselineVisuals, frameSamples;
        private float frameTotal, maxFrame, bandwidthAt;
        private long previousBytes, peakBytesPerSecond;
        private OnlineSession Session => OnlineSession.Current;
        private void Start()
        {
            // Two real 100-HP matches take longer than the former three-hit rounds.
            deadline = Time.unscaledTime + 180;
            var args = Environment.GetCommandLineArgs();
            stress = Array.IndexOf(args, "-classic-stress") >= 0;
            mineStress = Array.IndexOf(args, "-classic-mine-stress") >= 0;
            stress |= mineStress;
            int index = Array.IndexOf(args, "-online-result");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "classic-smoke.txt");
        }
        public PlayerCommand ReadCommand(Vector2 position)
        {
            if (Session?.Match == null) return new PlayerCommand { SelectedSlot = -1 };
            var target = Session.LocalSlot == 0 ? Session.Match.PlayerTwo : Session.Match.PlayerOne;
            if (stress) return new PlayerCommand { SelectedSlot = -1, Aim = Vector2.right };
            float age = started < 0 ? 0 : Time.unscaledTime - started;
            Vector2 toward = (Vector2)target.transform.position - position;
            float distanceSquared = toward.sqrMagnitude;
            Vector2 direction = distanceSquared > .0001f ? toward.normalized : Vector2.right;
            // Random real drafts can contain melee/orbit weapons and renewable shields.
            // Approach through ordinary input so those loadouts can also finish a match.
            return new PlayerCommand { Move = age < .5f ? Vector2.up : distanceSquared > .81f ? direction : Vector2.zero,
                Aim = direction,
                AimPosition = target.transform.position, Fire = age > 1,
                FirePressed = age > 1 && Time.frameCount % 30 == 0,
                DashPressed = age < .2f,
                SelectedSlot = Session.LocalPlayer.Hand == null ? -1
                    : (int)(age / 5) % Session.LocalPlayer.Hand.Drawn.Count };
        }
        private void Update()
        {
            if (done) return;
            if (Time.unscaledTime > deadline)
            {
                var timedMatch = Session?.Match;
                Finish(false, $"timeout rounds={completed} round={Session?.Round} hp={timedMatch?.PlayerOne.Health}/{timedMatch?.PlayerTwo.Health}");
                return;
            }
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
                if (stress) { UpdateStress(match); return; }
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
        private void UpdateStress(MatchController match)
        {
            float age = Time.unscaledTime - started;
            var presentation = match.GetComponent<ClassicPresentation>();
            int visible = Session.IsHost ? presentation.SourceVisualCount : presentation.ActiveReplicaCount;
            if (age < 1.8f) baselineVisuals = visible;
            peakVisuals = Mathf.Max(peakVisuals, visible);
            if (age > 1)
            {
                frameSamples++; frameTotal += Time.unscaledDeltaTime;
                maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime);
            }
            if (Time.unscaledTime - bandwidthAt >= 1)
            {
                peakBytesPerSecond = Math.Max(peakBytesPerSecond, presentation.TotalCaptureBytes - previousBytes);
                previousBytes = presentation.TotalCaptureBytes; bandwidthAt = Time.unscaledTime;
            }
            if (Session.IsHost && stressWave < 3 && age >= 2 + stressWave * 3)
            {
                stressWave++;
                if (mineStress)
                    for (int i = (stressWave - 1) * 4; i < stressWave * 4; i++)
                        stressMines[i] = ProjectileBase.SpawnBuiltin<MineProjectile>(match.PlayerOne,
                            new Vector2(-4 + i % 6 * 1.5f, -2.5f + i / 6 * 5), Vector2.right,
                            new WeaponStats(4, 0, 20, .34f, 1.5f, new Color(1, .48f, .14f)));
                for (int i = 0; i < 350; i++)
                {
                    var position = new Vector2(-3 + (i % 35) * .17f, -2 + (i / 35) * .25f);
                    var stats = new WeaponStats(.1f, 1, 1.5f, .035f, 10, Color.HSVToRGB((i % 16) / 16f, .7f, 1));
                    ProjectileBase.SpawnBuiltin<Bullet>(match.PlayerOne, position, Vector2.up, stats);
                }
            }
            if (mineStress && Session.IsHost && age > 11)
                for (int i = 0; i < stressMines.Length; i++)
                    if (stressMines[i] != null) { stressMines[i].RemoveProjectile(); stressMines[i] = null; }
            if (age < 14) return;
            bool cleaned = visible <= baselineVisuals;
            bool pass = cleaned && peakVisuals >= baselineVisuals + 350 && (Session.IsHost || presentation.CreatedReplicaCount <= 380)
                && peakBytesPerSecond <= 70000;
            Finish(pass, $"stress=350x3 mines={(mineStress ? 12 : 0)} peakVisuals={peakVisuals} remaining={visible} baseline={baselineVisuals} "
                + $"created={presentation.CreatedReplicaCount} peakVisualBytesPerSecond={peakBytesPerSecond} "
                + $"meanFrameMs={frameTotal / Mathf.Max(1, frameSamples) * 1000:F2} maxFrameMs={maxFrame * 1000:F2}");
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

