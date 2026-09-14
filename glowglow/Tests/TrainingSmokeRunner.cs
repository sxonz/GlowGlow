using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Copy only into an isolated project, as described in Tests/README.md.
public sealed class TrainingSmokeRunner : MonoBehaviour
{
    private static bool booted;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isBatchMode || booted) return;
        booted = true;
        var runner = new GameObject("Training checks").AddComponent<TrainingSmokeRunner>();
        DontDestroyOnLoad(runner);
        runner.StartCoroutine(runner.Run());
    }
    private IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception ex)
            {
                File.WriteAllText("training-result.txt", ex.ToString());
                Debug.LogException(ex); EditorApplication.Exit(1); yield break;
            }
            yield return current;
        }
        File.WriteAllText("training-result.txt", "PASS: title entry without deck, all weapons and upgrades, reset/cooldown/projectile cleanup, retained settings, unlimited hits, moving target, UI layout, classic mode isolation.");
        Debug.Log("TRAINING_SMOKE_PASSED");
        EditorApplication.Exit(0);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Invoke(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static Button Button(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);
    private static IEnumerator Verify()
    {
        yield return null;
        string saved = PlayerPrefs.GetString(DeckCatalog.SaveKey, "{}");
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        // Replace only the title controller reference with an empty transient catalog.
        // This proves entry does not depend on deck validity without writing PlayerPrefs.
        var title = FindFirstObjectByType<TitleScreenController>();
        typeof(TitleScreenController).GetField("deckCatalog", Flags).SetValue(title, ScriptableObject.CreateInstance<DeckCatalog>());
        title.PlaySolo();
        yield return new WaitForSeconds(.3f);
        var menu = (CanvasGroup)typeof(TitleScreenController).GetField("soloPanel", Flags).GetValue(title);
        Capture("training-entry.png", 1600, 900, menu.GetComponentInParent<Canvas>());
        Button("훈련장").onClick.Invoke();
        yield return new WaitForSeconds(.6f);
        var match = FindFirstObjectByType<MatchController>();
        var training = FindFirstObjectByType<TrainingGround>();
        Check(training != null && match.IsTraining && match.IsPlaying && !match.IsDrafting, "Training start");
        Check(match.PlayerTwo.CurrentWeapon == null && match.PlayerOne.Hand == null, "Sandbox equipment");
        var upgrades = Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades");
        for (int i = 0; i < catalog.cards.Length; i++)
        {
            match.PlayerOne.transform.position = new Vector2(-2.5f, 1.2f);
            match.PlayerTwo.transform.position = new Vector2(3.2f, -1.4f);
            typeof(TrainingGround).GetField("targetPhase", Flags).SetValue(training, 2.4f);
            training.SelectWeapon(i);
            CheckPositions(match, training);
            Check(training.Selected.Definition == catalog.cards[i].weapon, "Catalog order");
            foreach (var upgrade in upgrades.Where(u => u.CanApplyTo(training.Selected.Definition)))
            {
                Invoke(training, "ChangeLevel", upgrade, upgrade.maxLevel);
                Check(training.Selected.Upgrades.GetLevel(upgrade) == upgrade.maxLevel, "Upgrade max");
                CheckPositions(match, training);
                Invoke(training, "ChangeLevel", upgrade, 0);
                Check(training.Selected.Upgrades.GetLevel(upgrade) == 0, "Upgrade removal");
                CheckPositions(match, training);
            }
            Invoke(training, "SetAll", false);
            CheckPositions(match, training);
            Invoke(training, "SetAll", true);
            CheckPositions(match, training);
            var runtime = training.Selected;
            training.SelectWeapon(i + 1);
            training.SelectWeapon(i);
            Check(ReferenceEquals(runtime, training.Selected), "Retain per-weapon build");
            CheckPositions(match, training);
            runtime.Fire(match.PlayerOne, match.PlayerOne.MuzzlePosition, Vector2.right, match.PlayerTwo.transform.position);
            yield return new WaitForSeconds(.12f);
            training.ResetArena();
            Check((Vector2)match.PlayerOne.transform.position == new Vector2(-5.8f, 0) &&
                (Vector2)match.PlayerTwo.transform.position == new Vector2(4.8f, 0), "Explicit reset restores spawn positions");
            Check(runtime.CooldownRemaining == 0 && !match.PlayerOne.IsOverdriving, "Reset active effects/cooldown");
            Check(!FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None).Any(p => p.gameObject.activeInHierarchy), "Projectile cleanup");
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var text in GameObject.Find("Training Ground UI").GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Text overflow: " + text.text);
            }
        }
        training.SelectWeapon(0);
        Invoke(training, "SetAll", false);
        yield return new WaitForSeconds(.6f);
        training.Selected.Fire(match.PlayerOne, match.PlayerOne.MuzzlePosition, Vector2.right, match.PlayerTwo.transform.position);
        yield return new WaitForSeconds(1.4f);
        Check(match.PlayerTwo.HitsTaken > 0, "Actual projectile/target physics");
        training.ResetArena();
        for (int hit = 0; hit < 4; hit++)
        {
            yield return new WaitForSeconds(.85f);
            match.PlayerTwo.ReceiveHit(match.PlayerOne);
        }
        Check(match.PlayerTwo.HitsTaken == 4 && match.IsPlaying, "No three-hit match end");
        Button("표적: 고정").onClick.Invoke();
        yield return new WaitForSeconds(.3f);
        Check(Mathf.Abs(match.PlayerTwo.transform.position.y) > .1f, "Moving target");
        Button("표적: 고정").onClick.Invoke();
        training.SelectWeapon(0);
        yield return null;
        Capture("training-1600.png", 1600, 900);
        Capture("training-1280.png", 1280, 720);
        Capture("training-4x3.png", 1200, 900);
        training.SelectWeapon(4);
        yield return null;
        Capture("training-pulse.png", 1600, 900);
        Check(PlayerPrefs.GetString(DeckCatalog.SaveKey, "{}") == saved, "Saved deck changed");
        SceneManager.LoadScene("Title");
        yield return new WaitForSeconds(.3f);
        SceneManager.LoadScene("Arena");
        yield return new WaitForSeconds(.3f);
        match = FindFirstObjectByType<MatchController>();
        Check(match == null || !match.IsTraining && match.IsDrafting, "Training request leaked to classic");
    }
    private static void CheckPositions(MatchController match, TrainingGround training)
    {
        Check((Vector2)match.PlayerOne.transform.position == new Vector2(-2.5f, 1.2f), "Loadout change moved player");
        Check((Vector2)match.PlayerTwo.transform.position == new Vector2(3.2f, -1.4f), "Loadout change moved target");
        Check(Mathf.Approximately((float)typeof(TrainingGround).GetField("targetPhase", Flags).GetValue(training), 2.4f), "Loadout change reset target movement phase");
    }

    private static void Capture(string path, int width, int height, Canvas targetCanvas = null)
    {
        var camera = Camera.main;
        var canvas = targetCanvas != null ? targetCanvas : GameObject.Find("Training Ground UI").GetComponent<Canvas>();
        var output = new RenderTexture(width, height, 24);
        camera.targetTexture = output;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        camera.GetComponent<ArenaCameraLayout>()?.Apply();
        Canvas.ForceUpdateCanvases();
        var training = FindFirstObjectByType<TrainingGround>();
        if (training != null) Invoke(training, "LateUpdate");
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = output;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Destroy(pixels); output.Release(); Destroy(output);
    }
}
