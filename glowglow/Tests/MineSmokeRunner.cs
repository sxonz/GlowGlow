using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using GlowGlow.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into Assets only in the disposable validation project; excluded from game builds.
public sealed class MineSmokeRunner : MonoBehaviour
{
    private static readonly StringBuilder Results = new();
    private static WeaponDefinition definition;
    private static PlayerCombatant one, two;
    private sealed class Idle : IPlayerInputSource
    {
        public PlayerCommand ReadCommand(Vector2 p) => new() { SelectedSlot = -1, Aim = Vector2.right };
    }
    public static void Begin()
    {
        Type.GetType("MineAuthoring, Assembly-CSharp-Editor").GetMethod("BakeIcon").Invoke(null, null);
        EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Request() { if (Application.isBatchMode) MatchController.RequestTraining(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode) new GameObject("Mine validation").AddComponent<MineSmokeRunner>().StartCoroutine(Run());
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
                Debug.LogException(ex);
                File.WriteAllText("mine-smoke-result.txt", Results + "\nFAIL: " + ex);
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText("mine-smoke-result.txt", "PASS\n" + Results);
        Debug.Log("MINE_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private static MineProjectile Mine(Vector2 position) => ProjectileBase.SpawnBuiltin<MineProjectile>(
        one, position, Vector2.right, definition.ResolveStats(null));
    private static BulletProjectile Hit(PlayerCombatant owner, Vector2 position) => ProjectileBase.SpawnBuiltin<BulletProjectile>(
        owner, position, Vector2.right, new WeaponStats(1, 0, 2, .13f, 10, Color.cyan));
    private static ElectricPulseProjectile Pulse(PlayerCombatant owner, Vector2 position, bool pull = false)
    {
        var pulse = ProjectileBase.SpawnBuiltin<ElectricPulseProjectile>(owner, position, Vector2.right,
            new WeaponStats(2, 0, .4f, .13f, 3, Color.cyan), pull ? WeaponEffects.PulsePull : WeaponEffects.None);
        pulse.Configure(new BarrageStep { shape = BarrageShape.ElectricPulse, duration = .4f, dealsDamage = false });
        return pulse;
    }
    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        yield return new WaitForSeconds(.7f);
        var match = FindFirstObjectByType<MatchController>();
        Check(match != null && match.IsTraining && match.IsPlaying, "Real training arena loaded");
        match.GetComponent<TrainingGround>().enabled = false;
        one = match.PlayerOne; two = match.PlayerTwo;
        one.SetInputSource(new Idle()); two.SetInputSource(new Idle());
        one.ResetCombatant(new Vector2(-6, -3)); two.ResetCombatant(new Vector2(6, 3));
        definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Mine.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(definition != null && definition.steps[0].shape == BarrageShape.Mine, "Mine definition loaded");
        Check(catalog.cards.Any(c => c.id == "mine" && c.weapon == definition), "Mine available in catalog/training");
        var ids = catalog.StarterCardIds(); ids[0] = "mine";
        Check(catalog.IsDeckValid("{\"cards\":[" + string.Join(",", ids.Select(s => "\"" + s + "\"")) + "]}"), "Mine accepted in eight-card online deck");
        yield return new WaitForSeconds(.7f);

        var runtime = new WeaponRuntime(definition);
        runtime.Fire(one, one.MuzzlePosition, Vector2.right, Vector2.zero);
        var mine = ProjectileBase.ActiveProjectiles.OfType<MineProjectile>().Single();
        Check(Vector2.Distance(mine.transform.position, one.transform.position) < .001f, "Placed at owner's feet");
        Check(mine.Health == 3 && mine.RemainingLifetime > 19.9f, "Health three and twenty-second fuse");
        Check(mine.GetComponentsInChildren<SpriteRenderer>().Length == 3, "Only body, core and derived glow; no health indicators");
        yield return new WaitForSeconds(.12f);
        Check(mine.IsSpawned, "Owner cannot step-trigger own mine");
        var friendly = Hit(one, mine.transform.position);
        yield return new WaitForSeconds(.1f);
        Check(mine.Health == 3 && friendly.IsSpawned, "Friendly shot passes without mine damage");
        friendly.RemoveProjectile();
        one.transform.position = new Vector2(-3, -3);

        var beam = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(two, mine.transform.position, Vector2.right,
            new WeaponStats(1, 0, 2, .6f, 2, Color.cyan));
        beam.Configure(new BarrageStep { shape = BarrageShape.Circle, position = BarragePosition.Cursor,
            duration = 2, dimensions = Vector2.one, dealsDamage = true });
        yield return new WaitForSeconds(.1f);
        Check(mine.Health == 2, "Persistent attack damages once");
        yield return new WaitForSeconds(.35f);
        Check(mine.Health == 2, "TriggerStay does not count as extra hits");
        beam.RemoveProjectile();
        var second = Hit(two, mine.transform.position);
        yield return new WaitForSeconds(.08f);
        Check(mine.Health == 1 && !second.IsSpawned, "Second hit consumes bullet and leaves one health");
        var third = Hit(two, mine.transform.position);
        yield return new WaitForSeconds(.08f);
        Check(!mine.IsSpawned && !third.IsSpawned, "Third hit detonates mine");
        Check(ProjectileBase.ActiveProjectiles.OfType<BarrageShapeProjectile>().Count() == 1, "Exactly one explosion");
        ProjectileBase.DespawnAll();

        mine = Mine(new Vector2(1, 0));
        Check(mine.Health == 3 && mine.RemainingLifetime > 19.9f, "Pool reuse resets health and fuse");
        Pulse(one, Vector2.zero);
        yield return new WaitForSeconds(.85f);
        float pushed = mine.transform.position.x;
        Check(mine.IsSpawned && mine.Health == 3 && pushed > 3.2f && pushed < 3.42f,
            "Own pulse pushes once, preserves health, travel=" + pushed.ToString("F3"));
        ProjectileBase.DespawnAll();
        mine = Mine(new Vector2(1, 0));
        Pulse(two, Vector2.zero, true);
        yield return new WaitForSeconds(.85f);
        Check(mine.IsSpawned && mine.Health == 3 && Mathf.Abs(mine.transform.position.x) < .05f, "Enemy pull pulse pulls without overshooting/damaging");
        ProjectileBase.DespawnAll();
        var arena = match.GetComponent<ArenaVisuals>();
        mine = Mine(new Vector2(arena.Bounds.xMax - .4f, 0));
        Pulse(two, new Vector2(arena.Bounds.xMax - 2, 0));
        yield return new WaitForSeconds(.5f);
        Check(mine.IsSpawned && mine.transform.position.x <= arena.Bounds.xMax - definition.projectileRadius + .001f, "Pushed mine remains inside arena");
        ProjectileBase.DespawnAll();

        mine = Mine(Vector2.zero);
        int hits = two.HitsTaken;
        two.transform.position = Vector2.zero;
        yield return new WaitForSeconds(.12f);
        Check(!mine.IsSpawned && two.HitsTaken == hits + 1, "Enemy step triggers explosion and damage");
        two.transform.position = new Vector2(6, 3);
        ProjectileBase.DespawnAll();
        yield return new WaitForSeconds(.6f);
        mine = Mine(new Vector2(1, 0));
        two.transform.position = new Vector2(2, 0);
        hits = two.HitsTaken;
        Pulse(one, Vector2.zero);
        yield return new WaitForSeconds(.25f);
        Check(!mine.IsSpawned && two.HitsTaken == hits + 1, "Pushed mine sweep detects enemy and explosion hits");
        two.transform.position = new Vector2(6, 3);
        ProjectileBase.DespawnAll();

        mine = Mine(Vector2.zero);
        // Real scaled time, not an artificial call to the expiry handler.
        float fired = Time.time;
        while (Time.time - fired < 19.8f) yield return null;
        Check(mine.IsSpawned, "Mine survives until twenty seconds");
        while (Time.time - fired < 20.08f) yield return null;
        Check(!mine.IsSpawned && ProjectileBase.ActiveProjectiles.OfType<BarrageShapeProjectile>().Any(), "Twenty-second fuse detonates rather than silently disappearing");
        ProjectileBase.DespawnAll();

        var host = match.gameObject.AddComponent<ClassicPresentation>(); host.Initialize(match, true);
        var guestRoot = new GameObject("Mine guest visuals");
        var guest = guestRoot.AddComponent<ClassicPresentation>(); guest.Initialize(null, false);
        foreach (var frame in host.Capture(1, 1)) guest.Apply(frame);
        int baseline = host.SourceVisualCount;
        mine = Mine(Vector2.zero);
        int bodyId = mine.GetComponent<SpriteRenderer>().GetInstanceID();
        foreach (var frame in host.Capture(1, 2)) guest.Apply(frame);
        Check(host.SourceVisualCount == baseline + 2 && guest.ContainsReplica(bodyId), "Mine uses two existing compact sprite records on guest");
        Check(host.LastCaptureBytes <= 100, "Stationary mine initial incremental wire budget: " + host.LastCaptureBytes);
        foreach (var frame in host.Capture(1, 3)) guest.Apply(frame);
        Check(host.LastCaptureBytes == 0, "Unchanged mine adds zero immediate delta bytes");
        mine.RemoveProjectile();
        foreach (var frame in host.Capture(1, 4)) guest.Apply(frame);
        foreach (var removal in host.Removals) guest.ApplyRemoval(removal);
        Check(!guest.ContainsReplica(bodyId) && guest.ActiveReplicaCount == baseline, "Guest removes despawned mine completely");
        Destroy(guestRoot); Destroy(host);

        one.transform.position = new Vector2(-6, -3);
        for (int i = 0; i < 12; i++) Mine(new Vector2(-4 + i % 6 * 1.5f, -1 + i / 6 * 2));
        var camera = Camera.main;
        camera.GetComponent<ArenaCameraLayout>().enabled = false;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 5.35f;
        yield return new WaitForSeconds(.1f);
        Capture(camera);
        for (int i = 0; i < 350; i++) ProjectileBase.SpawnBuiltin<Bullet>(one,
            new Vector2(-3 + i % 35 * .17f, -2 + i / 35 * .25f), Vector2.up,
            new WeaponStats(.1f, .4f, 4, .04f, 10, Color.cyan));
        int samples = 0; float total = 0, maximum = 0;
        float until = Time.time + 3;
        while (Time.time < until)
        {
            yield return null; samples++; total += Time.unscaledDeltaTime;
            maximum = Mathf.Max(maximum, Time.unscaledDeltaTime);
        }
        Results.AppendLine($"LOAD: 12 mines + 350 bullets; mean={1000 * total / samples:F2}ms max={1000 * maximum:F2}ms samples={samples}");
        ProjectileBase.DespawnAll();
        Check(!ProjectileBase.ActiveProjectiles.Any(), "Round cleanup removes all mines and attacks");
    }
    private static void Capture(Camera camera)
    {
        var previous = RenderTexture.active; var target = camera.targetTexture;
        var render = new RenderTexture(1280, 720, 24); render.Create(); camera.targetTexture = render;
        camera.Render(); RenderTexture.active = render;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        File.WriteAllBytes("mine-arena.png", pixels.EncodeToPNG());
        camera.targetTexture = target; RenderTexture.active = previous;
        render.Release(); Destroy(render); Destroy(pixels);
    }
    private static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException(label);
        Results.AppendLine("PASS: " + label);
    }
}
