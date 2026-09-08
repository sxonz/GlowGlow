using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public sealed class PulsePreviewSmokeRunner : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode) new GameObject("Pulse preview tests").AddComponent<PulsePreviewSmokeRunner>().StartCoroutine(Run());
    }
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception ex) { Debug.LogException(ex); File.WriteAllText("pulse-preview-result.txt", ex.ToString()); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        File.WriteAllText("pulse-preview-result.txt", "PASS: baked UI, pulse hit/knockback/75% slow/0.3s expiry, pause/reset, render output, every catalog preview, isolated cleanup.");
        Debug.Log("PULSE_PREVIEW_SMOKE_PASSED");
        EditorApplication.Exit(0);
    }
    private static IEnumerator Verify()
    {
        yield return new WaitForSeconds(.2f);
        var controller = UnityEngine.Object.FindFirstObjectByType<TitleScreenController>();
        controller.OpenDeck();
        yield return new WaitForSeconds(.8f);
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckScreen>();
        Check(deck.HasPreview, "Preview must be baked into Title");
        var preview = UnityEngine.Object.FindFirstObjectByType<BarragePreview>();
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        typeof(DeckScreen).GetMethod("SelectCard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(deck, new object[] { catalog.cards[4] });
        var target = Field<PlayerCombatant>(preview, "target");
        var shooter = Field<PlayerCombatant>(preview, "shooter");
        Check(target.gameObject.scene.GetPhysicsScene2D() != Physics2D.defaultPhysicsScene, "Preview must isolate physics");
        float timeout = Time.time + 2;
        while (target.MovementMultiplier == 1 && Time.time < timeout) yield return null;
        Check(Mathf.Approximately(target.MovementMultiplier, .75f), "Pulse did not apply 75% speed");
        Check(target.HitsTaken == 0 && shooter.HitsTaken == 0, "Pulse must not deal damage");
        Check(target.CanBeHit, "Non-damaging pulse must not grant damage invulnerability");
        float initialX = target.transform.position.x;
        float slowStarted = Time.time;
        yield return new WaitForSeconds(.10f);
        Check(target.transform.position.x > initialX, "Pulse did not knock back");
        Check(Mathf.Approximately(target.MovementMultiplier, .75f), "Slow expired too early");
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.1f);
        Check(Mathf.Approximately(target.MovementMultiplier, .75f), "Pause consumed slow timer");
        Time.timeScale = 1;
        yield return new WaitForSeconds(.24f);
        Check(target.MovementMultiplier == 1 && Time.time - slowStarted < .4f, "Slow must expire after 0.3 seconds");
        Check(target.HitsTaken == 0, "Pulse contact must remain damage-free");
        // Read the actual render texture; a non-uniform image proves this is a live view.
        var rt = Field<RenderTexture>(preview, "texture");
        Check(rt != null && rt.IsCreated(), "Missing preview texture");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            var values = pixels.GetPixels32(); int bright = 0;
            foreach (var p in values) if (p.r > 90 || p.g > 90 || p.b > 90) bright++;
            Check(bright > 20, "Preview camera rendered no objects");
            File.WriteAllBytes("preview-world.png", pixels.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels);
            var camera = Camera.main;
            var canvas = deck.GetComponentInParent<Canvas>();
            var output = new RenderTexture(1600, 900, 24);
            camera.targetTexture = output;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = output;
            var capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); capture.Apply();
            File.WriteAllBytes("deck-preview.png", capture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.Destroy(capture); output.Release(); UnityEngine.Object.Destroy(output);
        }
        // Every card uses the same real runtime, including the remaining temporary weapons.
        foreach (var card in catalog.cards)
        {
            typeof(DeckScreen).GetMethod("SelectCard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(deck, new object[] { card });
            yield return new WaitForSeconds(.65f);
            Check(Field<PlayerCombatant>(preview, "shooter").CurrentWeapon.Definition == card.weapon, "Wrong preview weapon");
            Check(!Field<TMP_Text>(deck, "details").isTextOverflowing, "Deck description overflows for " + card.weapon.displayName);
        }
        target.ApplyImpact(Vector2.right, .55f, .75f, .3f);
        preview.Restart();
        Check(target.MovementMultiplier == 1, "Replay retained slow");
        var scene = shooter.gameObject.scene;
        deck.gameObject.SetActive(false);
        yield return null; yield return null;
        Check(Field<RenderTexture>(preview, "texture") == null, "Closing deck leaked render texture");
        Check(!scene.isLoaded, "Closing deck leaked preview scene");
    }
}
