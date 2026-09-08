using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ArenaSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Arena.unity";
    private static TMP_FontAsset font;

    [MenuItem("GlowGlow/Build Local Arena")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/HiKR-ExtraBold SDF.asset");
        var weapon = GetOrCreateWeapon();

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0, 0, -10);
        var camera = cameraGo.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("090315");

        CreateArenaFrame();
        CreateGrid();

        var matchGo = new GameObject("Match Controller", typeof(MatchController));
        var match = matchGo.GetComponent<MatchController>();
        var p1 = CreatePlayer("Player 1", new Vector2(-5.8f, 0), Hex("FF42CE"));
        var p2 = CreatePlayer("Player 2", new Vector2(5.8f, 0), Hex("7B68FF"));
        match.Configure(p1, p2);
        p1.Configure(1, match, weapon, LocalPlayerInput.ControlScheme.MouseAndKeyboard, camera, p2.transform);
        p2.Configure(2, match, weapon, LocalPlayerInput.ControlScheme.KeyboardTwo, camera, p1.transform);

        CreateHud(match);

        EditorSceneManager.SaveScene(scene, ScenePath);
        var title = new EditorBuildSettingsScene("Assets/Scenes/Title.unity", true);
        var arena = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = new[] { title, arena };
        AssetDatabase.SaveAssets();
        Debug.Log("GlowGlow local arena built successfully.");
    }

    public static void BuildBatch() { Build(); EditorApplication.Exit(0); }

    private static PlayerCombatant CreatePlayer(string name, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(LocalPlayerInput), typeof(PlayerCombatant));
        go.transform.position = position;
        go.transform.localScale = Vector3.one * .72f;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.color = color;
        renderer.sortingOrder = 5;
        var collider = go.GetComponent<CircleCollider2D>();
        collider.radius = .48f;

        var ring = new GameObject("Aim Ring", typeof(SpriteRenderer));
        ring.transform.SetParent(go.transform, false);
        ring.transform.localScale = Vector3.one * 1.35f;
        var ringRenderer = ring.GetComponent<SpriteRenderer>();
        ringRenderer.color = new Color(color.r, color.g, color.b, .18f);
        ringRenderer.sortingOrder = 4;
        return go.GetComponent<PlayerCombatant>();
    }

    private static void CreateArenaFrame()
    {
        var frame = new GameObject("Arena Border", typeof(LineRenderer));
        var line = frame.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.SetPositions(new[] { new Vector3(-8.7f, -4.65f), new Vector3(-8.7f, 4.65f), new Vector3(8.7f, 4.65f), new Vector3(8.7f, -4.65f) });
        line.startWidth = line.endWidth = .035f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = Hex("F449D9");
        line.sortingOrder = 1;
    }

    private static void CreateGrid()
    {
        var root = new GameObject("Arena Grid").transform;
        for (int x = -8; x <= 8; x += 2) CreateGridLine(root, new Vector3(x, -4.5f), new Vector3(x, 4.5f));
        for (int y = -4; y <= 4; y += 2) CreateGridLine(root, new Vector3(-8.5f, y), new Vector3(8.5f, y));
    }

    private static void CreateGridLine(Transform parent, Vector3 a, Vector3 b)
    {
        var go = new GameObject("Grid Line", typeof(LineRenderer));
        go.transform.SetParent(parent);
        var line = go.GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPositions(new[] { a, b });
        line.startWidth = line.endWidth = .012f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(.55f, .2f, .8f, .12f);
    }

    private static void CreateHud(MatchController match)
    {
        var canvasGo = new GameObject("Arena HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var root = canvasGo.GetComponent<RectTransform>();

        var topBar = Image("Top Bar", root, new Color(.025f, .008f, .08f, .88f));
        SetRect(topBar.rectTransform, new Vector2(0, .88f), Vector2.one);
        var p1 = Text("P1 Lives", topBar.transform, "P1  ◆ ◆ ◆", 32, Hex("FF78DE"), TextAlignmentOptions.MidlineLeft);
        SetRect(p1.rectTransform, new Vector2(.04f, 0), new Vector2(.38f, 1));
        var p2 = Text("P2 Lives", topBar.transform, "◆ ◆ ◆  P2", 32, Hex("A795FF"), TextAlignmentOptions.MidlineRight);
        SetRect(p2.rectTransform, new Vector2(.62f, 0), new Vector2(.96f, 1));
        var timer = Text("Timer", topBar.transform, "08:00", 42, Color.white, TextAlignmentOptions.Center);
        SetRect(timer.rectTransform, new Vector2(.4f, 0), new Vector2(.6f, 1));

        var help = Text("Controls", root, "P1  WASD · MOUSE · SPACE     |     P2  ARROWS · RCTRL · RSHIFT     |     ESC  TITLE", 18, new Color(1, 1, 1, .58f), TextAlignmentOptions.Center);
        SetRect(help.rectTransform, new Vector2(.12f, .015f), new Vector2(.88f, .07f));
        var result = Text("Result", root, string.Empty, 64, Color.white, TextAlignmentOptions.Center);
        SetRect(result.rectTransform, new Vector2(.25f, .35f), new Vector2(.75f, .65f));
        result.gameObject.AddComponent<Outline>().effectColor = new Color(1, 0, .8f, .75f);

        var hud = canvasGo.AddComponent<ArenaHud>();
        hud.Configure(match, p1, p2, timer, result);
        ArenaWeaponBarBuilder.Bake(canvas, match);
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
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

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static WeaponDefinition GetOrCreateWeapon()
    {
        const string path = "Assets/Settings/Pulse Shot.asset";
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (weapon != null) return weapon;
        weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        AssetDatabase.CreateAsset(weapon, path);
        return weapon;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}
