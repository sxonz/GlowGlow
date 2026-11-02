using System;
using System.IO;
using FishNet.Serializing;
using GlowGlow.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GraphicsUpgradeChecks
{
    [MenuItem("GlowGlow/Validate Graphics Upgrade")]
    public static void Run()
    {
        foreach (string name in new[] { "NeonCore", "NeonRibbon", "NeonArena", "NeonImpact", "NeonBeam", "SolarBody" })
        {
            var shader = Resources.Load<Shader>(name);
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compilation: " + name);
        }
        for (byte mode = 0; mode <= NeonVisualStyle.SunMode; mode++)
        foreach (bool clipped in new[] { false, true })
        {
            var view = new ClassicVisual { Id = 77, Sprite = 1, Scale = Vector3.one, Glow = mode,
                Color = Color.cyan, Clipped = clipped, ClipRect = new Vector4(-8.7f, -4.65f, 8.7f, 4.65f) };
            var writer = WriterPool.Retrieve();
            try
            {
                writer.WriteClassicVisual(view);
                Check(writer.Length == (clipped ? 50 : 34), "No added sprite bandwidth");
                var reader = ReaderPool.Retrieve(writer.GetArraySegment(), null);
                try
                {
                    var copy = reader.ReadClassicVisual();
                    Check(copy.Glow == mode && copy.Clipped == clipped, "All material styles round trip");
                }
                finally { reader.Store(); }
            }
            finally { writer.Store(); }
        }
        ClassicVisualChecks.Run();
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Prism Shot.asset");
            var one = new GameObject("One").AddComponent<PlayerCombatant>(); one.ConfigurePreview(weapon);
            var two = new GameObject("Two").AddComponent<PlayerCombatant>(); two.ConfigurePreview(weapon);
            var match = new GameObject("Match").AddComponent<MatchController>(); match.Configure(one, two);
            var arena = ArenaVisuals.Install(match);
            var host = match.gameObject.AddComponent<ClassicPresentation>(); host.Initialize(match, true);
            var root = new GameObject("Guest"); var guest = root.AddComponent<ClassicPresentation>(); guest.Initialize(null, false);
            var shot = ProjectileBase.SpawnBuiltin<PrismPellet>(one, Vector2.zero, Vector2.right, weapon.ResolveStats(null));
            shot.Configure(4, Vector2.zero, Vector2.zero, Vector2.right, Time.time);
            var trail = shot.GetComponent<TrailRenderer>();
            trail.AddPosition(Vector3.zero); trail.AddPosition(Vector3.right); trail.AddPosition(Vector3.up);
            int trailId = trail.GetInstanceID(); int shotId = shot.GetComponent<SpriteRenderer>().GetInstanceID();
            bool found = false;
            var frames = host.Capture(1, 1);
            foreach (var frame in frames)
            {
                guest.Apply(frame);
                foreach (var view in frame.Views)
                {
                    Check(view.Id != trailId, "Prism trail geometry must never be transmitted");
                    if (view.Id == shotId) { found = true; Check(view.Glow == NeonVisualStyle.PrismMode, "Host prism style"); }
                }
            }
            Check(found, "Authoritative projectile remains in capture");
            bool localTrail = false;
            foreach (var item in root.GetComponentsInChildren<TrailRenderer>()) localTrail |= item.enabled;
            Check(localTrail, "Guest derives its own ribbon");
            int bytes = host.LastCaptureBytes;
            arena.Effects.Emit(Vector2.zero, Color.cyan);
            host.Capture(1, 2);
            Check(host.LastCaptureBytes <= bytes, "Cosmetic bursts add no network records");
            guest.ClearReplicas();
            foreach (var item in root.GetComponentsInChildren<TrailRenderer>()) Check(!item.enabled && item.positionCount == 0, "Reset clears trails");
            Check(root.GetComponentsInChildren<TrailRenderer>(true).Length == ClassicPresentation.MaxLocalTrails, "Guest trail capacity");
            ProjectileBase.DespawnAll();
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }
        CombatUpgradeChecks.Run();
        Debug.Log("GRAPHICS_UPGRADE_CHECKS_PASSED: shaders, all style modes, unchanged wire sizes, local ribbons, cosmetic isolation, bounded pools, combat regression.");
    }
    public static void RunBatch()
    {
        try
        {
            var initial = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Temp");
            EditorSceneManager.SaveScene(initial, "Temp/GraphicsValidationHost.unity");
            Run(); EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    private static void Check(bool pass, string label) { if (!pass) throw new Exception(label); }
}
