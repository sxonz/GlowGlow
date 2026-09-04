using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public static class TitleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Title.unity";
    private static readonly Color Pink = Hex("FF3FCB");
    private static readonly Color Violet = Hex("8B5CFF");
    private static readonly Color White = Hex("FFF8FF");
    private static TMP_FontAsset font;

    [MenuItem("GlowGlow/Build Title Screen")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = GetOrCreateFontAsset();
        var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/glowglow.jpeg");

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("080414");
        camera.orthographic = true;
        cameraGo.transform.position = new Vector3(0, 0, -10);

        var canvasGo = new GameObject("Title UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var root = canvasGo.GetComponent<RectTransform>();
        var bg = Image("Background", root, background, Color.white, Vector2.zero, Vector2.one);
        bg.preserveAspect = false;
        Image("Dark Wash", root, null, new Color(0.025f, 0.008f, 0.09f, 0.44f), Vector2.zero, Vector2.one);
        var shade = Image("Left Shade", root, null, Color.white, Vector2.zero, new Vector2(.68f, 1));
        shade.gameObject.AddComponent<UIHorizontalGradient>();

        var top = Text("Eyebrow", root, "ONLINE BULLET HELL · ROGUELIKE", 22, new Color(1, .75f, .96f, .9f), TextAlignmentOptions.MidlineLeft);
        SetRect(top.rectTransform, new Vector2(.075f, .87f), new Vector2(.55f, .93f));

        var title = Text("Logo", root, "GLOW\nGLOW", 116, White, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(.07f, .52f), new Vector2(.56f, .86f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 70;
        title.fontSizeMax = 116;
        title.lineSpacing = .75f;
        title.gameObject.AddComponent<Shadow>().effectColor = new Color(1, 0, .72f, .8f);
        title.GetComponent<Shadow>().effectDistance = new Vector2(8, -3);

        var tagline = Text("Tagline", root, "탄막을 고르고, 강화하고, 끝까지 살아남아라.", 25, new Color(1, .9f, 1, .9f), TextAlignmentOptions.MidlineLeft);
        SetRect(tagline.rectTransform, new Vector2(.075f, .48f), new Vector2(.58f, .54f));

        var menuGo = Panel("Main Menu", root, new Vector2(.075f, .13f), new Vector2(.50f, .46f));
        var menu = menuGo.AddComponent<CanvasGroup>();
        var layout = menuGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        var multiplayer = MenuButton(menuGo.transform, "MULTIPLAYER", "두 플레이어 · 온라인 매치", Pink, 1.25f);
        var solo = MenuButton(menuGo.transform, "SOLO", "COMING LATER", Violet, 1f);
        var settings = MenuButton(menuGo.transform, "SETTINGS", "사운드 및 게임 설정", new Color(.55f, .36f, .85f, 1), 1f);
        var quit = MenuButton(menuGo.transform, "QUIT", "게임 종료", new Color(.28f, .2f, .42f, 1), .86f);

        var settingsGo = Panel("Settings Panel", root, new Vector2(.075f, .13f), new Vector2(.50f, .46f));
        var settingsCanvas = settingsGo.AddComponent<CanvasGroup>();
        var settingsBg = settingsGo.AddComponent<Image>();
        settingsBg.color = new Color(.045f, .018f, .13f, .94f);
        var outline = settingsGo.AddComponent<Outline>();
        outline.effectColor = new Color(1, .2f, .8f, .7f);
        outline.effectDistance = new Vector2(2, -2);
        var heading = Text("Heading", settingsGo.transform, "SETTINGS", 38, White, TextAlignmentOptions.MidlineLeft);
        SetRect(heading.rectTransform, new Vector2(.07f, .72f), new Vector2(.93f, .94f));
        var volumeLabel = Text("Volume Label", settingsGo.transform, "MASTER VOLUME", 20, new Color(1, .8f, 1, 1), TextAlignmentOptions.MidlineLeft);
        SetRect(volumeLabel.rectTransform, new Vector2(.07f, .5f), new Vector2(.93f, .66f));
        var slider = Slider(settingsGo.transform);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(.07f, .36f), new Vector2(.93f, .49f));
        var back = MenuButton(settingsGo.transform, "BACK", "메인 메뉴로", Pink, 1f);
        SetRect(back.GetComponent<RectTransform>(), new Vector2(.07f, .06f), new Vector2(.93f, .27f));

        var status = Text("Status", root, "READY  ·  SELECT A MODE", 18, new Color(.95f, .74f, 1, .9f), TextAlignmentOptions.MidlineRight);
        SetRect(status.rectTransform, new Vector2(.62f, .055f), new Vector2(.925f, .105f));
        var hint = Text("Hint", root, "WASD / STICK  이동     ENTER / A  선택", 16, new Color(1, 1, 1, .62f), TextAlignmentOptions.MidlineLeft);
        SetRect(hint.rectTransform, new Vector2(.075f, .045f), new Vector2(.48f, .1f));

        var controllerGo = new GameObject("Title Screen Controller", typeof(TitleScreenController));
        var controller = controllerGo.GetComponent<TitleScreenController>();
        SetObject(controller, "mainMenu", menu);
        SetObject(controller, "settingsPanel", settingsCanvas);
        SetObject(controller, "statusText", status);
        SetObject(controller, "firstButton", multiplayer);
        SetObject(controller, "settingsBackButton", back);
        SetObject(controller, "masterVolume", slider);
        UnityEventTools.AddPersistentListener(multiplayer.onClick, controller.PlayMultiplayer);
        UnityEventTools.AddPersistentListener(solo.onClick, controller.PlaySolo);
        UnityEventTools.AddPersistentListener(settings.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.QuitGame);
        UnityEventTools.AddPersistentListener(back.onClick, controller.CloseSettings);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        settingsCanvas.alpha = 0;
        settingsCanvas.interactable = false;
        settingsCanvas.blocksRaycasts = false;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("GlowGlow title screen built successfully.");
    }

    public static void BuildBatch() { Build(); EditorApplication.Exit(0); }

    private static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), min, max);
        return go;
    }

    private static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        SetRect(image.rectTransform, min, max);
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, int size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button MenuButton(Transform parent, string label, string sublabel, Color accent, float weight)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(.035f, .012f, .11f, .88f);
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1, .7f, 1, 1);
        colors.selectedColor = new Color(1, .55f, .95f, 1);
        colors.pressedColor = accent;
        colors.fadeDuration = .08f;
        button.colors = colors;
        go.GetComponent<LayoutElement>().flexibleHeight = weight;
        var bar = Image("Accent", go.transform, null, accent, new Vector2(0, 0), new Vector2(.018f, 1));
        bar.rectTransform.offsetMax = Vector2.zero;
        var main = Text("Label", go.transform, label, 27, White, TextAlignmentOptions.MidlineLeft);
        SetRect(main.rectTransform, new Vector2(.07f, .35f), new Vector2(.68f, .9f));
        var sub = Text("Sub Label", go.transform, sublabel, 14, new Color(1, .75f, 1, .72f), TextAlignmentOptions.MidlineRight);
        SetRect(sub.rectTransform, new Vector2(.42f, .08f), new Vector2(.94f, .42f));
        return button;
    }

    private static Slider Slider(Transform parent)
    {
        var go = new GameObject("Master Volume", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var bg = Image("Background", go.transform, null, new Color(.25f, .14f, .38f, 1), new Vector2(0, .38f), new Vector2(1, .62f));
        var fillArea = Panel("Fill Area", go.transform, new Vector2(0, .38f), new Vector2(1, .62f));
        var fill = Image("Fill", fillArea.transform, null, Pink, Vector2.zero, Vector2.one);
        fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = Panel("Handle Slide Area", go.transform, new Vector2(0, 0), new Vector2(1, 1));
        var handle = Image("Handle", handleArea.transform, null, White, new Vector2(0, .12f), new Vector2(.035f, .88f));
        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = .8f;
        return slider;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetObject(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        const string assetPath = "Assets/Font/HiKR-ExtraBold SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        const string settingsFolder = "Assets/TextMesh Pro/Resources";
        const string settingsPath = settingsFolder + "/TMP Settings.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                AssetDatabase.CreateFolder("Assets", "TextMesh Pro");
            if (!AssetDatabase.IsValidFolder(settingsFolder))
                AssetDatabase.CreateFolder("Assets/TextMesh Pro", "Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TMP_Settings>(), settingsPath);
            AssetDatabase.SaveAssets();
        }

        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/하이커 폰트/HiKR-ExtraBold.ttf");
        var created = TMP_FontAsset.CreateFontAsset(source);
        created.name = "HiKR-ExtraBold SDF";
        created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        AssetDatabase.CreateAsset(created, assetPath);
        AssetDatabase.AddObjectToAsset(created.material, created);
        AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
        AssetDatabase.SaveAssets();
        return created;
    }
}
