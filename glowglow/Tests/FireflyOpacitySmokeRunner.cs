// Copy into Assets only in the isolated validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Serializing;
using GlowGlow.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FireflyOpacitySmokeRunner : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Results = new();
    private static bool hadPreference;
    private static float savedOpacity;
    private static GraphicsThemeKind savedTheme;
    private static int checks;
    private static Camera cameraView;
    private static RenderTexture render;
    private static Texture2D readback;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isBatchMode || !Environment.CommandLine.Contains("FireflyOpacitySmokeRunner.Begin")) return;
        hadPreference = PlayerPrefs.HasKey(BarrageVisibility.SaveKey);
        savedOpacity = PlayerPrefs.GetFloat(BarrageVisibility.SaveKey, 1);
        savedTheme = GraphicsTheme.Current;
        new GameObject("Firefly opacity validation").AddComponent<FireflyOpacitySmokeRunner>().StartCoroutine(Run());
    }
    private static void Restore()
    {
        Time.timeScale = 1;
        if (hadPreference) PlayerPrefs.SetFloat(BarrageVisibility.SaveKey, savedOpacity);
        else PlayerPrefs.DeleteKey(BarrageVisibility.SaveKey);
        PlayerPrefs.Save(); BarrageVisibility.Load(); BarrageVisibility.SetLocalOwner(1);
        GraphicsTheme.Select(savedTheme);
    }
    private static IEnumerator Run()
    {
        Directory.CreateDirectory("ValidationOutput");
        var test = Verify();
        while (true)
        {
            object next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception ex)
            {
                Restore(); Debug.LogException(ex);
                File.WriteAllText("ValidationOutput/firefly-opacity-result.txt", string.Join("\n", Results) + "\nFAIL: " + ex);
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        Restore(); Results.Add("PASS: " + checks + " checks.");
        File.WriteAllText("ValidationOutput/firefly-opacity-result.txt", string.Join("\n", Results));
        Debug.Log("FIREFLY_OPACITY_PASSED: " + checks); EditorApplication.Exit(0);
    }
    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        yield return null;
        var title = FindFirstObjectByType<TitleScreenController>();
        title.OpenSettings(); yield return new WaitForSeconds(.3f);
        var slider = FindObjectsByType<Slider>(FindObjectsSortMode.None).Single(s => s.name == "Barrage Opacity");
        float volume = AudioListener.volume;
        slider.value = .25f;
        Check(Mathf.Approximately(BarrageVisibility.Opacity, .25f) && AudioListener.volume == volume, "Opacity slider changes only barrage visibility");
        Check(PlayerPrefs.GetFloat(BarrageVisibility.SaveKey) == .25f, "Opacity preference is stored");
        BarrageVisibility.Load(); Check(BarrageVisibility.Opacity == .25f, "Saved opacity reloads");
        Check(slider.navigation.selectOnUp is Slider && slider.navigation.selectOnDown is Button, "Keyboard navigation includes opacity slider");
        Canvas.ForceUpdateCanvases();
        Check(((RectTransform)slider.transform).rect.height >= 30, "Opacity slider has a usable pointer target");
        CaptureSettings();
        title.CloseSettings(); yield return new WaitForSeconds(.3f);
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);

        cameraView = Camera.main;
        cameraView.orthographic = true; cameraView.orthographicSize = 4.5f;
        cameraView.transform.position = new Vector3(0, 0, -10);
        cameraView.backgroundColor = Color.black; cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.cullingMask = 1 << 30;
        cameraView.aspect = 16f / 9;
        render = new RenderTexture(256, 144, 24); render.Create();
        readback = new Texture2D(256, 144, TextureFormat.RGB24, false);
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Firefly Convergence.asset");
        var owner = Player("Caster", weapon, 1, new Vector2(0, 0));
        var enemy = Player("Opponent", weapon, 2, new Vector2(30, 30));
        yield return new WaitForSeconds(.65f);
        var stats = new WeaponStats(1, 4.2f, 8, .14f, 11, Color.green);
        FireflyLight.Cast(owner, new Vector2(30, 30), stats);
        var lights = ProjectileBase.ActiveProjectiles.OfType<FireflyLight>().ToArray();
        Check(lights.Length == 9 && lights.All(l => !CelestialBarrageGeometry.ViewBounds(owner).Contains(l.transform.position)), "Nine fireflies spawn outside the view");
        Check(lights.All(l => Vector2.Dot(l.LinearVelocity, (Vector2)owner.transform.position - (Vector2)l.transform.position) > 0), "Initial guidance points toward caster, ignoring enemy aim");
        var tick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), lights[0], typeof(FireflyLight).GetMethod("Tick", Private));
        tick(.00001f);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) tick(.00001f);
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated == 0, "Warmed homing movement allocates zero bytes");
        yield return new WaitForSeconds(1);
        owner.transform.position = new Vector2(3, 1.5f);
        float deadline = Time.time + 5.5f;
        var absorbed = new HashSet<int>();
        while (lights.Any(l => l.IsSpawned) && Time.time < deadline)
        {
            foreach (var light in lights)
                if (!light.IsSpawned && absorbed.Add(light.GetInstanceID()))
                    Check(light.RemainingLifetime > .1f, "Firefly is absorbed before lifetime expiration");
            yield return null;
        }
        Check(lights.All(l => !l.IsSpawned && l.RemainingLifetime > .1f), "All nine lights follow a relocated caster and are absorbed");
        Check(owner.HitsTaken == 0, "Absorption does not damage the caster");
        FireflyLight.Cast(owner, Vector2.zero, stats);
        var crossing = ProjectileBase.ActiveProjectiles.OfType<FireflyLight>().First();
        foreach (var p in ProjectileBase.ActiveProjectiles.ToArray()) if (p != crossing) p.RemoveProjectile();
        crossing.enabled = false;
        owner.transform.position = Vector2.zero;
        crossing.transform.position = new Vector2(3, 0);
        typeof(FireflyLight).GetField("center", Private).SetValue(crossing, new Vector2(3, 0));
        typeof(FireflyLight).GetField("previousOwnerPosition", Private).SetValue(crossing, Vector2.zero);
        owner.transform.position = new Vector2(6, 0);
        typeof(FireflyLight).GetMethod("Tick", Private).Invoke(crossing, new object[] { .01f });
        Check(!crossing.IsSpawned, "Swept absorption catches a player dashing through a light");
        crossing.enabled = true;

        owner.transform.position = new Vector2(4, 0);
        enemy.ResetCombatant(new Vector2(1, 0));
        FireflyLight.Cast(owner, Vector2.zero, stats);
        var damaging = ProjectileBase.ActiveProjectiles.OfType<FireflyLight>().First();
        foreach (var p in ProjectileBase.ActiveProjectiles.ToArray()) if (p != damaging) p.RemoveProjectile();
        damaging.enabled = false;
        damaging.transform.position = Vector2.zero;
        typeof(FireflyLight).GetField("center", Private).SetValue(damaging, Vector2.zero);
        typeof(FireflyLight).GetField("heading", Private).SetValue(damaging, Vector2.right);
        typeof(FireflyLight).GetField("previousOwnerPosition", Private).SetValue(damaging, new Vector2(4, 0));
        yield return new WaitForSeconds(.65f);
        BarrageVisibility.Select(0);
        typeof(FireflyLight).GetMethod("Tick", Private).Invoke(damaging, new object[] { .7f });
        Check(enemy.HitsTaken == 1 && !damaging.IsSpawned && owner.HitsTaken == 0, "Invisible own firefly still damages an enemy on its return path");
        damaging.enabled = true;

        owner.transform.position = new Vector2(-6, -3); enemy.transform.position = new Vector2(6, -3);
        var stationary = new WeaponStats(1, .1f, 30, .5f, 10, new Color(.3f, 1, .6f));
        var own = ProjectileBase.SpawnBuiltin<CelestialBullet>(owner, new Vector2(-2, 0), Vector2.right, stationary);
        var other = ProjectileBase.SpawnBuiltin<CelestialBullet>(enemy, new Vector2(2, 0), Vector2.right, stationary);
        yield return null;
        var ownSprite = own.GetComponent<SpriteRenderer>(); var otherSprite = other.GetComponent<SpriteRenderer>();
        var block = new MaterialPropertyBlock();
        ownSprite.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1, "Own projectile stores caster identity");
        otherSprite.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 2, "Enemy projectile retains separate identity");
        BarrageVisibility.ApplyOwner(ownSprite, 1);
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) BarrageVisibility.ApplyOwner(ownSprite, 1);
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated == 0, "Warmed visibility assignment allocates zero bytes");
        var materials = new[] { BarrageVisibility.SpriteMaterial, NeonBeamStyle.Material, NeonVisualStyle.Get(4), NeonVisualStyle.Get(5), NeonVisualStyle.Get(6), NeonVisualStyle.Get(7), NeonVisualStyle.Get(8), NeonVisualStyle.Get(9), NeonVisualStyle.Glow, NeonVisualStyle.Ribbon };
        foreach (var theme in new[] { GraphicsThemeKind.Classic, GraphicsThemeKind.Neon })
        {
            GraphicsTheme.Select(theme);
            foreach (var material in materials)
            {
                ownSprite.sharedMaterial = otherSprite.sharedMaterial = material;
                BarrageVisibility.SetLocalOwner(1); BarrageVisibility.Select(1);
                var full = Energy();
                BarrageVisibility.Select(0); var hidden = Energy();
                Check(full.x > 100 && hidden.x < 5 && Mathf.Abs(hidden.y - full.y) < Mathf.Max(30, full.y * .01f), "Own body/glow hides; enemy unchanged: " + theme + "/" + material.name + " full=" + full + " hidden=" + hidden);
                BarrageVisibility.Select(.25f); var quarter = Energy();
                Check(quarter.x > full.x * .03f && quarter.x < full.x * .8f, "Partial opacity is visible: " + material.name);
                BarrageVisibility.SetLocalOwner(2); BarrageVisibility.Select(0); var swapped = Energy();
                Check(swapped.y < 5 && Mathf.Abs(swapped.x - full.x) < Mathf.Max(30, full.x * .01f), "Guest local ownership swaps visibility correctly: full=" + full + " swapped=" + swapped);
            }
        }
        BarrageVisibility.SetLocalOwner(1); BarrageVisibility.Select(.4f);
        ownSprite.sharedMaterial = otherSprite.sharedMaterial = NeonVisualStyle.Get(4);
        var match = new GameObject("Capture match").AddComponent<MatchController>(); match.enabled = false; match.Configure(owner, enemy);
        var host = new GameObject("Capture host").AddComponent<ClassicPresentation>(); host.enabled = false; host.Initialize(match, true);
        var guest = new GameObject("Capture guest").AddComponent<ClassicPresentation>(); guest.enabled = false; guest.Initialize(null, false);
        var captured = host.Capture(1, 1).SelectMany(f => f.Views).ToArray();
        var ownView = captured.Single(v => v.Id == ownSprite.GetInstanceID());
        Check(ownView.Owner == 1 && ownView.Color == stationary.Color, "Network capture preserves gameplay color and tags ownership");
        var writer = WriterPool.Retrieve();
        foreach (byte tag in new byte[] { 0, 1, 2 })
        foreach (int sprite in Enumerable.Range(0, 9))
        {
            writer.Clear(); var view = ownView; view.Owner = tag; view.Sprite = sprite;
            view.Points = new[] { Vector2.zero, Vector2.right }; view.Clipped = true; view.ClipRect = new Vector4(-8, -4, 8, 4);
            writer.WriteClassicVisual(view);
            var reader = ReaderPool.Retrieve(writer.GetArraySegment(), null);
            var decoded = reader.ReadClassicVisual(); reader.Store();
            Check(decoded.Owner == tag && decoded.Sprite == sprite && decoded.Clipped && writer.Length == ClassicVisualCodec.SizeOf(view), "Ownership round trip keeps packet size: " + sprite + "/" + tag);
        }
        writer.Store();
        guest.Apply(new ClassicVisualFrame { Round = 1, Sequence = 1, Views = captured });
        Check(guest.ActiveReplicaCount == captured.Length, "Guest creates all captured visuals");
        var remote = guest.GetComponentsInChildren<SpriteRenderer>().First(r => r.sprite == RuntimeShapes.Circle && r.transform.position.x < -1 && r.transform.position.y > -1);
        remote.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1, "Remote sprite preserves owner for local opacity");
        BarrageVisibility.Select(.1f);
        Check(host.Capture(1, 2).Count == 0, "Changing local opacity does not generate network visual changes");
        var ribbon = ownView; ribbon.Id = 888001; ribbon.Glow = NeonVisualStyle.PrismMode;
        var line = ownView; line.Id = 888002; line.Sprite = 0; line.Points = new[] { Vector2.left, Vector2.right };
        line.StartWidth = line.EndWidth = .1f; line.EndColor = line.Color;
        guest.Apply(new ClassicVisualFrame { Round = 1, Sequence = 2, Views = new[] { ribbon, line } });
        var remoteTrail = guest.GetComponentsInChildren<TrailRenderer>().Single(t => t.enabled);
        remoteTrail.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1, "Guest prism trail follows own opacity");
        var remoteLine = guest.GetComponentInChildren<LineRenderer>();
        remoteLine.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1, "Guest line follows own opacity");
        guest.ClearReplicas(); Destroy(guest.gameObject); Destroy(host.gameObject); Destroy(match.gameObject);
        Check(own.Reflect(enemy), "Reflection transfers projectile ownership");
        ownSprite.GetPropertyBlock(block); Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 2, "Reflected projectile refreshes visibility ownership");
        own.RemoveProjectile(); other.RemoveProjectile();
        var reused = ProjectileBase.SpawnBuiltin<CelestialBullet>(owner, Vector2.zero, Vector2.right, stationary);
        reused.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
        Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1, "Pool reuse resets visibility ownership");
        ProjectileBase.DespawnAll();

        // A configured/clipped beam must preserve ownership after its post-spawn setup.
        var beam = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(owner, Vector2.zero, Vector2.right, stationary);
        beam.Configure(new BarrageStep { shape = BarrageShape.Line, duration = 1, dealsDamage = true });
        beam.ClipToArena(new Rect(-8, -4, 16, 8));
        foreach (var renderer in beam.GetComponentsInChildren<Renderer>())
        {
            renderer.GetPropertyBlock(block);
            Check(block.GetFloat(BarrageVisibility.OwnerProperty) == 1 && block.GetVector("_ClipRect").z == 8, "Clipping preserves owner tag");
        }
        ProjectileBase.DespawnAll();
        render.Release(); Destroy(render); Destroy(readback);
        Destroy(owner.gameObject); Destroy(enemy.gameObject);
        cameraView.tag = "Untagged"; cameraView.enabled = false;
        BarrageVisibility.Select(1);
        yield return null;
        // Reuse the existing gameplay/replication stress checks after the shared renderer changes.
        var regression = (IEnumerator)typeof(CelestialBarrageSmokeRunner).GetMethod("Verify", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        while (regression.MoveNext()) yield return regression.Current;
        var regressionResults = (List<string>)typeof(CelestialBarrageSmokeRunner).GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        File.WriteAllText("ValidationOutput/celestial-opacity-regression.txt", string.Join("\n", regressionResults));
        int regressionChecks = (int)typeof(CelestialBarrageSmokeRunner).GetField("checks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(regressionChecks > 300, "Celestial gameplay and replication regression passed: " + regressionChecks);
    }
    private static PlayerCombatant Player(string name, WeaponDefinition weapon, int index, Vector2 position)
    {
        var player = new GameObject(name).AddComponent<PlayerCombatant>();
        player.ConfigurePreview(weapon);
        typeof(PlayerCombatant).GetField("playerIndex", Private).SetValue(player, index);
        player.ResetCombatant(position); player.gameObject.layer = 30;
        return player;
    }
    private static Vector2 Energy()
    {
        var old = cameraView.targetTexture; var active = RenderTexture.active;
        cameraView.targetTexture = render; cameraView.Render(); RenderTexture.active = render;
        readback.ReadPixels(new Rect(0, 0, 256, 144), 0, 0); readback.Apply();
        var pixels = readback.GetPixels32(); Vector2 result = Vector2.zero;
        for (int y = 54; y < 90; y++) for (int x = 76; x < 180; x++)
        {
            Color32 pixel = pixels[y * 256 + x]; float light = pixel.r + pixel.g + pixel.b;
            if (x < 116) result.x += light;
            else if (x >= 140) result.y += light;
        }
        cameraView.targetTexture = old; RenderTexture.active = active;
        return result;
    }
    private static void CaptureSettings()
    {
        var camera = Camera.main;
        var canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var target = RenderTexture.GetTemporary(1280, 720, 24); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        File.WriteAllBytes("ValidationOutput/opacity-settings.png", pixels.EncodeToPNG()); Destroy(pixels);
        camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
        RenderTexture.ReleaseTemporary(target); Canvas.ForceUpdateCanvases();
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++; Results.Add("OK: " + message);
    }
}
