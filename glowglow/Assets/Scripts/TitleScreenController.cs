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

    private void Start()
    {
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
    }

#if UNITY_EDITOR
    // Called only by the editor builder. Runtime uses these serialized scene objects.
    public void BakeSceneUI()
    {
        if (deckPanel != null)
        {
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
        if (!RequireCompleteDeck(mainMenu)) return;
        SetStatus("SINGLEPLAYER  ·  신규 모드 준비 중");
        BeginTransition(mainMenu, soloPanel, soloBackButton);
    }

    public void OpenDeck()
    {
        SetStatus("DECK  ·  카드 도감 / 덱 구성");
        BeginTransition(mainMenu, deckPanel, deckScreen.BackButton);
    }

    private void CreateDeckScreen()
    {
        deckButton = Instantiate(firstButton, mainMenu.transform);
        deckButton.name = "DECK";
        deckButton.transform.SetSiblingIndex(2);
        deckButton.onClick = new Button.ButtonClickedEvent();
        deckButton.GetComponent<LayoutElement>().flexibleHeight = 1f;
        SetLabels(deckButton, "덱 구성", "카드 도감 · 내 덱 편집");
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
        SetStatus("CLASSIC  ·  LOADING");
        SceneManager.LoadScene("Arena");
    }

    private bool RequireCompleteDeck(CanvasGroup from)
    {
        if (deckCatalog != null && deckCatalog.IsSavedDeckValid()) return true;
        SetStatus("덱에 정확히 8장을 구성한 뒤 플레이하세요");
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
        var hint = VolumeText(label, "Volume Hint", new Vector2(.07f, .28f), new Vector2(.93f, .35f));
        hint.fontSize = 14;
        hint.color = new Color(.85f, .76f, .93f);
        hint.text = "클릭 / 드래그로 조절 · 방향키 ← →";
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
        classicButton = CreateModeButton(multiplayerPanel, "클래식", "CLASSIC · 두 플레이어 대전");
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
        var button = Instantiate(firstButton, panel.transform.Find("Content"));
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
        button.transform.Find("Label").GetComponent<TMP_Text>().text = label;
        button.transform.Find("Sub Label").GetComponent<TMP_Text>().text = subtitle;
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
