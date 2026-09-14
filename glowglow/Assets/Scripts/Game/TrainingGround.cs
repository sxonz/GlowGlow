using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-local sandbox. Catalog assets and saved deck selections are never modified.</summary>
public sealed class TrainingGround : MonoBehaviour
{
    private readonly List<WeaponRuntime> weapons = new();
    private readonly List<Button> weaponButtons = new();
    private readonly List<Action> refreshUpgrades = new();
    private WeaponUpgradeDefinition[] upgrades;
    private MatchController match;
    private TMP_FontAsset font;
    private RectTransform upgradeContent;
    private GridLayoutGroup weaponGrid;
    private TMP_Text selectedName, stats, telemetry, cooldownLabel;
    private Image cooldownFill;
    private GameObject overlay;
    private int selected;
    private bool movingTarget;
    private float targetPhase;
    private static readonly Color Panel = new(.035f, .025f, .075f, 1);
    private static readonly Color Card = new(.085f, .065f, .14f, 1);
    private static readonly Color Accent = new(.55f, .9f, 1f, 1);
    private static readonly Color Muted = new(.63f, .63f, .75f, 1);
    public WeaponRuntime Selected => weapons.Count > 0 ? weapons[selected] : null;

    public void Initialize(MatchController controller, DeckCatalog catalog)
    {
        match = controller;
        var seen = new HashSet<WeaponDefinition>();
        if (catalog != null && catalog.cards != null)
            foreach (var card in catalog.cards)
                if (card?.weapon != null && seen.Add(card.weapon)) weapons.Add(new WeaponRuntime(card.weapon));
        foreach (var definition in Resources.LoadAll<WeaponDefinition>(""))
            if (seen.Add(definition)) weapons.Add(new WeaponRuntime(definition));
        upgrades = Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades");
        Array.Sort(upgrades, (a, b) => a.displaySlot != b.displaySlot
            ? a.displaySlot.CompareTo(b.displaySlot) : string.CompareOrdinal(a.name, b.name));
        var hud = FindFirstObjectByType<ArenaHud>();
        font = hud != null ? hud.GetComponentInChildren<TMP_Text>(true)?.font : TMP_Settings.defaultFontAsset;
        if (hud != null) hud.gameObject.SetActive(false);
        foreach (var bar in FindObjectsByType<DrawnWeaponBar>(FindObjectsSortMode.None)) bar.gameObject.SetActive(false);
        var layout = Camera.main != null ? Camera.main.GetComponent<ArenaCameraLayout>() : null;
        if (layout != null) { layout.TrainingLayout = true; layout.Apply(); }
        Build();
        ResetArena();
        SelectWeapon(0);
    }

    public void ResetArena()
    {
        ResetLoadout(true);
    }

    private void ResetLoadout(bool resetPositions = false)
    {
        Vector2 playerPosition = resetPositions ? new Vector2(-5.8f, 0) : (Vector2)match.PlayerOne.transform.position;
        Vector2 targetPosition = resetPositions ? new Vector2(4.8f, 0) : (Vector2)match.PlayerTwo.transform.position;
        ProjectileBase.DespawnAll();
        match.PlayerOne.ResetCombatant(playerPosition);
        match.PlayerTwo.ResetCombatant(targetPosition);
        foreach (var runtime in weapons) runtime.ResetCooldown();
        match.PlayerOne.EquipTrainingWeapon(Selected);
        match.PlayerTwo.EquipTrainingWeapon(null);
        if (resetPositions) targetPhase = 0;
    }

    public void CycleWeapon(int direction) => SelectWeapon(selected + direction);

    public void SelectWeapon(int index)
    {
        if (weapons.Count == 0)
        {
            selectedName.text = "사용 가능한 탄막이 없습니다";
            ResetLoadout();
            return;
        }
        selected = (index % weapons.Count + weapons.Count) % weapons.Count;
        ResetLoadout();
        RebuildUpgrades();
        Refresh();
    }

    private void ChangeLevel(WeaponUpgradeDefinition upgrade, int level)
    {
        if (Selected == null || !upgrade.CanApplyTo(Selected.Definition)) return;
        level = Mathf.Clamp(level, 0, upgrade.maxLevel);
        int current = Selected.Upgrades.GetLevel(upgrade);
        // Incrementing preserves the runtime's acquisition order.
        if (level < current) Selected.Upgrades.Remove(upgrade);
        while (Selected.Upgrades.GetLevel(upgrade) < level)
            if (!Selected.TryUpgrade(upgrade)) break;
        ResetLoadout();
        Refresh();
    }

    private void SetAll(bool maximum)
    {
        if (Selected == null) return;
        Selected.Upgrades.Clear();
        if (maximum)
            foreach (var upgrade in upgrades)
                if (upgrade.CanApplyTo(Selected.Definition))
                    for (int i = 0; i < upgrade.maxLevel; i++) Selected.TryUpgrade(upgrade);
        ResetLoadout();
        Refresh();
    }

    private void Update()
    {
        if (match == null) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame) SelectWeapon(selected - 1);
            if (keyboard.eKey.wasPressedThisFrame) SelectWeapon(selected + 1);
            if (keyboard.rKey.wasPressedThisFrame) ResetArena();
        }
        if (movingTarget)
        {
            targetPhase += Time.deltaTime;
            var target = match.PlayerTwo.transform;
            var desired = new Vector2(4.8f, Mathf.Sin(targetPhase * 1.3f) * 3f);
            target.position = Vector2.MoveTowards(target.position, desired, Time.deltaTime * 3.9f);
        }
        telemetry.text = $"표적 명중  <color=#8FE6FF>{match.PlayerTwo.HitsTaken:000}</color>   /   제한 시간 없음";
        cooldownLabel.text = Selected == null ? "—" : Selected.CooldownRemaining > 0
            ? $"재사용까지 {Selected.CooldownRemaining:0.0}초" : "발사 준비 완료";
        cooldownFill.fillAmount = Selected == null ? 0 : 1 - Selected.CooldownFraction;
    }

    private void Refresh()
    {
        if (Selected == null) return;
        selectedName.text = Selected.Definition.displayName;
        selectedName.color = Selected.Definition.RarityColor;
        var value = Selected.Stats;
        string lifetime = float.IsPositiveInfinity(value.Lifetime) ? "제한 없음" : $"{value.Lifetime:0.##}초";
        stats.text = $"쿨타임  {value.Cooldown:0.##}초     탄속  {value.Speed:0.#}\n수명  {lifetime}     범위  {value.Range:0.#}";
        for (int i = 0; i < weaponButtons.Count; i++)
        {
            weaponButtons[i].GetComponent<Image>().color = i == selected ? new Color(.18f, .24f, .34f) : Card;
            weaponButtons[i].GetComponent<Outline>().enabled = i == selected;
        }
        foreach (var update in refreshUpgrades) update();
    }

    private void LateUpdate()
    {
        if (weaponGrid == null) return;
        float width = (((RectTransform)weaponGrid.transform).rect.width - 8) / 2;
        if (Mathf.Abs(weaponGrid.cellSize.x - width) > .1f)
            weaponGrid.cellSize = new Vector2(width, 43);
    }

    private void Build()
    {
        if (EventSystem.current == null)
            new GameObject("Training EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        overlay = new GameObject("Training Ground UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var root = (RectTransform)overlay.transform;
        var header = Box(root, "Header", .02f, .9f, .67f, .98f, Panel);
        Label(header, "TRAINING  /  훈련장", .025f, .48f, .8f, .94f, 28, Color.white);
        Label(header, "덱 제한 없이, 원하는 조합을 바로 테스트하세요", .025f, .05f, .98f, .45f, 18, Muted);
        var footer = Box(root, "Controls", .02f, .02f, .67f, .10f, Panel);
        Label(footer, "WASD 이동   ·   마우스 조준 / 발사   ·   SPACE 대시", .025f, .49f, .98f, .96f, 19, Color.white);
        Label(footer, "Q / E 또는 휠 전환   ·   R 전장 초기화   ·   ESC 나가기", .025f, .02f, .98f, .48f, 18, Muted);
        var side = Box(root, "Loadout", .70f, 0, 1, 1, Panel);
        Label(side, "탄막 설정", .055f, .935f, .72f, .98f, 28, Color.white);
        MakeButton(side, "나가기", .76f, .937f, .95f, .976f, () => SceneManager.LoadScene("Title"));
        Label(side, "선택 즉시 적용 · 탄막별 설정 유지", .055f, .903f, .95f, .933f, 17, Muted);
        var weaponContent = Scroll(side, "Weapons", .055f, .672f, .945f, .89f);
        var grid = weaponContent.gameObject.AddComponent<GridLayoutGroup>();
        weaponGrid = grid;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(193, 43);
        grid.spacing = new Vector2(8, 8);
        // Width follows the actual sidebar on wider aspect ratios.
        Canvas.ForceUpdateCanvases();
        grid.cellSize = new Vector2((weaponContent.rect.width - 8) / 2, 43);
        for (int i = 0; i < weapons.Count; i++)
        {
            int index = i;
            var definition = weapons[i].Definition;
            var button = MakeButton(weaponContent, definition.displayName, 0, 0, 1, 1, () => SelectWeapon(index));
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(1, -1);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.rectTransform.anchorMin = new Vector2(.24f, .06f);
            label.fontSize = 17;
            var icon = Box(button.transform, "Icon", .04f, .2f, .20f, .8f, definition.color).GetComponent<Image>();
            icon.sprite = definition.icon != null ? definition.icon : RuntimeShapes.Circle;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            weaponButtons.Add(button);
        }
        selectedName = Label(side, "", .055f, .62f, .94f, .663f, 26, Accent);
        stats = Label(side, "", .055f, .55f, .94f, .617f, 18, Muted);
        Label(side, "업그레이드", .055f, .502f, .5f, .54f, 21, Color.white);
        MakeButton(side, "기본", .55f, .504f, .73f, .54f, () => SetAll(false));
        MakeButton(side, "최대", .76f, .504f, .945f, .54f, () => SetAll(true));
        upgradeContent = Scroll(side, "Upgrades", .055f, .23f, .945f, .496f);
        var layout = upgradeContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = layout.childForceExpandWidth = true;
        var target = MakeButton(side, "표적: 고정", .055f, .18f, .49f, .22f, () => {});
        target.onClick.AddListener(() =>
        {
            movingTarget = !movingTarget;
            target.GetComponentInChildren<TMP_Text>().text = movingTarget ? "표적: 이동" : "표적: 고정";
            ResetArena();
        });
        MakeButton(side, "전장 초기화 · R", .51f, .18f, .945f, .22f, ResetArena);
        telemetry = Label(side, "", .055f, .13f, .945f, .17f, 18, Color.white);
        cooldownLabel = Label(side, "", .055f, .078f, .945f, .12f, 20, Accent);
        var track = Box(side, "Cooldown Track", .055f, .055f, .945f, .063f, Card);
        cooldownFill = Box(track, "Fill", 0, 0, 1, 1, Accent).GetComponent<Image>();
        cooldownFill.sprite = RuntimeShapes.Circle;
        cooldownFill.type = Image.Type.Filled;
        cooldownFill.fillMethod = Image.FillMethod.Horizontal;
        cooldownFill.fillOrigin = 0;
        Label(side, "위치는 유지되고 탄막·쿨타임이 초기화됩니다", .055f, .012f, .945f, .046f, 15, Muted);
    }

    private void RebuildUpgrades()
    {
        refreshUpgrades.Clear();
        foreach (Transform child in upgradeContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var upgrade in upgrades)
        {
            if (!upgrade.CanApplyTo(Selected.Definition)) continue;
            var row = Box(upgradeContent, upgrade.name, 0, 0, 1, 1, Card);
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            var title = Label(row, upgrade.displayName, .04f, .66f, .70f, .95f, 19, Color.white);
            var description = Label(row, upgrade.description, .04f, .08f, .72f, .64f, 16, Muted);
            float textWidth = Mathf.Max(240, upgradeContent.rect.width * .68f);
            rowLayout.preferredHeight = Mathf.Max(74, description.GetPreferredValues(upgrade.description, textWidth, Mathf.Infinity).y / .56f + 4);
            var levelText = Label(row, "", .75f, .62f, .97f, .93f, 17, Accent);
            levelText.alignment = TextAlignmentOptions.Center;
            var minus = MakeButton(row, "−", .75f, .15f, .85f, .5f,
                () => ChangeLevel(upgrade, Selected.Upgrades.GetLevel(upgrade) - 1));
            var plus = MakeButton(row, "+", .87f, .15f, .97f, .5f,
                () => ChangeLevel(upgrade, Selected.Upgrades.GetLevel(upgrade) + 1));
            refreshUpgrades.Add(() =>
            {
                int level = Selected.Upgrades.GetLevel(upgrade);
                levelText.text = $"{level} / {upgrade.maxLevel}";
                minus.interactable = level > 0;
                plus.interactable = level < upgrade.maxLevel;
                title.color = level > 0 ? Accent : Color.white;
            });
        }
        if (refreshUpgrades.Count == 0)
        {
            var empty = Label(upgradeContent, "이 탄막에는 업그레이드가 없습니다", 0, 0, 1, 1, 18, Muted);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
        }
        upgradeContent.anchoredPosition = Vector2.zero;
    }

    private RectTransform Scroll(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var viewport = Box(parent, name, x0, y0, x1, y1, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        var content = Rect(viewport, "Content", 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        var track = Box(parent, name + " Scrollbar", x1 + .008f, y0, x1 + .018f, y1, Card);
        var handle = Box(track, "Handle", 0, 0, 1, 1, Muted);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    private static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
    {
        var rect = Rect(parent, name, x0, y0, x1, y1);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private TMP_Text Label(Transform parent, string text, float x0, float y0, float x1, float y1, float size, Color color)
    {
        var label = Rect(parent, "Label", x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size - 2;
        label.fontSizeMax = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
    }

    private Button MakeButton(Transform parent, string text, float x0, float y0, float x1, float y1, Action action)
    {
        var rect = Box(parent, text, x0, y0, x1, y1, Card);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor = new Color(.65f, .9f, 1);
        colors.disabledColor = new Color(.5f, .5f, .5f, .5f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => action());
        Label(rect, text, .04f, .06f, .96f, .94f, 18, Color.white).alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void OnDestroy() { if (overlay != null) Destroy(overlay); }
}
