// Copy into Assets only in the isolated validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponIconSmokeRunner : MonoBehaviour
{
    private const string AtlasPath = "Assets/UI/BarrageIcons/WeaponIcons.png";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Results = new();
    private static int checks;
    private static DeckCatalog catalog;

    public static void Begin()
    {
        Directory.CreateDirectory("ValidationOutput");
        try
        {
            var previous = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>()
                .ToDictionary(s => s.name, s => GlobalObjectId.GetGlobalObjectIdSlow(s).ToString());
            Type.GetType("WeaponIconAuthoring, Assembly-CSharp-Editor", true).GetMethod("Bake").Invoke(null, null);
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>())
                if (!previous.TryGetValue(sprite.name, out var id) || id != GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString())
                    throw new InvalidOperationException("Rebake changed a serialized sprite identity: " + sprite.name);
            File.WriteAllText("ValidationOutput/weapon-icon-rebake.txt", "All 19 sprite identities survive rebaking.");
            EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode && Environment.CommandLine.Contains("WeaponIconSmokeRunner.Begin"))
            new GameObject("Icon validation").AddComponent<WeaponIconSmokeRunner>().StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        var iterator = Verify();
        while (true)
        {
            object next;
            try { if (!iterator.MoveNext()) break; next = iterator.Current; }
            catch (Exception ex)
            {
                File.WriteAllText("ValidationOutput/weapon-icons-result.txt", string.Join("\n", Results) + "\nFAIL: " + ex);
                Debug.LogException(ex); EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        Results.Add("PASS: " + checks + " checks.");
        File.WriteAllText("ValidationOutput/weapon-icons-result.txt", string.Join("\n", Results));
        Debug.Log("WEAPON_ICON_SMOKE_PASSED: " + checks); EditorApplication.Exit(0);
    }

    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        ValidateAssets();
        yield return null;
        var title = FindFirstObjectByType<TitleScreenController>();
        title.OpenDeck();
        yield return new WaitForSeconds(.5f);
        var deck = FindFirstObjectByType<DeckScreen>();
        var nextButton = (Button)typeof(DeckScreen).GetField("next", Private).GetValue(deck);
        for (int page = 0; page < 4; page++)
        {
            var visible = deck.GetComponentsInChildren<Image>().Where(i => i.name == "Weapon Icon").ToArray();
            Check(visible.Length == (page < 3 ? 6 : 1), "Real collection page icon count: " + page);
            for (int i = 0; i < visible.Length; i++)
                Check(visible[i].sprite == catalog.cards[page * 6 + i].weapon.icon && visible[i].color == Color.white &&
                    visible[i].preserveAspect, "Collection uses the full colored icon: " + visible[i].sprite.name);
            Capture("weapon-icons-deck-" + (page + 1) + ".png", 1920, 1080);
            if (nextButton.interactable) { nextButton.onClick.Invoke(); yield return null; }
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);

        // Actual Image rendering at the collection/draft sizes and the 26px upgrade size.
        var board = new GameObject("Icon contact sheet", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var boardCanvas = board.GetComponent<Canvas>(); boardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = board.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 1000); scaler.matchWidthOrHeight = .5f;
        Label(board.transform, "GLOWGLOW / BARRAGE ICONS", new Vector2(0, 455), new Vector2(1100, 40), 28);
        Label(board.transform, "90 px  /  48 px  /  26 px  ·  prepared glow · 19 unique silhouettes", new Vector2(0, 420), new Vector2(1100, 32), 16);
        for (int i = 0; i < catalog.cards.Length; i++)
        {
            var weapon = catalog.cards[i].weapon;
            float x = -500 + i % 5 * 250, y = 288 - i / 5 * 210;
            Icon(board.transform, weapon.icon, new Vector2(x - 35, y), 90);
            Icon(board.transform, weapon.icon, new Vector2(x + 48, y), 48);
            Icon(board.transform, weapon.icon, new Vector2(x + 99, y), 26);
            Label(board.transform, weapon.displayName, new Vector2(x + 18, y - 73), new Vector2(238, 30), 18);
        }
        yield return null;
        Capture("weapon-icons-sheet.png", 1280, 1000);
        board.SetActive(false);

        // Supply the same Korean HUD font the real arena provides to the draft.
        var fontHost = new GameObject("Arena font fixture").AddComponent<ArenaHud>(); fontHost.enabled = false;
        Label(fontHost.transform, "", Vector2.zero, Vector2.one, 16).font =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/HiKR-ExtraBold SDF.asset");
        var draftScreen = new GameObject("Draft fixture").AddComponent<OpeningDraftScreen>();
        var draft = new OpeningDraft(catalog.cards.Select(c => c.weapon), new System.Random(67), null);
        var botDraft = new OpeningDraft(catalog.cards.Select(c => c.weapon), new System.Random(97), null);
        while (!botDraft.IsComplete) botDraft.Confirm(true);
        draftScreen.Show(draft, _ => { }, botDraft.Choices, 1200);
        yield return new WaitForSeconds(1.3f);
        var draftIcons = draftScreen.GetComponentsInChildren<Image>().Where(i => i.name == "Icon").ToArray();
        Check(draftIcons.Length == 6 && draftIcons.All(i => i.color == Color.white && i.preserveAspect &&
            AssetDatabase.GetAssetPath(i.sprite) == AtlasPath), "Actual opening draft and bot picks retain bright baked cores");
        Capture("weapon-icons-draft.png", 1920, 1080);
        draftScreen.Hide();

        var player = new GameObject("HUD preview player").AddComponent<PlayerCombatant>();
        player.ConfigurePreview(catalog.cards[0].weapon);
        var bar = new GameObject("HUD fixture").AddComponent<DrawnWeaponBar>(); bar.enabled = false;
        var slotRoot = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        var slot = new DrawnWeaponBar.Slot { button = slotRoot.GetComponent<Button>(), selection = slotRoot.GetComponent<Outline>(),
            icon = Icon(slotRoot.transform, null, Vector2.zero, 48), cooldownClock = Icon(slotRoot.transform, null, Vector2.zero, 48),
            name = Label(slotRoot.transform, "", Vector2.zero, Vector2.one * 100, 16),
            remaining = Label(slotRoot.transform, "", Vector2.zero, Vector2.one * 100, 16) };
        var status = Label(slotRoot.transform, "", Vector2.zero, Vector2.one * 100, 16);
        var shards = new GameObject("Upgrade fixture", typeof(RectTransform), typeof(UpgradeShardDisplay)).GetComponent<UpgradeShardDisplay>();
        foreach (var card in catalog.cards)
        {
            player.ConfigurePreview(card.weapon);
            bar.Configure(player, status, RuntimeShapes.Circle, new[] { slot });
            Check(slot.icon.sprite == card.weapon.icon && slot.icon.color == Color.white && slot.icon.preserveAspect,
                "HUD slot: " + card.weapon.displayName);
            shards.Show(new WeaponRuntime(card.weapon), Array.Empty<WeaponUpgradeDefinition>());
            var core = shards.transform.Find("Weapon Icon").GetComponent<Image>();
            Check(core.sprite == card.weapon.icon && core.color == Color.white, "Upgrade core: " + card.weapon.displayName);
        }
        bar.Refresh();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) bar.Refresh();
        Check(GC.GetAllocatedBytesForCurrentThread() - bytes == 0, "Warmed HUD refresh allocates zero bytes");
    }

    private static void ValidateAssets()
    {
        Check(catalog.cards.Length == 19, "All 19 registered barrages covered");
        var imported = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        Check(!imported.mipmapEnabled && !imported.isReadable && imported.textureCompression == TextureImporterCompression.Uncompressed,
            "No mip blur, lossy compression or CPU texture copy");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(AtlasPath));
        var pixels = texture.GetPixels32();
        var silhouettes = new HashSet<ulong>();
        var sprites = new HashSet<Sprite>();
        Texture2D shared = catalog.cards[0].weapon.icon.texture;
        foreach (var card in catalog.cards)
        {
            var sprite = card.weapon.icon;
            Check(sprites.Add(sprite) && sprite.name == card.weapon.displayName && sprite.texture == shared,
                "Unique, correctly assigned atlas sprite: " + card.weapon.displayName);
            Check(sprite.rect.size == new Vector2(192, 192) && sprite.pivot == new Vector2(96, 96) && sprite.vertices.Length == 4,
                "Whole centered canvas, no component slicing: " + sprite.name);
            ulong hash = 1469598103934665603UL;
            int solid = 0, glow = 0; bool safeEdge = true;
            for (int y = 0; y < 192; y++)
            for (int x = 0; x < 192; x++)
            {
                Color32 pixel = pixels[((int)sprite.rect.y + y) * texture.width + (int)sprite.rect.x + x];
                hash = unchecked((hash ^ pixel.a) * 1099511628211UL);
                if (pixel.a > 240 && Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b)) > 195) solid++;
                if (pixel.a > 4 && pixel.a < 110) glow++;
                if (x < 3 || y < 3 || x >= 189 || y >= 189) safeEdge &= pixel.a == 0;
            }
            Check(silhouettes.Add(hash), "Distinct geometry, not a recolored duplicate: " + sprite.name);
            Check(solid > 90 && glow > 1000 && safeEdge, "Crisp core, soft halo and clean gutter: " + sprite.name);
        }
        Results.Add($"Shared atlas: {shared.width}x{shared.height}, {shared.format}, {shared.mipmapCount} mip level, " +
            $"{UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(shared)} Editor profiler bytes.");
        Check(shared.width == 1024 && shared.height == 1024 && shared.format == TextureFormat.RGBA32 && shared.mipmapCount == 1,
            "One 1024-square RGBA32 atlas, 4 MiB of texels; Editor profiler also counts editor-side storage");
        Results.Add(File.ReadAllText("ValidationOutput/weapon-icon-rebake.txt"));
        Destroy(texture);
    }

    private static Image Icon(Transform parent, Sprite sprite, Vector2 position, float size)
    {
        var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.sprite = sprite; image.color = Color.white; image.preserveAspect = true;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = Vector2.one * size;
        return image;
    }
    private static TMP_Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false); label.font = TMP_Settings.defaultFontAsset; label.fontSize = fontSize;
        label.text = value; label.color = new Color(.8f, .82f, .9f); label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
        return label;
    }
    private static void Capture(string name, int width, int height)
    {
        var cameraObject = new GameObject("UI capture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.018f, .012f, .04f); camera.orthographic = true;
        camera.cullingMask &= ~(1 << 31); // Match the title camera's isolated preview exclusion.
        camera.transform.position = new Vector3(0, 0, -10);
        var render = new RenderTexture(width, height, 24); camera.targetTexture = render;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5; }
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = render;
        var result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply();
        File.WriteAllBytes("ValidationOutput/" + name, result.EncodeToPNG());
        RenderTexture.active = previous;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        camera.targetTexture = null; Destroy(result); Destroy(render); Destroy(cameraObject);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; Results.Add("PASS: " + message); }
}
