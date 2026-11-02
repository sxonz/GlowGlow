using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FishNet.Serializing;
using GlowGlow.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Copy only into an isolated project's Assets for play-mode validation.
public sealed class GraphicsThemeSmokeRunner : MonoBehaviour
{
    private static readonly StringBuilder Results = new();
    private static bool saved;
    private static int previous;
    private sealed class Idle : IPlayerInputSource
    {
        public PlayerCommand ReadCommand(Vector2 p) => new() { SelectedSlot = -1, Aim = Vector2.right };
    }
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isBatchMode) return;
        saved = PlayerPrefs.HasKey(GraphicsTheme.SaveKey); previous = PlayerPrefs.GetInt(GraphicsTheme.SaveKey, 1);
        var root = new GameObject("Theme smoke runner"); DontDestroyOnLoad(root);
        root.AddComponent<GraphicsThemeSmokeRunner>().StartCoroutine(Run());
    }
    private static void Restore()
    {
        Time.timeScale = 1;
        if (saved) PlayerPrefs.SetInt(GraphicsTheme.SaveKey, previous); else PlayerPrefs.DeleteKey(GraphicsTheme.SaveKey);
        PlayerPrefs.Save(); GraphicsTheme.Load();
    }
    private static IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception ex)
            {
                Debug.LogException(ex); Restore();
                File.WriteAllText("theme-smoke-result.txt", Results + "\nFAIL: " + ex);
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        Restore(); File.WriteAllText("theme-smoke-result.txt", "PASS\n" + Results);
        Debug.Log("THEME_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private static IEnumerator Verify()
    {
        yield return new WaitForSeconds(.5f);
        var title = FindFirstObjectByType<TitleScreenController>();
        title.OpenSettings();
        yield return new WaitForSeconds(.3f);
        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        var classic = buttons.Single(b => b.name == "Classic Theme");
        var neon = buttons.Single(b => b.name == "Neon Theme");
        classic.onClick.Invoke();
        Check(GraphicsTheme.IsClassic && PlayerPrefs.GetInt(GraphicsTheme.SaveKey, -1) == 0, "Settings Classic click applies and saves");
        GraphicsTheme.Load(); Check(GraphicsTheme.IsClassic, "Saved preference reloads");
        neon.onClick.Invoke();
        Check(!GraphicsTheme.IsClassic && PlayerPrefs.GetInt(GraphicsTheme.SaveKey, -1) == 1, "Settings Neon click applies and saves");
        Check(classic.navigation.selectOnRight == neon && neon.navigation.selectOnLeft == classic, "Theme buttons have keyboard navigation");
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        ((RectTransform)classic.transform).GetWorldCorners(corners);
        Check(corners[2].x > corners[0].x + 40 && corners[2].y > corners[0].y + 20, "Theme controls have usable bounds");
        CaptureUI("theme-settings.png");
        yield return new WaitForSeconds(.4f);

        MatchController.RequestTraining();
        yield return SceneManager.LoadSceneAsync("Arena");
        yield return new WaitForSeconds(.6f);
        Check(!GraphicsTheme.IsClassic, "Preference survives scene transition");
        var match = FindFirstObjectByType<MatchController>();
        match.GetComponent<TrainingGround>().enabled = false;
        var one = match.PlayerOne; var two = match.PlayerTwo;
        one.SetInputSource(new Idle()); two.SetInputSource(new Idle());
        one.ResetCombatant(new Vector2(-5, 0)); two.ResetCombatant(new Vector2(5, 0));
        ProjectileBase.DespawnAll();
        var stats = new WeaponStats(2, 5, 20, .18f, 4, Color.cyan);
        var bullet = ProjectileBase.SpawnBuiltin<Bullet>(one, new Vector2(-2, 1), Vector2.right, stats);
        var blast = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(one, new Vector2(0, -1), Vector2.right,
            new WeaponStats(2, 1, 20, 1, 4, Color.magenta));
        blast.Configure(new BarrageStep { shape = BarrageShape.Circle, position = BarragePosition.Player,
            duration = 20, dealsDamage = true, dimensions = Vector2.one });
        var mine = ProjectileBase.SpawnBuiltin<MineProjectile>(one, new Vector2(2, 1), Vector2.right,
            new WeaponStats(4, 1, 20, .34f, 1.5f, new Color(1, .48f, .14f)));
        var camera = Camera.main;
        camera.GetComponent<ArenaCameraLayout>().enabled = false;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 5.35f;
        yield return null;
        Time.timeScale = 0;
        Physics2D.SyncTransforms();
        var colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        var bounds = colliders.Select(c => c.bounds).ToArray();
        var offsets = colliders.Select(c => c.offset).ToArray();
        var enabledStates = colliders.Select(c => c.enabled).ToArray();
        var scales = colliders.Select(c => c.transform.lossyScale).ToArray();
        var random = UnityEngine.Random.state;
        int health = mine.Health, firstHits = one.HitsTaken, secondHits = two.HitsTaken;
        var arena = match.GetComponent<ArenaVisuals>();
        var floor = match.transform.Find("Neon Arena Floor").GetComponent<SpriteRenderer>();
        var host = match.gameObject.AddComponent<ClassicPresentation>(); host.Initialize(match, true);
        var views = host.Capture(1, 1).SelectMany(f => f.Views).OrderBy(v => v.Id).ToArray();
        byte[] originalWire = Encode(views);
        var neonPixels = Capture(camera, "theme-neon.png");
        GraphicsTheme.Select(GraphicsThemeKind.Classic);
        Check(!floor.enabled, "Classic restores original arena renderers");
        Check(FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name == "Arena Border" && l.enabled), "Classic arena border visible");
        var classicPixels = Capture(camera, "theme-classic.png");
        int differences = 0;
        for (int i = 0; i < neonPixels.Length; i++) if (!neonPixels[i].Equals(classicPixels[i])) differences++;
        Check(differences > 20000, "Themes visibly differ at identical positions: " + differences + " pixels");
        for (int pass = 0; pass < 8; pass++)
        {
            GraphicsTheme.Select(pass % 2 == 0 ? GraphicsThemeKind.Neon : GraphicsThemeKind.Classic);
            Physics2D.SyncTransforms();
            for (int i = 0; i < colliders.Length; i++)
                Check(colliders[i] != null && colliders[i].bounds == bounds[i] && colliders[i].offset == offsets[i]
                    && colliders[i].enabled == enabledStates[i] && colliders[i].transform.lossyScale == scales[i],
                    "Collider unchanged " + colliders[i].name + " pass " + pass);
        }
        Check(UnityEngine.Random.state.Equals(random), "Theme changes do not consume gameplay random values");
        Check(bullet.Stats.Equals(stats) && mine.Health == health && one.HitsTaken == firstHits && two.HitsTaken == secondHits,
            "Speed, radius, lifetime, cooldown, mine health and player hits unchanged");
        Check(arena.Effects.ActiveCount == 0, "Classic has no new cosmetic particles");
        var otherHost = match.gameObject.AddComponent<ClassicPresentation>(); otherHost.Initialize(match, true);
        var classicViews = otherHost.Capture(1, 1).SelectMany(f => f.Views).OrderBy(v => v.Id).ToArray();
        if (!originalWire.SequenceEqual(Encode(classicViews)))
        {
            Results.AppendLine("Neon views: " + views.Length + ", Classic views: " + classicViews.Length);
            for (int i = 0; i < Mathf.Min(views.Length, classicViews.Length); i++)
                if (!Encode(new[] { views[i] }).SequenceEqual(Encode(new[] { classicViews[i] })))
                    Results.AppendLine(JsonUtility.ToJson(views[i]) + "\n" + JsonUtility.ToJson(classicViews[i]));
        }
        Check(originalWire.SequenceEqual(Encode(classicViews)), "Exact same wire bytes in both themes");
        var guestRoot = new GameObject("Classic guest of Neon host");
        var guest = guestRoot.AddComponent<ClassicPresentation>(); guest.Initialize(null, false);
        guest.Apply(new ClassicVisualFrame { Round = 1, Sequence = 1, Views = views });
        Check(guest.ContainsReplica(bullet.GetComponent<SpriteRenderer>().GetInstanceID()), "Classic guest accepts Neon host snapshot");
        GraphicsTheme.Select(GraphicsThemeKind.Neon);
        guest.Apply(new ClassicVisualFrame { Round = 1, Sequence = 2, Views = classicViews });
        Check(guest.ContainsReplica(mine.GetComponent<SpriteRenderer>().GetInstanceID()), "Neon guest accepts Classic host snapshot");
        Check(floor.enabled, "Neon arena returns after switching back");
        Destroy(guestRoot); Destroy(host); Destroy(otherHost);
        ProjectileBase.DespawnAll();
        Time.timeScale = 1; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        yield return null;
        for (int i = 0; i < 350; i++) ProjectileBase.SpawnBuiltin<Bullet>(one,
            new Vector2(-3 + i % 35 * .17f, -2 + i / 35 * .25f), Vector2.up,
            new WeaponStats(1, 1, 15, .04f, 10, Color.cyan));
        for (int i = 0; i < 12; i++) ProjectileBase.SpawnBuiltin<MineProjectile>(one,
            new Vector2(-4 + i % 6 * 1.5f, -2.5f + i / 6 * 5), Vector2.right,
            new WeaponStats(4, 1, 20, .34f, 1.5f, Color.yellow));
        foreach (var theme in new[] { GraphicsThemeKind.Classic, GraphicsThemeKind.Neon })
        {
            GraphicsTheme.Select(theme);
            yield return new WaitForSeconds(.35f);
            var samples = new float[2000]; int count = 0; float sum = 0, max = 0;
            float until = Time.unscaledTime + 2;
            while (Time.unscaledTime < until)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000;
                if (count < samples.Length) samples[count++] = ms;
                sum += ms; max = Mathf.Max(max, ms);
            }
            Array.Sort(samples, 0, count);
            Results.AppendLine($"LOAD {theme}: 350 bullets + 12 mines; mean={sum / count:F2}ms p95={samples[(int)(count * .95f)]:F2}ms max={max:F2}ms");
        }
        ProjectileBase.DespawnAll();
    }
    private static void CaptureUI(string path)
    {
        var camera = Camera.main;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var changed = new List<Canvas>();
        foreach (var canvas in canvases)
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                changed.Add(canvas); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
            }
        Canvas.ForceUpdateCanvases(); Capture(camera, path);
        foreach (var canvas in changed) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
    }
    private static byte[] Encode(ClassicVisual[] views)
    {
        var writer = WriterPool.Retrieve();
        try { foreach (var view in views) writer.WriteClassicVisual(view); return writer.GetArraySegment().ToArray(); }
        finally { writer.Store(); }
    }
    private static Color32[] Capture(Camera camera, string path)
    {
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var render = new RenderTexture(1280, 720, 24); render.Create();
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        var pixels = image.GetPixels32(); File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = oldTarget; RenderTexture.active = previous;
        render.Release(); Destroy(render); Destroy(image); return pixels;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Results.AppendLine("PASS: " + label);
    }
}
