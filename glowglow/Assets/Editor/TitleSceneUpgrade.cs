using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TitleSceneUpgrade
{
    [MenuItem("GlowGlow/Upgrade Existing Title UI")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before editing the title scene.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Title.unity")
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
        }
        var controller = UnityEngine.Object.FindFirstObjectByType<TitleScreenController>();
        controller.BakeSceneUI();
        Canvas.ForceUpdateCanvases();
        foreach (var layout in UnityEngine.Object.FindObjectsByType<VerticalLayoutGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Verify(controller);
        Debug.Log("TITLE_UI_BAKED_AND_VERIFIED");
    }

    private static void Verify(TitleScreenController controller)
    {
        var serialized = new SerializedObject(controller);
        foreach (string field in new[] { "soloPanel", "multiplayerPanel", "deckPanel", "deckButton", "deckScreen", "volumeValueText", "classicButton", "soloBackButton", "multiplayerBackButton" })
            if (serialized.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing baked reference: " + field);
        var menu = (CanvasGroup)serialized.FindProperty("mainMenu").objectReferenceValue;
        if (menu.GetComponentsInChildren<Button>().Length != 5) throw new InvalidOperationException("Expected five main menu buttons.");
        foreach (var button in menu.GetComponentsInChildren<Button>())
            if (((RectTransform)button.transform).rect.height < 70) throw new InvalidOperationException("Main menu button is too short.");
        var slider = (Slider)serialized.FindProperty("masterVolume").objectReferenceValue;
        float height = slider.handleRect.rect.height;
        float original = slider.value;
        slider.SetValueWithoutNotify(0);
        if (Mathf.Abs(slider.handleRect.rect.height - height) > .01f) throw new InvalidOperationException("Slider handle changed size.");
        slider.SetValueWithoutNotify(1);
        if (Mathf.Abs(slider.handleRect.rect.height - height) > .01f) throw new InvalidOperationException("Slider handle changed size.");
        slider.SetValueWithoutNotify(original);
    }

    public static void UpgradeBatch()
    {
        try { Upgrade(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void CaptureBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<TitleScreenController>();
            var serialized = new SerializedObject(controller);
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (string shown in new[] { "mainMenu", "soloPanel", "deckPanel" })
            {
                foreach (string field in new[] { "mainMenu", "soloPanel", "multiplayerPanel", "settingsPanel", "deckPanel" })
                {
                    var panel = (CanvasGroup)serialized.FindProperty(field).objectReferenceValue;
                    panel.gameObject.SetActive(field == shown);
                    panel.alpha = 1;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Library/TitlePreviews");
                File.WriteAllBytes("Library/TitlePreviews/" + shown + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("TITLE_PREVIEWS_CAPTURED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
