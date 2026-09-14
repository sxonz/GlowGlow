using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Three sequential one-of-three choices, with eight seconds for each choice.</summary>
public sealed class OpeningDraftScreen : MonoBehaviour
{
    private const float SelectionSeconds = 8f;
    private const float FlipSeconds = .6f;
    private const float DealInterval = .18f;
    private GameObject overlay;
    private OpeningDraft draft;
    private Action<OpeningDraft> onComplete;
    private float shownAt;
    private float deadline;
    private TMP_FontAsset font;
    private TMP_Text countdown;
    private TMP_Text selectionLabel;
    private Button confirm;
    private Image timerFill;
    private GameObject botSummary;
    private TMP_Text botSummaryTitle;
    private readonly CanvasGroup[] botPicks = new CanvasGroup[OpeningDraft.RoundCount];
    private float botShownAt;
    private int summaryRating;
    private readonly RectTransform[] cards = new RectTransform[OpeningDraft.OfferCount];
    private readonly Button[] buttons = new Button[OpeningDraft.OfferCount];
    private readonly Image[] backgrounds = new Image[OpeningDraft.OfferCount];
    private readonly CanvasGroup[] faces = new CanvasGroup[OpeningDraft.OfferCount];
    private readonly TMP_Text[] backs = new TMP_Text[OpeningDraft.OfferCount];
    private readonly TMP_Text[] badges = new TMP_Text[OpeningDraft.OfferCount];
    private readonly UpgradeShardDisplay[] upgradeDisplays = new UpgradeShardDisplay[OpeningDraft.OfferCount];

    public void Show(OpeningDraft value, Action<OpeningDraft> complete,
        IReadOnlyList<OpeningDraft.Offer> botChoices = null, int botRating = 0)
    {
        draft = value;
        onComplete = complete;
        if (overlay == null) Build();
        overlay.SetActive(true);
        ShowBotChoices(botChoices, botRating);
        ShowRound();
    }

    public void Hide()
    {
        draft = null;
        onComplete = null;
        if (overlay != null) overlay.SetActive(false);
    }

    private void ShowRound()
    {
        overlay.transform.Find("Title").GetComponent<TMP_Text>().text = $"시작 선택 {draft.RoundNumber} / 3  ·  하나를 선택하세요";
        overlay.transform.Find("Subtitle").GetComponent<TMP_Text>().text = draft.RoundNumber == 1
            ? "첫 번째 선택은 신규 탄막입니다" : "신규 탄막을 획득하거나 보유 탄막을 강화하세요";
        confirm.GetComponentInChildren<TMP_Text>().text = draft.RoundNumber == 3
            ? (GlowGlow.Online.OnlineSession.Current?.InArena == true ? "선택 확정 · 준비 완료" : "선택 확정 · 전투 시작")
            : "선택 확정 · 다음 라운드";
        shownAt = Time.unscaledTime;
        deadline = shownAt + FlipSeconds + DealInterval * (cards.Length - 1) + SelectionSeconds;
        for (int i = 0; i < cards.Length; i++)
        {
            var offer = draft.Offers[i];
            var weapon = offer.Weapon;
            var face = faces[i].transform;
            face.Find("Name").GetComponent<TMP_Text>().text = offer.IsUpgrade ? offer.Upgrade.displayName : weapon.displayName;
            face.Find("Name").GetComponent<TMP_Text>().color = weapon.RarityColor;
            face.Find("Rarity").GetComponent<TMP_Text>().text = offer.IsUpgrade ? "보유 탄막 강화" : weapon.RarityTag + " · 신규 탄막";
            face.Find("Stats").GetComponent<TMP_Text>().text = offer.IsUpgrade
                ? $"{weapon.displayName} · Lv.{offer.Target.Upgrades.GetLevel(offer.Upgrade) + 1}\n{offer.Upgrade.description}"
                : $"탄속  {weapon.projectileSpeed:0.#}\n쿨타임  {weapon.cooldown:0.#}초";
            var icon = face.Find("Icon").GetComponent<Image>();
            icon.gameObject.SetActive(!offer.IsUpgrade);
            upgradeDisplays[i].gameObject.SetActive(offer.IsUpgrade);
            upgradeDisplays[i].Show(offer.Target, draft.UpgradeDefinitions, offer.IsUpgrade ? offer.Upgrade : null);
            icon.sprite = offer.IsUpgrade && offer.Upgrade.icon != null ? offer.Upgrade.icon :
                weapon.icon != null ? weapon.icon : RuntimeShapes.Circle;
            icon.color = weapon.color;
        }
        Refresh();
        Update();
    }

    private void Update()
    {
        if (draft == null || overlay == null || !overlay.activeSelf) return;
        float now = Time.unscaledTime;
        UpdateBotSummary(now);
        bool ready = now >= deadline - SelectionSeconds;
        for (int i = 0; i < cards.Length; i++)
        {
            float progress = Mathf.Clamp01((now - shownAt - i * DealInterval) / FlipSeconds);
            float eased = Mathf.SmoothStep(0, 1, progress);
            cards[i].anchoredPosition = new Vector2(Mathf.Lerp(0, (i - 1) * 350, eased), Mathf.Lerp(-100, -10, eased));
            cards[i].localScale = new Vector3(Mathf.Max(.03f, Mathf.Abs(Mathf.Cos(progress * Mathf.PI))), 1, 1);
            cards[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp((i - 1) * 12, 0, eased));
            faces[i].alpha = progress >= .5f ? 1 : 0;
            backs[i].enabled = progress < .5f;
            buttons[i].interactable = ready;
        }
        float remaining = Mathf.Clamp(deadline - now, 0, SelectionSeconds);
        countdown.text = ready ? $"{Mathf.CeilToInt(remaining):00}초" : "카드를 펼치는 중";
        countdown.color = ready && remaining <= 5 ? new Color(1, .45f, .6f) : Color.white;
        timerFill.fillAmount = remaining / SelectionSeconds;
        confirm.interactable = ready && draft.SelectedIndex >= 0;
        if (!ready) return;
        if (remaining <= 0) { FinishRound(true); return; }
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) Toggle(0);
        if (keyboard.digit2Key.wasPressedThisFrame) Toggle(1);
        if (keyboard.digit3Key.wasPressedThisFrame) Toggle(2);
        if (keyboard.enterKey.wasPressedThisFrame && draft.SelectedIndex >= 0) FinishRound();
    }

    private void Toggle(int index)
    {
        if (draft == null || Time.unscaledTime < deadline - SelectionSeconds || Time.unscaledTime >= deadline) return;
        draft.Select(index);
        Refresh();
    }

    private void Refresh()
    {
        string owned = string.Join(" · ", System.Linq.Enumerable.Select(draft.Acquired,
            weapon => weapon.Definition.displayName + (weapon.Upgrades.Count > 0 ? " (강화)" : "")));
        selectionLabel.text = string.IsNullOrEmpty(owned) ? "클릭 또는 숫자 1–3으로 선택 · ENTER로 확정" : $"보유 탄막: {owned}";
        for (int i = 0; i < cards.Length; i++)
        {
            bool selected = draft.SelectedIndex == i;
            backgrounds[i].color = selected ? new Color(.32f, .08f, .31f) : new Color(.085f, .045f, .16f);
            cards[i].GetComponent<Outline>().effectColor = selected ? new Color(1, .35f, .8f) : new Color(.35f, .25f, .55f);
            badges[i].text = selected ? "선택됨 · 확정 대기" : $"[ {i + 1} ]  선택";
        }
    }

    private void FinishRound(bool autoPick = false)
    {
        if (draft == null || Time.unscaledTime < deadline - SelectionSeconds) return;
        if (!draft.Confirm(autoPick)) return;
        if (!draft.IsComplete) { ShowRound(); return; }
        var completed = draft;
        draft = null;
        overlay.SetActive(false);
        var callback = onComplete;
        onComplete = null;
        callback?.Invoke(completed);
    }

    private void Build()
    {
        var hud = FindFirstObjectByType<ArenaHud>();
        font = hud != null ? hud.GetComponentInChildren<TMP_Text>(true)?.font : TMP_Settings.defaultFontAsset;
        if (EventSystem.current == null)
            new GameObject("Draft EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        overlay = new GameObject("Opening Draft", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var shade = Rect(overlay.transform, "Backdrop", Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.sizeDelta = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(.018f, .008f, .05f, .97f);
        Text(overlay.transform, "Title", "", new Vector2(0, 370), new Vector2(1500, 80), 46);
        Text(overlay.transform, "Subtitle", "", new Vector2(0, 310), new Vector2(1200, 50), 25);
        countdown = Text(overlay.transform, "Countdown", "8초", new Vector2(0, 250), new Vector2(700, 60), 34);
        var track = Rect(overlay.transform, "Timer Track", new Vector2(0, 210), new Vector2(1470, 5));
        track.gameObject.AddComponent<Image>().color = new Color(.18f, .1f, .25f);
        var fill = Rect(track, "Fill", Vector2.zero, new Vector2(1470, 5));
        timerFill = fill.gameObject.AddComponent<Image>();
        timerFill.sprite = RuntimeShapes.Square;
        timerFill.color = new Color(1, .3f, .8f);
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i] = Rect(overlay.transform, $"Card {i + 1}", new Vector2((i - 1) * 350, -10), new Vector2(300, 430));
            backgrounds[i] = card.gameObject.AddComponent<Image>();
            card.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            var button = buttons[i] = card.gameObject.AddComponent<Button>();
            button.targetGraphic = backgrounds[i];
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() => Toggle(index));
            backs[i] = Text(card, "Back", "GLOW\nGLOW", Vector2.zero, new Vector2(250, 220), 44);
            var face = Rect(card, "Face", Vector2.zero, new Vector2(280, 430));
            Text(face, "Rarity", "", new Vector2(0, 185), new Vector2(260, 35), 20);
            faces[i] = face.gameObject.AddComponent<CanvasGroup>();
            faces[i].blocksRaycasts = false;
            var icon = Rect(face, "Icon", new Vector2(0, 112), new Vector2(90, 90)).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var shardRoot = Rect(face, "Upgrade Shards", new Vector2(0, 104), new Vector2(128, 128));
            upgradeDisplays[i] = shardRoot.gameObject.AddComponent<UpgradeShardDisplay>();
            Text(face, "Name", "", new Vector2(0, 20), new Vector2(260, 44), 26);
            Text(face, "Stats", "", new Vector2(0, -75), new Vector2(260, 132), 20);
            badges[i] = Text(face, "Selection", "", new Vector2(0, -185), new Vector2(260, 32), 21);
        }
        selectionLabel = Text(overlay.transform, "Selection Count", "", new Vector2(0, -265), new Vector2(1500, 50), 25);
        var confirmRect = Rect(overlay.transform, "Confirm", new Vector2(0, -315), new Vector2(440, 70));
        var confirmImage = confirmRect.gameObject.AddComponent<Image>();
        confirmImage.color = new Color(.65f, .1f, .48f);
        confirm = confirmRect.gameObject.AddComponent<Button>();
        confirm.targetGraphic = confirmImage;
        confirm.navigation = new Navigation { mode = Navigation.Mode.None };
        confirm.onClick.AddListener(() => FinishRound());
        Text(confirmRect, "Label", "선택 확정 · 전투 시작", Vector2.zero, new Vector2(420, 60), 27);
        Text(overlay.transform, "Hint", "숫자 1–3 / 클릭으로 선택 · ENTER로 확정 · 매 라운드 8초, 미선택 시 자동 선택", new Vector2(0, -390), new Vector2(1600, 50), 22);
    }

    private void ShowBotChoices(IReadOnlyList<OpeningDraft.Offer> choices, int rating)
    {
        if (choices == null || choices.Count == 0)
        {
            if (botSummary != null) botSummary.SetActive(false);
            return;
        }
        if (botSummary == null)
        {
            botSummary = Rect(overlay.transform, "Bot Selection Summary", new Vector2(0, -475), new Vector2(1470, 104)).gameObject;
            botSummaryTitle = Text(botSummary.transform, "Title", "", new Vector2(0, 39), new Vector2(1470, 28), 18);
            botSummaryTitle.color = new Color(.65f, .8f, 1);
            for (int i = 0; i < botPicks.Length; i++)
            {
                var tile = Rect(botSummary.transform, "Bot Pick " + (i + 1), new Vector2((i - 1) * 490, -9), new Vector2(474, 62));
                var background = tile.gameObject.AddComponent<Image>();
                background.color = new Color(.075f, .055f, .13f);
                background.raycastTarget = false;
                botPicks[i] = tile.gameObject.AddComponent<CanvasGroup>();
                botPicks[i].interactable = botPicks[i].blocksRaycasts = false;
                var icon = Rect(tile, "Icon", new Vector2(-197, 0), new Vector2(42, 42)).gameObject.AddComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false;
                Text(tile, "Name", "", new Vector2(27, 12), new Vector2(360, 27), 21).alignment = TextAlignmentOptions.MidlineLeft;
                Text(tile, "Detail", "", new Vector2(27, -15), new Vector2(360, 23), 17).alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
        botSummary.SetActive(true);
        botShownAt = Time.unscaledTime;
        summaryRating = rating;
        for (int i = 0; i < botPicks.Length; i++)
        {
            var tile = botPicks[i];
            tile.gameObject.SetActive(i < choices.Count);
            tile.alpha = 0;
            if (i >= choices.Count) continue;
            var pick = choices[i];
            var icon = tile.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = pick.IsUpgrade && pick.Upgrade.icon != null ? pick.Upgrade.icon : pick.Weapon.icon;
            if (icon.sprite == null) icon.sprite = RuntimeShapes.Circle;
            icon.color = pick.Weapon.color;
            tile.transform.Find("Name").GetComponent<TMP_Text>().text = pick.IsUpgrade ? pick.Upgrade.displayName : pick.Weapon.displayName;
            var detail = tile.transform.Find("Detail").GetComponent<TMP_Text>();
            detail.text = pick.IsUpgrade ? $"{i + 1:00}  강화 · {pick.Weapon.displayName}" : $"{i + 1:00}  신규 탄막";
            detail.color = new Color(.67f, .65f, .78f);
        }
    }

    private void UpdateBotSummary(float now)
    {
        if (botSummary == null || !botSummary.activeSelf) return;
        int revealed = 0;
        foreach (var tile in botPicks)
        {
            tile.alpha = Mathf.Clamp01((now - botShownAt - revealed * .22f) / .16f);
            revealed++;
        }
        botSummaryTitle.text = $"BOT {summaryRating}  ·  " + (now - botShownAt < .6f ? "선택 중…" : "선택 완료") + "  /  선택 요약";
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .75f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
