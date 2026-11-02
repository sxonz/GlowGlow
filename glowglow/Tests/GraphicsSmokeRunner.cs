using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy only into the isolated validation project. Never included in the game build.
public sealed class GraphicsSmokeRunner : MonoBehaviour
{
    private sealed class IdleInput : IPlayerInputSource
    {
        public PlayerCommand ReadCommand(Vector2 position) => new PlayerCommand { SelectedSlot = -1, Aim = Vector2.right };
    }
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Request() { if (Application.isBatchMode) MatchController.RequestTraining(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode) new GameObject("Graphics smoke").AddComponent<GraphicsSmokeRunner>().StartCoroutine(Run());
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
                Debug.LogException(ex); File.WriteAllText("graphics-smoke-result.txt", ex.ToString());
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        Debug.Log("GRAPHICS_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        yield return new WaitForSeconds(.7f);
        var match = FindFirstObjectByType<MatchController>();
        Check(match != null && match.IsTraining && match.IsPlaying, "Real arena training entry");
        match.GetComponent<TrainingGround>().enabled = false;
        var one = match.PlayerOne; var two = match.PlayerTwo;
        one.SetInputSource(new IdleInput()); two.SetInputSource(new IdleInput());
        one.ResetCombatant(new Vector2(-6, 0)); two.ResetCombatant(new Vector2(6, 1));
        var arena = match.GetComponent<ArenaVisuals>();
        var camera = Camera.main;
        camera.GetComponent<ArenaCameraLayout>().enabled = false;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 5.35f;
        var prism = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Prism Shot.asset");
        var left = new WeaponRuntime(prism); var right = new WeaponRuntime(prism);
        foreach (var upgrade in Resources.LoadAll<BarrageUpgradeDefinition>("WeaponUpgrades"))
            if (upgrade.CanApplyTo(prism)) { left.TryUpgrade(upgrade); right.TryUpgrade(upgrade); }
        left.Fire(one, one.MuzzlePosition, Vector2.right, Vector2.zero);
        right.Fire(two, two.transform.position + Vector3.left * .55f, Vector2.left, Vector2.zero);
        yield return new WaitForSeconds(.61f);
        Capture(camera, "graphics-prism.png");
        CombatVfx.Burst(match.gameObject.scene, new Vector2(1, -1), new Color(.2f, .85f, 1), 1.5f);
        CombatVfx.Burst(match.gameObject.scene, new Vector2(-1, 1.5f), new Color(1, .2f, .6f), 1.2f);
        yield return new WaitForSeconds(.075f);
        Capture(camera, "graphics-impact.png");
        ProjectileBase.DespawnAll();
        yield return new WaitForSeconds(.5f);
        Check(arena.Effects.ActiveCount == 0, "Effects drain completely");

        // Saturation must not allocate or alter Unity's gameplay random stream.
        var randomBefore = UnityEngine.Random.state;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) arena.Effects.Emit(Vector2.zero, Color.cyan);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "Cosmetic burst allocated after warmup: " + bytes);
        Check(UnityEngine.Random.state.Equals(randomBefore), "Cosmetics altered gameplay randomness");
        Check(arena.Effects.ActiveCount <= CombatVfx.Capacity && arena.Effects.DroppedEvents > 0, "Cosmetic burst cap");
        arena.Effects.Clear();

        var samples = new float[1800]; int sampleCount = 0; float sum = 0, max = 0;
        for (int wave = 0; wave < 4; wave++)
        {
            one.ResetCombatant(new Vector2(-7, -3)); two.ResetCombatant(new Vector2(7, 3));
            for (int i = 0; i < 350; i++)
            {
                var stats = new WeaponStats(.1f, .4f, 1.2f, .04f, 10, Color.HSVToRGB((i % 16) / 16f, .7f, 1));
                ProjectileBase.SpawnBuiltin<Bullet>(one, new Vector2(-3 + i % 35 * .17f, -2 + i / 35 * .25f), Vector2.up, stats);
            }
            for (int i = 0; i < 64; i++)
            {
                var point = new Vector2(-5, -3 + i % 16 * .4f);
                var shot = ProjectileBase.SpawnBuiltin<PrismPellet>(one, point, Vector2.right,
                    new WeaponStats(1, 4, 1.2f, .045f, 10, Color.cyan));
                shot.Configure(4, point, point, Vector2.right, Time.time);
            }
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                arena.Effects.Emit(new Vector2(Mathf.Sin(Time.time) * 3, -1), Color.cyan);
                if (wave > 0 && sampleCount < samples.Length)
                {
                    float ms = Time.unscaledDeltaTime * 1000;
                    samples[sampleCount++] = ms; sum += ms; max = Mathf.Max(max, ms);
                }
                yield return null;
            }
            ProjectileBase.DespawnAll();
        }
        arena.Effects.Clear();
        Check(arena.Effects.ActiveCount == 0, "Stress cleanup");
        Array.Sort(samples, 0, sampleCount);
        string result = $"PASS: arena, prism and impact captures; VFX warm allocation={bytes} B; peak={arena.Effects.PeakCount}/{CombatVfx.Capacity}; "
            + $"350 bullets + 64 prism projectiles x 4 waves; samples={sampleCount}; meanMs={sum / Mathf.Max(1, sampleCount):F2}; "
            + $"p95Ms={samples[Mathf.Min(sampleCount - 1, Mathf.FloorToInt(sampleCount * .95f))]:F2}; maxMs={max:F2}.";
        File.WriteAllText("graphics-smoke-result.txt", result); Debug.Log(result);
    }
    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1600, 900, 24);
        var previous = RenderTexture.active;
        var oldTarget = camera.targetTexture;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = previous;
            Destroy(pixels); Destroy(target);
        }
    }
    private static void Check(bool pass, string label) { if (!pass) throw new Exception(label); }
}
