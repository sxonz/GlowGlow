using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ArenaWeaponBarBuilder
{
    public static void Bake(Canvas canvas, MatchController match)
    {
        match.ConfigureDeckRules(AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset"));
        ApplyLayout(canvas);
        if (canvas.GetComponentInChildren<DrawnWeaponBar>(true) != null) return;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/HiKR-ExtraBold SDF.asset");
        var icon = CreateSprite("Pulse Icon", false);
        var clock = CreateSprite("Cooldown Ring", true);
        var pulse = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Pulse Shot.asset");
        if (pulse.icon == null) { pulse.icon = icon; EditorUtility.SetDirty(pulse); }
        match.PlayerOne.ConfigureDeck(AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset"));
        EditorUtility.SetDirty(match.PlayerOne);
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var root = Rect(canvas.transform, "Drawn Weapon Bar", new Vector2(.5f, 0), new Vector2(.5f, 0));
        root.pivot = new Vector2(.5f, 0);
        root.sizeDelta = new Vector2(780, 158);
        root.anchoredPosition = new Vector2(0, 10);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(.035f, .013f, .09f, .98f);
        var border = root.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(.65f, .3f, .85f, .65f);
        var status = Text(root, "Hand Status", "", font, 18, new Vector2(.025f, .81f), new Vector2(.975f, .99f));
        var slots = new DrawnWeaponBar.Slot[WeaponHand.SlotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            float left = .025f + i * .193f;
            var rect = Rect(root, $"Weapon Slot {i + 1}", new Vector2(left, .07f), new Vector2(left + .178f, .78f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.11f, .055f, .19f, 1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var outline = rect.gameObject.AddComponent<Outline>();
            Text(rect, "Key", (i + 1).ToString(), font, 20, new Vector2(.06f, .73f), new Vector2(.25f, .98f));
            var symbol = Image(rect, "Icon", icon, new Vector2(.5f, .59f), new Vector2(48, 48));
            var ring = Image(rect, "Cooldown Clock", clock, new Vector2(.5f, .59f), new Vector2(66, 66));
            ring.type = UnityEngine.UI.Image.Type.Filled;
            ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.color = new Color(1f, .3f, .8f);
            var name = Text(rect, "Weapon Name", "빈 슬롯", font, 17, new Vector2(.05f, .16f), new Vector2(.95f, .33f));
            var remaining = Text(rect, "Cooldown Remaining", "—", font, 17, new Vector2(.05f, .01f), new Vector2(.95f, .17f));
            name.alignment = remaining.alignment = TextAlignmentOptions.Center;
            slots[i] = new DrawnWeaponBar.Slot { button = button, icon = symbol, cooldownClock = ring, selection = outline, name = name, remaining = remaining };
        }
        root.gameObject.AddComponent<DrawnWeaponBar>().Configure(match.PlayerOne, status, icon, slots);
        var controls = canvas.transform.Find("Controls");
        if (controls != null)
        {
            var text = controls.GetComponent<TMP_Text>();
            text.text = "P1  WASD · MOUSE · SPACE     |     P2  ARROWS · RCTRL · RSHIFT     |     ESC  TITLE";
            var rect = (RectTransform)controls;
            rect.anchorMin = new Vector2(.12f, .162f);
            rect.anchorMax = new Vector2(.88f, .19f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            text.fontSize = 15;
        }
        Canvas.ForceUpdateCanvases();
    }

    private static void ApplyLayout(Canvas canvas)
    {
        var camera = Camera.main;
        var layout = camera.GetComponent<ArenaCameraLayout>();
        if (layout == null) layout = camera.gameObject.AddComponent<ArenaCameraLayout>();
        layout.Apply();
        var bar = canvas.GetComponentInChildren<DrawnWeaponBar>(true);
        if (bar != null) ((RectTransform)bar.transform).sizeDelta = new Vector2(780, 158);
        var controls = canvas.transform.Find("Controls") as RectTransform;
        if (controls != null)
        {
            controls.anchorMin = new Vector2(.12f, .162f);
            controls.anchorMax = new Vector2(.88f, .19f);
            controls.offsetMin = controls.offsetMax = Vector2.zero;
        }
        var top = canvas.transform.Find("Top Bar") as RectTransform;
        if (top != null) top.anchorMin = new Vector2(0, .925f);
    }

    private static Sprite CreateSprite(string name, bool ring)
    {
        const string folder = "Assets/UI";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "UI");
        string path = folder + "/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alpha = 0;
            if (ring)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(47.5f, 47.5f));
                alpha = Mathf.Clamp01(45 - radius) * Mathf.Clamp01(radius - 37);
            }
            else
            {
                for (int j = 0; j < 3; j++)
                {
                    var center = new Vector2(22 + j * 24, 27 + j * 15);
                    float dx = Mathf.Abs(x - center.x) - 5;
                    float dy = Mathf.Abs(y - center.y) - 12;
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1 - Mathf.Max(dx, dy)));
                }
            }
            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Image(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size)
    {
        var image = Rect(parent, name, anchor, anchor).gameObject.AddComponent<Image>();
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, int size, Vector2 min, Vector2 max)
    {
        var text = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .75f;
        text.fontSizeMax = size;
        return text;
    }

    public static void UpgradeBatch()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity", OpenSceneMode.Single);
            var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            Bake(UnityEngine.Object.FindFirstObjectByType<Canvas>(), match);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            VerifyAndCapture(match);
            Debug.Log("ARENA_WEAPON_BAR_BAKED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void VerifyAndCapture(MatchController match)
    {
        var samples = new WeaponDefinition[6];
        var pulse = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Pulse Shot.asset");
        for (int i = 0; i < samples.Length; i++) samples[i] = UnityEngine.Object.Instantiate(pulse);
        foreach (int size in new[] { 0, 1, 2, 5, 6 })
        {
            var deck = new WeaponDefinition[size];
            Array.Copy(samples, deck, size);
            var hand = new WeaponHand(deck, new System.Random(7));
            if (hand.Drawn.Count != Mathf.Min(5, Mathf.Max(0, size - 1))) throw new Exception("Incorrect proper-subset draw size.");
            var unique = new System.Collections.Generic.HashSet<WeaponDefinition>();
            foreach (var drawn in hand.Drawn)
                if (Array.IndexOf(deck, drawn.Definition) < 0 || !unique.Add(drawn.Definition)) throw new Exception("Invalid or duplicate drawn weapon.");
            if (hand.Select(5) || hand.Select(-1)) throw new Exception("Invalid slot was selected.");
        }
        var duplicates = new WeaponHand(new[] { pulse, pulse }, new System.Random(7));
        if (duplicates.DeckCount != 1 || duplicates.Drawn.Count != 0) throw new Exception("Duplicate deck entries were counted as distinct weapons.");

        var previewHand = new WeaponHand(samples, new System.Random(7));
        match.PlayerOne.AttachMatch(match);
        typeof(MatchController).GetProperty("IsPlaying").SetValue(match, true);
        typeof(PlayerCombatant).GetProperty("Hand").SetValue(match.PlayerOne, previewHand);
        var first = previewHand.Drawn[0];
        first.Definition.cooldown = 8;
        first.Fire(match.PlayerOne, Vector2.zero, Vector2.right);
        if (first.CooldownRemaining <= 0 || first.Fire(match.PlayerOne, Vector2.zero, Vector2.right) != null)
            throw new Exception("Cooldown did not prevent repeated fire.");
        match.PlayerOne.SelectWeaponSlot(1);
        if (previewHand.Drawn[1].CooldownRemaining != 0) throw new Exception("Cooldown leaked into another weapon.");
        match.PlayerOne.SelectWeaponSlot(0);
        if (first.CooldownRemaining <= 0) throw new Exception("Switching reset a weapon cooldown.");
        var bar = UnityEngine.Object.FindFirstObjectByType<DrawnWeaponBar>();
        bar.SendMessage("Start");
        var buttons = bar.GetComponentsInChildren<Button>();
        if (buttons.Length != 5) throw new Exception("Expected five baked slots.");
        buttons[2].onClick.Invoke();
        if (match.PlayerOne.CurrentWeapon != previewHand.Drawn[2]) throw new Exception("Slot click did not select the weapon.");
        buttons[0].onClick.Invoke();
        bar.Refresh();
        foreach (var label in bar.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Directory.CreateDirectory("Library/ArenaPreviews");
        File.WriteAllText("Library/ArenaPreviews/verification.txt", "PASS: D/G proper subset, uniqueness, empty/single-card decks, slot bounds, click selection, independent cooldowns, switch persistence, five serialized slots.");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            ProjectileBase.DespawnAll();
            match.PlayerOne.SendMessage("Awake");
            match.PlayerTwo.SendMessage("Awake");
            match.PlayerOne.ResetCombatant(new Vector2(-5.8f, 0));
            match.PlayerTwo.ResetCombatant(new Vector2(5.8f, 0));
            if (match.PlayerOne.CurrentWeapon == null) throw new Exception("Basic shot was disabled by an incomplete deck.");
            if (match.PlayerOne.Hand.Drawn.Count == 0 && !match.PlayerOne.SelectWeaponSlot(0)) throw new Exception("Basic slot cannot be selected.");
            bar.Refresh();
            var canvas = bar.GetComponentInParent<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.GetComponent<ArenaCameraLayout>().Apply();
            if (camera.rect != new Rect(0, 0, 1, 1) || camera.clearFlags != CameraClearFlags.SolidColor)
                throw new Exception("Arena does not clear the full screen.");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            RenderTexture.active = target;
            GL.Clear(true, true, Color.magenta);
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            if (image.GetPixel(10, 10).r > .8f) throw new Exception("Stale pixels remain in the bottom HUD region.");
            File.WriteAllBytes("Library/ArenaPreviews/weapon-bar.png", image.EncodeToPNG());
        }
        Debug.Log("ARENA_HAND_TESTS_PASSED");
    }
}
