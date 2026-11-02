using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public sealed class TitleScreenController : MonoBehaviour
{
    [SerializeField] private CanvasGroup mainMenu;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button firstButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Slider masterVolume;
    [SerializeField] private DeckCatalog deckCatalog;

    private Coroutine transition;
    [SerializeField] private CanvasGroup soloPanel;
    [SerializeField] private CanvasGroup multiplayerPanel;
    [SerializeField] private Button soloBackButton;
    [SerializeField] private Button multiplayerBackButton;
    [SerializeField] private Button classicButton;
    [SerializeField] private Button soloButton;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private CanvasGroup deckPanel;
    [SerializeField] private Button deckButton;
    [SerializeField] private DeckScreen deckScreen;
    private Button classicThemeButton, neonThemeButton;
    private Button trainingButton;
    private const float ModeRowHeight = 88;
    private Slider barrageOpacity;
    private TMP_Text barrageOpacityValue;

    private void Start()
    {
        BarrageVisibility.SetLocalOwner(1);
        var training = CreateModeButton(soloPanel, "훈련장", "TRAINING · 모든 탄막 / 업그레이드 자유 테스트");
        trainingButton = training;
        training.transform.SetSiblingIndex(0);
        training.onClick.AddListener(() => { MatchController.RequestTraining(); SceneManager.LoadScene("Arena"); });
        foreach (var existing in soloPanel.GetComponentsInChildren<Button>(true))
            foreach (var label in existing.GetComponentsInChildren<TMP_Text>(true))
                if (label.text == "봇 대전")
                {
                    existing.gameObject.SetActive(false);
                    Destroy(existing.gameObject);
                    break;
                }
        var botButton = CreateModeButton(soloPanel, "봇 대전", "BOT MATCH");
        botButton.transform.SetSiblingIndex(1);
        botButton.onClick.AddListener(() =>
        {
            soloPanel.interactable = soloPanel.blocksRaycasts = false;
            BotMatchSetup.Show(mainMenu.transform.parent,
            firstButton.GetComponentInChildren<TMP_Text>(true).font, rating =>
            {
                if (!RequireCompleteDeck(soloPanel)) return;
                MatchController.RequestBotMatch(rating);
                SceneManager.LoadScene("Arena");
            }, () => { soloPanel.interactable = soloPanel.blocksRaycasts = true; Select(botButton); });
        });
        ConfigureSoloScrolling();
        SetLabels(soloButton, "SINGLEPLAYER", "훈련장 · 봇 대전 · 캠페인");
        deckButton.onClick.AddListener(OpenDeck);
        soloBackButton.onClick.AddListener(() => CloseModes(soloPanel, soloButton));
        multiplayerBackButton.onClick.AddListener(() => CloseModes(multiplayerPanel, firstButton));
        classicButton.onClick.AddListener(PlayClassic);
        deckScreen.Initialize(deckCatalog, () => CloseModes(deckPanel, deckButton));
        Show(mainMenu, true);
        Show(settingsPanel, false);
        Show(soloPanel, false);
        Show(multiplayerPanel, false);
        Show(deckPanel, false);
        Select(firstButton);
        if (masterVolume != null)
        {
            masterVolume.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = settingsBackButton
            };
            settingsBackButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = masterVolume
            };
            masterVolume.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 0.8f)));
            SetMasterVolume(masterVolume.value);
        }
        BuildThemeSettings();
        ApplySecondaryTheme();
        RefreshThemeButtons();
    }

    // Main menu artwork stays intact; all secondary screens share the barrage icon language.
    public void ApplySecondaryTheme()
    {
        var font = firstButton.GetComponentInChildren<TMP_Text>(true).font;
        NeonUI.Place((RectTransform)settingsPanel.transform, .10f, .10f, .90f, .90f);
        NeonUI.Panel(settingsPanel.GetComponent<Image>(), NeonUI.Violet);
        var heading = settingsPanel.transform.Find("Heading").GetComponent<TMP_Text>();
        heading.fontSize = heading.fontSizeMax = 54;
        NeonUI.Place(heading.rectTransform, .065f, .75f, .44f, .86f);
        NeonUI.Text(settingsPanel.transform, "Theme Eyebrow", "SYSTEM / PERSONALIZATION", font, 15, NeonUI.Cyan, .065f, .87f, .45f, .93f);
        NeonUI.Text(settingsPanel.transform, "Theme Description", "화면과 소리를\n나에게 맞춰 조정하세요.", font, 25, NeonUI.Muted, .065f, .17f, .44f, .30f);
        NeonUI.Art(settingsPanel.transform, "System Orbit", "Orbit Seal", new Color(.65f,.45f,1,.42f), .08f,.32f,.40f,.70f).preserveAspect = true;
        NeonUI.Rule(settingsPanel.transform, "Settings Divider", new Color(.34f,.87f,1,.35f), .51f,.61f,.93f);
        PlaceSetting("Volume Label", .51f,.75f,.79f,.81f);
        PlaceSetting("Volume Value", .79f,.75f,.93f,.81f);
        NeonUI.Place((RectTransform)masterVolume.transform, .51f,.66f,.93f,.73f);
        PlaceSetting("Barrage Opacity Label", .51f,.51f,.83f,.58f);
        PlaceSetting("Barrage Opacity Value", .83f,.51f,.93f,.58f);
        if (barrageOpacity != null) NeonUI.Place((RectTransform)barrageOpacity.transform,.51f,.43f,.93f,.50f);
        PlaceSetting("Theme Heading",.51f,.32f,.93f,.38f);
        if (classicThemeButton != null) NeonUI.Place((RectTransform)classicThemeButton.transform,.51f,.20f,.71f,.29f);
        if (neonThemeButton != null) NeonUI.Place((RectTransform)neonThemeButton.transform,.73f,.20f,.93f,.29f);
        NeonUI.Place((RectTransform)settingsBackButton.transform,.065f,.065f,.30f,.15f);
        foreach (var button in settingsPanel.GetComponentsInChildren<Button>(true)) NeonUI.Button(button,NeonUI.Violet);
        NeonUI.Slider(masterVolume,NeonUI.Pink);
        if (barrageOpacity != null) NeonUI.Slider(barrageOpacity,NeonUI.Cyan);
        ThemeModes(soloPanel,"SINGLEPLAYER","혼자서도, 끝없이.","Training / Bot match", "Training",NeonUI.Cyan,font);
        ThemeModes(multiplayerPanel,"MULTIPLAYER","서로 다른 탄막, 한 번의 승부.","Classic / Online duel","Network",NeonUI.Pink,font);
        deckScreen.ApplyVisualTheme();
    }

    private void PlaceSetting(string name,float x0,float y0,float x1,float y1)
    {
        var element = settingsPanel.transform.Find(name) as RectTransform;
        if (element != null) NeonUI.Place(element,x0,y0,x1,y1);
    }

    private static void ThemeModes(CanvasGroup panel,string title,string subtitle,string eyebrow,string glyph,Color accent,TMP_FontAsset font)
    {
        if (panel == null) return;
        NeonUI.Place((RectTransform)panel.transform,.10f,.12f,.90f,.88f);
        NeonUI.Panel(panel.GetComponent<Image>(),accent);
        var content = panel.transform.Find("Content") as RectTransform;
        NeonUI.Place(content,.51f,.08f,.94f,.91f);
        var layout=content.GetComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight=false; layout.spacing=16;
        NeonUI.Text(panel.transform,"Mode Eyebrow",eyebrow.ToUpperInvariant(),font,16,accent,.065f,.85f,.46f,.92f);
        NeonUI.Text(panel.transform,"Mode Title",title,font,47,NeonUI.White,.065f,.72f,.47f,.84f);
        NeonUI.Text(panel.transform,"Mode Description",subtitle,font,23,NeonUI.Muted,.065f,.60f,.46f,.70f);
        NeonUI.Art(panel.transform,"Mode Orbit","Orbit Seal",new Color(accent.r,accent.g,accent.b,.24f),.10f,.17f,.40f,.54f).preserveAspect=true;
        NeonUI.Art(panel.transform,"Mode Sigil",glyph,accent,.17f,.25f,.33f,.45f).preserveAspect=true;
        foreach(var button in content.GetComponentsInChildren<Button>(true))
        {
            NeonUI.Button(button,accent,button.name!="BACK" && button.interactable);
            var row=button.GetComponent<LayoutElement>();
            if(row!=null) { row.minHeight=row.preferredHeight=button.name=="BACK"?72:102; row.flexibleHeight=0; }
            var visual=button.transform.Find("Hover Visual")??button.transform;
            string symbol=button.name=="BACK"?"Return":button.name=="훈련장"?"Training":button.name=="봇 대전"?"Bot":button.name=="캠페인"?"Campaign":"Network";
            NeonUI.Emblem(visual,symbol,accent,.035f,.19f,.15f,.81f);
            if(!button.interactable)
            {
                foreach(var part in new[]{"Lock Body","Lock Top","Lock Left","Lock Right"})
                {
                    var old=button.transform.Find(part)??visual.Find(part);
                    if(old!=null) old.gameObject.SetActive(false);
                }
                NeonUI.Art(visual,"Mode Lock","Lock",accent,.88f,.38f,.96f,.79f).preserveAspect=true;
            }
            var label=visual.Find("Label")?.GetComponent<TMP_Text>();
            var detail=visual.Find("Sub Label")?.GetComponent<TMP_Text>();
            if(label!=null) { NeonUI.Place(label.rectTransform,.19f,.35f,.86f,.88f); label.fontSize=label.fontSizeMax=27; }
            if(detail!=null) { NeonUI.Place(detail.rectTransform,.19f,.09f,.88f,.36f); detail.color=NeonUI.Muted; detail.alignment=TextAlignmentOptions.MidlineLeft; }
        }
    }

    private void BuildThemeSettings()
    {
        if (settingsPanel == null || masterVolume == null) return;
        var template = settingsPanel.transform.Find("Volume Label").GetComponent<TMP_Text>();
        SetVolumeRect((RectTransform)settingsPanel.transform.Find("Heading"), new Vector2(.07f, .84f), new Vector2(.93f, .96f), Vector2.zero);
        SetVolumeRect(template.rectTransform, new Vector2(.07f, .74f), new Vector2(.7f, .81f), Vector2.zero);
        if (volumeValueText != null)
            SetVolumeRect(volumeValueText.rectTransform, new Vector2(.7f, .74f), new Vector2(.93f, .81f), Vector2.zero);
        SetVolumeRect((RectTransform)masterVolume.transform, new Vector2(.07f, .65f), new Vector2(.93f, .73f), Vector2.zero);
        SetVolumeRect((RectTransform)settingsBackButton.transform, new Vector2(.07f, .04f), new Vector2(.93f, .19f), Vector2.zero);
        var opacityLabel = VolumeText(template, "Barrage Opacity Label", new Vector2(.07f, .54f), new Vector2(.77f, .61f));
        opacityLabel.text = "MY BARRAGE OPACITY";
        barrageOpacityValue = VolumeText(template, "Barrage Opacity Value", new Vector2(.77f, .54f), new Vector2(.93f, .61f));
        barrageOpacityValue.alignment = TextAlignmentOptions.MidlineRight;
        barrageOpacityValue.color = new Color(1, .65f, .9f);
        barrageOpacity = Instantiate(masterVolume, settingsPanel.transform);
        barrageOpacity.name = "Barrage Opacity";
        barrageOpacity.onValueChanged = new Slider.SliderEvent();
        barrageOpacity.minValue = 0;
        barrageOpacity.maxValue = 1;
        barrageOpacity.wholeNumbers = false;
        SetVolumeRect((RectTransform)barrageOpacity.transform, new Vector2(.07f, .45f), new Vector2(.93f, .53f), Vector2.zero);
        barrageOpacity.SetValueWithoutNotify(BarrageVisibility.Opacity);
        barrageOpacityValue.text = $"{Mathf.RoundToInt(BarrageVisibility.Opacity * 100)}%";
        barrageOpacity.onValueChanged.AddListener(SetBarrageOpacity);
        var heading = VolumeText(template, "Theme Heading", new Vector2(.07f, .34f), new Vector2(.93f, .41f));
        heading.text = "그래픽 테마";
        classicThemeButton = ThemeButton(template, "Classic Theme", .07f, .48f, GraphicsThemeKind.Classic);
        neonThemeButton = ThemeButton(template, "Neon Theme", .52f, .93f, GraphicsThemeKind.Neon);
        GraphicsTheme.Changed += RefreshThemeButtons;
        RefreshThemeButtons();
    }

    private Button ThemeButton(TMP_Text template, string name, float left, float right, GraphicsThemeKind theme)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(settingsPanel.transform, false);
        SetVolumeRect(rect, new Vector2(left, .23f), new Vector2(right, .33f), Vector2.zero);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1, .8f, 1);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.7f, .55f, .8f);
        button.colors = colors;
        var label = Instantiate(template, rect);
        label.name = "Label";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 23;
        label.color = Color.white;
        label.raycastTarget = false;
        SetVolumeRect(label.rectTransform, new Vector2(.025f, .05f), new Vector2(.975f, .95f), Vector2.zero);
        button.onClick.AddListener(() => GraphicsTheme.Select(theme));
        return button;
    }

    private void RefreshThemeButtons()
    {
        if (classicThemeButton == null || neonThemeButton == null) return;
        bool classic = GraphicsTheme.IsClassic;
        classicThemeButton.GetComponent<Image>().color = classic ? new Color(.55f, .12f, .43f) : new Color(.12f, .07f, .22f);
        neonThemeButton.GetComponent<Image>().color = !classic ? new Color(.55f, .12f, .43f) : new Color(.12f, .07f, .22f);
        classicThemeButton.GetComponentInChildren<TMP_Text>().text = classic ? "클래식 · 이전 (선택됨)" : "클래식 · 이전";
        neonThemeButton.GetComponentInChildren<TMP_Text>().text = !classic ? "네온 · 현재 (선택됨)" : "네온 · 현재";
        var selected = classic ? classicThemeButton : neonThemeButton;
        NeonUI.Selection(classicThemeButton,classic,NeonUI.Violet);
        NeonUI.Selection(neonThemeButton,!classic,NeonUI.Violet);
        masterVolume.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = barrageOpacity };
        barrageOpacity.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = masterVolume, selectOnDown = selected };
        classicThemeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = barrageOpacity, selectOnRight = neonThemeButton, selectOnDown = settingsBackButton };
        neonThemeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = barrageOpacity, selectOnLeft = classicThemeButton, selectOnDown = settingsBackButton };
        settingsBackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = selected };
    }

    private void OnDestroy() => GraphicsTheme.Changed -= RefreshThemeButtons;

#if UNITY_EDITOR
    // Called only by the editor builder. Runtime uses these serialized scene objects.
    public void BakeSceneUI()
    {
        if (deckPanel != null)
        {
            ConfigureSoloScrolling();
            if (deckScreen.DeckSlotCapacity != DeckCatalog.RequiredDeckSize || !deckScreen.HasPreview)
            {
                deckScreen.RebuildForEditor(deckCatalog);
                AddPanelFlash(deckPanel);
            }
            return;
        }
        var root = mainMenu.transform.parent;
        SetVolumeRect((RectTransform)mainMenu.transform, new Vector2(.075f, .13f), new Vector2(.50f, .58f), Vector2.zero);
        SetVolumeRect((RectTransform)settingsPanel.transform, new Vector2(.075f, .13f), new Vector2(.50f, .58f), Vector2.zero);
        SetVolumeRect((RectTransform)root.Find("Logo"), new Vector2(.07f, .67f), new Vector2(.56f, .91f), Vector2.zero);
        var logo = root.Find("Logo").GetComponent<TMP_Text>();
        logo.fontSizeMax = 104;
        SetVolumeRect((RectTransform)root.Find("Tagline"), new Vector2(.075f, .60f), new Vector2(.58f, .66f), Vector2.zero);
        SetVolumeRect((RectTransform)root.Find("Eyebrow"), new Vector2(.075f, .92f), new Vector2(.55f, .97f), Vector2.zero);
        CreateModePanels();
        ConfigureSoloScrolling();
        CreateDeckScreen();
        foreach (var button in mainMenu.GetComponentsInChildren<Button>())
            button.GetComponent<LayoutElement>().flexibleHeight = 1;
        mainMenu.GetComponent<VerticalLayoutGroup>().spacing = 12;
        volumeValueText = ConfigureVolumeSlider(masterVolume);
        AddPanelFlash(settingsPanel);
        AddPanelFlash(soloPanel);
        AddPanelFlash(multiplayerPanel);
        AddPanelFlash(deckPanel);
        AddHoverEffects(mainMenu);
        AddHoverEffects(settingsPanel);
        AddHoverEffects(soloPanel);
        AddHoverEffects(multiplayerPanel);
        Show(mainMenu, true);
        Show(settingsPanel, false);
        Show(soloPanel, false);
        Show(multiplayerPanel, false);
        Show(deckPanel, false);
        // Hidden panels are ordinary inactive objects: enabling one in the editor previews it.
        foreach (var panel in new[] { settingsPanel, soloPanel, multiplayerPanel, deckPanel })
        {
            panel.alpha = 1;
            panel.interactable = panel.blocksRaycasts = true;
        }
    }
#endif

    public void PlayMultiplayer()
    {
        if (!RequireCompleteDeck(mainMenu)) return;
        SetStatus("MULTIPLAYER  ·  모드 선택");
        BeginTransition(mainMenu, multiplayerPanel, classicButton);
    }

    public void PlaySolo()
    {
        SetStatus("SINGLEPLAYER  ·  훈련장 / 모드 선택");
        BeginTransition(mainMenu, soloPanel, trainingButton);
    }

    public void OpenDeck()
    {
        SetStatus("DECK  ·  탄막 도감 / 덱 구성");
        BeginTransition(mainMenu, deckPanel, deckScreen.BackButton);
    }

    private void CreateDeckScreen()
    {
        deckButton = Instantiate(firstButton, mainMenu.transform);
        deckButton.name = "DECK";
        deckButton.transform.SetSiblingIndex(2);
        deckButton.onClick = new Button.ButtonClickedEvent();
        deckButton.GetComponent<LayoutElement>().flexibleHeight = 1f;
        SetLabels(deckButton, "덱 구성", "탄막 도감 · 덱 편집");
        mainMenu.GetComponent<VerticalLayoutGroup>().spacing = 8;

        var go = new GameObject("Deck Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(mainMenu.transform.parent, false);
        SetVolumeRect(rect, new Vector2(.055f, .12f), new Vector2(.945f, .94f), Vector2.zero);
        go.GetComponent<Image>().color = new Color(.025f, .01f, .075f, .99f);
        var border = go.GetComponent<Outline>();
        border.effectColor = settingsPanel.GetComponent<Outline>().effectColor;
        border.effectDistance = new Vector2(2, -2);
        deckPanel = go.GetComponent<CanvasGroup>();
        deckScreen = go.AddComponent<DeckScreen>();
        deckScreen.Build(deckCatalog, firstButton.transform.Find("Label").GetComponent<TMP_Text>().font,
            () => CloseModes(deckPanel, deckButton));
        Show(deckPanel, false);
    }

    private void PlayClassic()
    {
        if (!RequireCompleteDeck(multiplayerPanel)) return;
        SetStatus("CLASSIC");
        GlowGlow.Online.OnlineSession.Open(deckCatalog);
    }

    private bool RequireCompleteDeck(CanvasGroup from)
    {
        if (deckCatalog != null && deckCatalog.IsSavedDeckValid()) return true;
        SetStatus("8종의 탄막으로 구성한 뒤 플레이하세요");
        BeginTransition(from, deckPanel, deckScreen.BackButton);
        return false;
    }

    private void CloseModes(CanvasGroup panel, Button selection)
    {
        SetStatus("READY  ·  SELECT A MODE");
        BeginTransition(panel, mainMenu, selection);
    }

    public void OpenSettings() => BeginTransition(mainMenu, settingsPanel, masterVolume);
    public void CloseSettings()
    {
        PlayerPrefs.Save();
        BeginTransition(settingsPanel, mainMenu, firstButton);
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        if (volumeValueText != null)
            volumeValueText.text = value <= 0f ? "0% · 음소거" : $"{Mathf.RoundToInt(value * 100f)}%";
    }

    public void SetBarrageOpacity(float value)
    {
        BarrageVisibility.Select(value);
        if (barrageOpacityValue != null) barrageOpacityValue.text = $"{Mathf.RoundToInt(BarrageVisibility.Opacity * 100)}%";
    }

    // Used by both existing scenes at startup and the title scene builder.
    public static TMP_Text ConfigureVolumeSlider(Slider slider)
    {
        var hitArea = slider.GetComponent<Image>();
        if (hitArea == null) hitArea = slider.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;

        var track = slider.transform.Find("Background").GetComponent<Image>();
        track.color = new Color(.32f, .23f, .44f, 1f);
        SetVolumeRect(track.rectTransform, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 8));

        var fillArea = (RectTransform)slider.fillRect.parent;
        SetVolumeRect(fillArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 8));
        slider.fillRect.GetComponent<Image>().color = new Color(1f, .25f, .8f, 1f);

        var handleArea = (RectTransform)slider.handleRect.parent;
        // Slider.UpdateVisuals stretches the handle's vertical anchors to 0..1.
        // Fix the height on its container so value changes cannot enlarge it.
        SetVolumeRect(handleArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 30));
        SetVolumeRect(slider.handleRect, new Vector2(slider.normalizedValue, 0),
            new Vector2(slider.normalizedValue, 1), new Vector2(20, 0));
        var handle = slider.handleRect.GetComponent<Image>();
        handle.color = Color.white;
        handle.raycastTarget = true;
        slider.targetGraphic = handle;
        var outline = handle.GetComponent<Outline>();
        if (outline == null) outline = handle.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, .25f, .8f, .8f);
        outline.effectDistance = new Vector2(2, -2);

        var colors = slider.colors;
        colors.normalColor = new Color(1f, .92f, 1f);
        colors.highlightedColor = new Color(1f, .65f, .9f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(1f, .25f, .8f);
        slider.colors = colors;

        var label = slider.transform.parent.Find("Volume Label").GetComponent<TMP_Text>();
        label.text = "MASTER VOLUME";
        label.rectTransform.anchorMax = new Vector2(.70f, .66f);
        var value = VolumeText(label, "Volume Value", new Vector2(.70f, .5f), new Vector2(.93f, .66f));
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.color = new Color(1f, .65f, .9f);
        value.text = $"{Mathf.RoundToInt(slider.value * 100f)}%";
        var hint = slider.transform.parent.Find("Volume Hint");
        if (hint != null)
        {
            if (Application.isPlaying) Destroy(hint.gameObject);
#if UNITY_EDITOR
            else DestroyImmediate(hint.gameObject);
#endif
        }
        return value;
    }

    private static TMP_Text VolumeText(TMP_Text template, string name, Vector2 min, Vector2 max)
    {
        var existing = template.transform.parent.Find(name);
        var text = existing != null ? existing.GetComponent<TMP_Text>() : Instantiate(template, template.transform.parent);
        text.name = name;
        text.raycastTarget = false;
        SetVolumeRect(text.rectTransform, min, max, Vector2.zero);
        return text;
    }

    private static void SetVolumeRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }

    // Editor construction reuses the title scene's existing button styling.
    private void CreateModePanels()
    {
        soloButton = mainMenu.transform.Find("SOLO")?.GetComponent<Button>();
        if (soloButton == null) soloButton = mainMenu.transform.Find("CAMPAIGN")?.GetComponent<Button>();
        if (soloButton != null) SetLabels(soloButton, "SINGLEPLAYER", "봇 대전 · 캠페인");
        SetLabels(firstButton, "MULTIPLAYER", "모드 선택");

        soloPanel = CreateModePanel("Singleplayer Modes", "SINGLEPLAYER");
        CreateModeButton(soloPanel, "봇 대전", "잠금 · 준비 중", true);
        CreateModeButton(soloPanel, "캠페인", "잠금 · 준비 중", true);
        soloBackButton = CreateModeButton(soloPanel, "BACK", "메인 메뉴로");
        soloBackButton.onClick.AddListener(() => CloseModes(soloPanel, soloButton));

        multiplayerPanel = CreateModePanel("Multiplayer Modes", "MULTIPLAYER");
        classicButton = CreateModeButton(multiplayerPanel, "클래식", "CLASSIC");
        var back = CreateModeButton(multiplayerPanel, "BACK", "메인 메뉴로");
        multiplayerBackButton = back;
    }

    private CanvasGroup CreateModePanel(string name, string heading)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(mainMenu.transform.parent, false);
        var source = (RectTransform)settingsPanel.transform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = source.sizeDelta;
        rect.anchoredPosition = source.anchoredPosition;
        var background = go.GetComponent<Image>();
        background.color = settingsPanel.GetComponent<Image>().color;
        var border = go.GetComponent<Outline>();
        var settingsBorder = settingsPanel.GetComponent<Outline>();
        border.effectColor = settingsBorder.effectColor;
        border.effectDistance = settingsBorder.effectDistance;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.SetParent(rect, false);
        SetVolumeRect(contentRect, new Vector2(.07f, .06f), new Vector2(.93f, .94f), Vector2.zero);
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        var title = Instantiate(firstButton.transform.Find("Label").GetComponent<TMP_Text>(), contentRect);
        title.name = "Heading";
        title.text = heading + " <size=60%> / 모드 선택</size>";
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 48;
        titleLayout.preferredHeight = 48;
        titleLayout.flexibleHeight = 0;
        var panel = go.GetComponent<CanvasGroup>();
        Show(panel, false);
        return panel;
    }

    private Button CreateModeButton(CanvasGroup panel, string label, string subtitle, bool locked = false)
    {
        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        var button = Instantiate(firstButton, scroll != null ? scroll.content : panel.transform.Find("Content"));
        button.name = label;
        // Replace the cloned persistent scene event as well as runtime listeners.
        button.onClick = new Button.ButtonClickedEvent();
        button.GetComponent<LayoutElement>().flexibleHeight = 1;
        SetLabels(button, label, subtitle);
        button.interactable = !locked;
        if (locked)
        {
            var tint = button.gameObject.AddComponent<CanvasGroup>();
            tint.alpha = .5f;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            AddLockPart(button.transform, "Lock Body", new Vector2(0, -4), new Vector2(22, 16));
            AddLockPart(button.transform, "Lock Top", new Vector2(0, 13), new Vector2(16, 4));
            AddLockPart(button.transform, "Lock Left", new Vector2(-6, 7), new Vector2(4, 12));
            AddLockPart(button.transform, "Lock Right", new Vector2(6, 7), new Vector2(4, 12));
        }
        return button;
    }

    private void ConfigureSoloScrolling()
    {
        if (soloPanel == null || soloBackButton == null) return;
        var root = (RectTransform)soloPanel.transform.Find("Content");
        var scroll = soloPanel.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null)
        {
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            var heading = root.Find("Heading");
            heading.SetAsFirstSibling();
            var list = new GameObject("Modes", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
            list.transform.SetParent(root, false);
            list.transform.SetSiblingIndex(1);
            var slot = list.GetComponent<LayoutElement>();
            slot.minHeight = 48;
            slot.preferredHeight = 0;
            slot.flexibleHeight = 1;
            list.GetComponent<Image>().color = Color.clear;
            scroll = list.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(list.transform, false);
            SetVolumeRect(viewport, Vector2.zero, Vector2.one, Vector2.zero);
            viewport.offsetMax = new Vector2(-14, 0);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = content.anchoredPosition = Vector2.zero;
            var rows = content.GetComponent<VerticalLayoutGroup>();
            rows.spacing = 12;
            rows.padding = new RectOffset(0, 18, 4, 4);
            rows.childControlWidth = rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == heading || child == list.transform || child == soloBackButton.transform) continue;
                child.SetParent(content, false);
                child.SetAsFirstSibling();
            }
            soloBackButton.transform.SetAsLastSibling();
            var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.SetParent(list.transform, false);
            SetVolumeRect(trackRect, new Vector2(1, 0), Vector2.one, new Vector2(6, 0));
            trackRect.pivot = new Vector2(1, .5f);
            track.GetComponent<Image>().color = new Color(.4f, .2f, .6f, .25f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            handle.transform.SetParent(trackRect, false);
            SetVolumeRect(handle.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            handle.color = new Color(.8f, .45f, 1, .8f);
            var bar = track.GetComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        var backLayout = soloBackButton.GetComponent<LayoutElement>();
        backLayout.minHeight = backLayout.preferredHeight = 80;
        backLayout.flexibleHeight = 0;
        // Rebuild navigation only when constructing the menu; hidden rows remain reachable.
        Button previous = soloBackButton, first = null;
        foreach (Transform row in scroll.content)
        {
            if (!row.TryGetComponent<Button>(out var button)) continue;
            var size = button.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = ModeRowHeight;
            size.flexibleHeight = 0;
            if (!button.interactable || !button.gameObject.activeSelf) continue;
            if (button.GetComponent<UIScrollSelection>() == null) button.gameObject.AddComponent<UIScrollSelection>();
            if (first == null) first = button;
            var before = previous.navigation;
            before.mode = Navigation.Mode.Explicit;
            before.selectOnDown = button;
            previous.navigation = before;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous };
            previous = button;
        }
        var last = previous.navigation;
        last.selectOnDown = soloBackButton;
        previous.navigation = last;
        soloBackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous, selectOnDown = first };
    }

    private static void AddLockPart(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var icon = go.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.color = new Color(1f, .75f, 1f);
        var rect = icon.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.94f, .65f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetLabels(Button button, string label, string subtitle)
    {
        var root = button.transform.Find("Hover Visual") ?? button.transform;
        root.Find("Label").GetComponent<TMP_Text>().text = label;
        root.Find("Sub Label").GetComponent<TMP_Text>().text = subtitle;
    }

    private static void Select(Selectable selection)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selection != null ? selection.gameObject : null);
    }

    private static void AddHoverEffects(CanvasGroup panel)
    {
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            var hover = button.GetComponent<UIHoverSlide>();
            if (hover == null) hover = button.gameObject.AddComponent<UIHoverSlide>();
            hover.PrepareVisual();
        }
    }

    private void BeginTransition(CanvasGroup from, CanvasGroup to, Selectable selection)
    {
        if (transition != null) return;
        var chromeRoot=mainMenu.transform.parent;
        var logo=chromeRoot.Find("Logo");
        var tagline=chromeRoot.Find("Tagline");
        if(logo!=null) logo.gameObject.SetActive(to==mainMenu);
        if(tagline!=null) tagline.gameObject.SetActive(to==mainMenu);
        transition = StartCoroutine(Transition(from, to, selection));
    }

    private static void AddPanelFlash(CanvasGroup panel)
    {
        var go = new GameObject("Panel Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var flash = go.GetComponent<Image>();
        flash.rectTransform.SetParent(panel.transform, false);
        SetVolumeRect(flash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
        flash.color = new Color(1f, .65f, .9f, 0f);
        flash.raycastTarget = false;
    }

    private IEnumerator Transition(CanvasGroup from, CanvasGroup to, Selectable selection)
    {
        Show(to, true);
        from.interactable = false;
        from.blocksRaycasts = false;
        to.interactable = false;
        Select(null);
        to.alpha = 0f;
        var flash = to.transform.Find("Panel Flash")?.GetComponent<Image>();
        const float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            from.alpha = 1f - t;
            to.alpha = t;
            if (flash != null)
                flash.color = new Color(1f, .65f, .9f, Mathf.Sin(t * Mathf.PI) * .3f);
            yield return null;
        }
        Show(from, false);
        if (flash != null) flash.color = new Color(1f, .65f, .9f, 0f);
        Show(to, true);
        Select(selection);
        transition = null;
    }

    private static void Show(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        group.gameObject.SetActive(visible);
    }
}
