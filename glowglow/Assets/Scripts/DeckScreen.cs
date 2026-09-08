using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeckScreen : MonoBehaviour
{
    private const string SaveKey = DeckCatalog.SaveKey;
    [Serializable] private sealed class SavedDeck { public List<string> cards = new List<string>(); }
    private readonly List<DeckCatalog.Card> cards = new List<DeckCatalog.Card>();
    private SavedDeck saved = new SavedDeck();
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TMP_Text details;
    [SerializeField] private TMP_Text deckCount;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text pageLabel;
    [SerializeField] private RectTransform grid;
    [SerializeField] private RectTransform deckList;
    [SerializeField] private Button toggle;
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [SerializeField] private Button backButton;
    [SerializeField] private Button starterButton;
    [SerializeField] private TMP_Text deckRequirement;
    [SerializeField] private List<Button> collectionSlots = new List<Button>();
    [SerializeField] private List<Button> deckSlots = new List<Button>();
    [SerializeField] private TMP_Text emptyCollection;
    [SerializeField] private TMP_Text emptyDeck;
    [SerializeField] private BarragePreview preview;
    public bool HasPreview => preview != null;
    private DeckCatalog.Card selected;
    private int page;
    private DeckCatalog activeCatalog;
    public int DeckSlotCapacity => deckSlots.Count;
    public Button BackButton => backButton;
    private static readonly Color Pink = new Color(1f, .25f, .8f);

    private void LoadCards(DeckCatalog catalog, bool loadSaved)
    {
        cards.Clear();
        activeCatalog = catalog;
        if (loadSaved && catalog != null) catalog.EnsureStarterDeck();
        var ids = new HashSet<string>();
        if (catalog != null)
            foreach (var card in catalog.cards)
                if (card != null && card.weapon != null && !string.IsNullOrWhiteSpace(card.id) && ids.Add(card.id)) cards.Add(card);
        try { saved = loadSaved ? JsonUtility.FromJson<SavedDeck>(PlayerPrefs.GetString(SaveKey, "{}")) ?? new SavedDeck() : new SavedDeck(); }
        catch (ArgumentException) { saved = new SavedDeck(); }
        if (saved.cards == null) saved.cards = new List<string>();
        var unique = new HashSet<string>();
        saved.cards.RemoveAll(id => !ids.Contains(id) || !unique.Add(id));
    }

    public void Initialize(DeckCatalog catalog, Action close)
    {
        LoadCards(catalog, true);
        previous.onClick.AddListener(() => { page--; RefreshCollection(); });
        next.onClick.AddListener(() => { page++; RefreshCollection(); });
        toggle.onClick.AddListener(ToggleSelected);
        backButton.onClick.AddListener(() => close());
        starterButton.onClick.AddListener(FillStarterDeck);
        RefreshCollection();
        RefreshDeck();
        SelectCard(cards.Count > 0 ? cards[0] : null);
    }

    public void Build(DeckCatalog catalog, TMP_FontAsset titleFont, Action close)
    {
        font = titleFont;
        LoadCards(catalog, false);

        Text(transform, "덱 구성 <size=55%> / 카드 도감</size>", 38, .035f, .88f, .8f, .97f);
        Text(transform, "카드를 살펴보고 나만의 덱을 구성하세요.", 18, .035f, .82f, .8f, .88f);
        var collection = Box(transform, "Collection", .025f, .13f, .37f, .80f);
        Text(collection, $"무기 도감  ·  {cards.Count}종", 24, .05f, .88f, .95f, .98f);
        grid = Rect(collection, "Cards", .05f, .15f, .95f, .86f);
        previous = Button(collection, "이전", .05f, .025f, .27f, .12f, () => { page--; RefreshCollection(); });
        next = Button(collection, "다음", .73f, .025f, .95f, .12f, () => { page++; RefreshCollection(); });
        pageLabel = Text(collection, "", 18, .3f, .025f, .7f, .12f);
        pageLabel.alignment = TextAlignmentOptions.Center;

        var detail = Box(transform, "Details", .385f, .13f, .75f, .80f);
        var previewRect = Box(detail, "Barrage Preview", .07f, .53f, .93f, .94f);
        var previewImage = Rect(previewRect, "Live View", .01f, .01f, .99f, .99f).gameObject.AddComponent<RawImage>();
        previewImage.raycastTarget = false;
        previewImage.color = new Color(.025f, .012f, .07f);
        preview = previewRect.gameObject.AddComponent<BarragePreview>();
        preview.Configure(previewImage);
        details = Text(detail, "카드를 선택하면\n설명과 능력치를 볼 수 있습니다.", 22, .07f, .19f, .93f, .48f);
        details.enableAutoSizing = false;
        details.textWrappingMode = TextWrappingModes.Normal;
        details.alignment = TextAlignmentOptions.TopLeft;
        toggle = Button(detail, "덱에 추가", .07f, .04f, .93f, .15f, ToggleSelected);
        actionLabel = toggle.GetComponentInChildren<TMP_Text>();

        var deck = Box(transform, "My Deck", .765f, .13f, .975f, .80f);
        deckCount = Text(deck, "", 24, .07f, .88f, .93f, .98f);
        deckRequirement = Text(deck, "8장을 구성해야 플레이할 수 있습니다", 15, .07f, .81f, .93f, .88f);
        starterButton = Button(deck, "기본 8장으로 채우기", .07f, .70f, .93f, .79f, null);
        var viewport = Rect(deck, "Viewport", .07f, .05f, .93f, .68f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, .01f);
        deckList = Rect(viewport, "Deck Cards", 0, 1, 1, 1);
        deckList.pivot = new Vector2(.5f, 1);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = deckList;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        backButton = Button(transform, "BACK · 메인 메뉴", .035f, .035f, .23f, .10f, close);
        Text(transform, "카드 선택 → 상세 확인 → 덱에 추가 / 제거", 17, .27f, .035f, .95f, .10f);
        BuildSlots();
        RefreshCollection();
        RefreshDeck();
        SelectCard(cards.Count > 0 ? cards[0] : null);
    }

    private void SelectCard(DeckCatalog.Card card)
    {
        selected = card;
        if (preview != null) preview.Select(card?.weapon);
        toggle.interactable = card != null;
        if (card == null) return;
        var weapon = card.weapon;
        var stats = weapon.ResolveStats(null);
        details.text = $"<color=#FF80DD><size=120%>{weapon.displayName}</size></color>\n" +
            $"{card.description}\n\n쿨타임 {stats.Cooldown:0.##}초  ·  최대 강화 +{weapon.maxUpgradeLevel}";
        bool included = saved.cards.Contains(card.id);
        toggle.interactable = included || saved.cards.Count < DeckCatalog.RequiredDeckSize;
        actionLabel.text = included ? "덱에서 제거" : saved.cards.Count >= DeckCatalog.RequiredDeckSize ? "덱이 가득 찼습니다 (8/8)" : "덱에 추가";
    }

    private void ToggleSelected()
    {
        if (selected == null) return;
        if (!saved.cards.Remove(selected.id))
        {
            if (saved.cards.Count >= DeckCatalog.RequiredDeckSize) return;
            saved.cards.Add(selected.id);
        }
        SaveAndRefresh();
    }

    private void FillStarterDeck()
    {
        if (activeCatalog == null) return;
        saved.cards = activeCatalog.StarterCardIds();
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
        PlayerPrefs.Save();
        SelectCard(selected);
        RefreshCollection();
        RefreshDeck();
    }

    private void RefreshCollection()
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(cards.Count / 6f));
        page = Mathf.Clamp(page, 0, pages - 1);
        previous.interactable = page > 0;
        next.interactable = page < pages - 1;
        pageLabel.text = $"{page + 1} / {pages}";
        emptyCollection.gameObject.SetActive(cards.Count == 0);
        for (int cell = 0; cell < collectionSlots.Count; cell++)
        {
            int i = page * 6 + cell;
            var button = collectionSlots[cell];
            button.gameObject.SetActive(i < cards.Count);
            if (i >= cards.Count) continue;
            var card = cards[i];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCard(card));
            button.transform.Find("Card Name").GetComponent<TMP_Text>().text = card.weapon.displayName;
            button.transform.Find("Card Status").GetComponent<TMP_Text>().text = saved.cards.Contains(card.id) ? "덱에 포함됨" : "무기 · 상세 보기";
            foreach (var image in button.transform.Find("Bullet Preview").GetComponentsInChildren<Image>())
            {
                image.color = card.weapon.color;
                image.sprite = card.weapon.icon;
                image.preserveAspect = true;
            }
        }
    }

    private void RefreshDeck()
    {
        deckCount.text = $"내 덱  ·  {saved.cards.Count}/8";
        bool ready = activeCatalog != null && activeCatalog.IsDeckValid(JsonUtility.ToJson(saved));
        deckRequirement.text = ready ? "플레이 준비 완료 · 자동 저장" : "플레이하려면 정확히 8장이 필요합니다";
        deckRequirement.color = ready ? new Color(.65f, 1f, .8f) : new Color(1f, .65f, .85f);
        deckList.sizeDelta = new Vector2(0, Mathf.Max(100, saved.cards.Count * 66));
        emptyDeck.gameObject.SetActive(saved.cards.Count == 0);
        for (int i = 0; i < deckSlots.Count; i++)
        {
            var row = deckSlots[i];
            row.gameObject.SetActive(i < saved.cards.Count);
            if (i >= saved.cards.Count) continue;
            var card = cards.Find(candidate => candidate.id == saved.cards[i]);
            row.GetComponentInChildren<TMP_Text>().text = $"{i + 1:00}  {card.weapon.displayName}";
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => SelectCard(card));
        }
    }

    private void BuildSlots()
    {
        emptyCollection = Text(grid, "등록된 카드가 없습니다.", 22, 0, .4f, 1, .6f);
        emptyDeck = Text(deckList, "아직 비어 있습니다.\n도감에서 카드를 추가하세요.", 18, 0, 0, 1, 1);
        for (int cell = 0; cell < 6; cell++)
        {
            float x = (cell % 2) * .52f;
            float y = 1 - (cell / 2) * .34f;
            var button = Button(grid, "", x, y - .30f, x + .48f, y, null);
            button.name = $"Card Slot {cell + 1}";
            Text(button.transform, "", 22, .07f, .26f, .93f, .55f).name = "Card Name";
            Text(button.transform, "", 15, .07f, .04f, .93f, .25f).name = "Card Status";
            var art = Rect(button.transform, "Bullet Preview", .08f, .61f, .92f, .90f);
            var icon = Rect(art, "Weapon Icon", .5f, .5f, .5f, .5f);
            icon.sizeDelta = new Vector2(48, 48);
            icon.gameObject.AddComponent<Image>().raycastTarget = false;
            collectionSlots.Add(button);
        }
        for (int i = 0; i < DeckCatalog.RequiredDeckSize; i++)
        {
            var row = Button(deckList, "", 0, 1, 1, 1, null);
            row.name = $"Deck Slot {i + 1}";
            var rect = (RectTransform)row.transform;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(0, 56);
            rect.anchoredPosition = new Vector2(0, -i * 66);
            deckSlots.Add(row);
        }
    }

#if UNITY_EDITOR
    public void RebuildForEditor(DeckCatalog catalog)
    {
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
        collectionSlots.Clear();
        deckSlots.Clear();
        page = 0;
        Build(catalog, font, null);
    }
#endif

    private static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var rect = Rect(parent, name, x0, y0, x1, y1);
        rect.gameObject.AddComponent<Image>().color = new Color(.055f, .025f, .14f, .98f);
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.6f, .3f, .7f, .55f);
        outline.effectDistance = new Vector2(1, -1);
        return rect;
    }

    private TMP_Text Text(Transform parent, string value, int size, float x0, float y0, float x1, float y1)
    {
        var text = Rect(parent, "Text", x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .8f;
        text.fontSizeMax = size;
        text.color = new Color(1, .94f, 1);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private Button Button(Transform parent, string label, float x0, float y0, float x1, float y1, Action action)
    {
        var rect = Box(parent, label, x0, y0, x1, y1);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = Pink;
        colors.selectedColor = new Color(1f, .65f, .9f);
        button.colors = colors;
        // Scene construction stores visuals only; Initialize wires runtime actions.
        var text = Text(rect, label, 20, .06f, .04f, .94f, .96f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
