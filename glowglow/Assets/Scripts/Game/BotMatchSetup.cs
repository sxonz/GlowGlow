using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Minimal bot setup. Rating is an AI difficulty setting, not a calibrated ladder score.</summary>
public sealed class BotMatchSetup : MonoBehaviour
{
    private TMP_FontAsset font;
    private bool ownsFont;
    private TMP_InputField ratingInput;
    private Slider ratingSlider;
    private TMP_Text feedback;
    private TMP_Text ratingTier;
    private Button startButton;
    private Action<int> start;
    private Action onClose;
    private static readonly Color Surface = new(.045f, .025f, .085f, 1);
    private static readonly Color Accent = new(.6f, .9f, 1f, 1);

    public static void Show(Transform parent, TMP_FontAsset font, Action<int> start, Action onClose)
    {
        if (FindFirstObjectByType<BotMatchSetup>() != null) return;
        var root = Rect(parent, "Bot Match Setup", Vector2.zero, Vector2.one);
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, .02f, .88f);
        var setup = root.gameObject.AddComponent<BotMatchSetup>();
        setup.font = font; setup.start = start; setup.onClose = onClose;
        if (font != null && font.sourceFontFile != null)
        {
            // Keep editable labels independent of the title's serialized dynamic atlas.
            setup.font = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            setup.font.name = "Bot Setup Font";
            setup.ownsFont = true;
        }
        setup.Build();
    }

    private void Build()
    {
        var panel = Rect(transform, "Panel", new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        panel.sizeDelta = new Vector2(640, 590);
        panel.gameObject.AddComponent<Image>().color = Surface;
        var border = panel.gameObject.AddComponent<Outline>(); border.effectColor = new Color(.6f, .25f, .6f); border.effectDistance = Vector2.one;
        Label(panel, "BOT MATCH  /  봇 대전", .07f, .87f, .94f, .97f, 32, Color.white);
        Label(panel, "상대 봇의 레이팅을 정하세요", .07f, .80f, .94f, .87f, 20, Accent);
        Label(panel, "봇 레이팅", .07f, .72f, .6f, .79f, 22, Color.white);
        ratingTier = Label(panel, "", .42f, .72f, .93f, .79f, 24, Accent);
        ratingTier.alignment = TextAlignmentOptions.MidlineRight;
        var inputRect = Rect(panel, "Rating", new Vector2(.07f, .56f), new Vector2(.93f, .71f));
        inputRect.gameObject.AddComponent<Image>().color = new Color(.1f, .08f, .16f);
        ratingInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect(inputRect, "Viewport", new Vector2(.04f, .05f), new Vector2(.96f, .95f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label(viewport, "", 0, 0, 1, 1, 32, Color.white);
        ratingInput.textViewport = viewport;
        ratingInput.textComponent = (TextMeshProUGUI)text;
        ratingInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        ratingInput.characterLimit = 4;
        ratingInput.lineType = TMP_InputField.LineType.SingleLine;
        ratingInput.onValueChanged.AddListener(_ => Validate());
        BuildRatingSlider(panel);
        int[] presets = { 600, 1200, 1800, 2400 };
        for (int i = 0; i < presets.Length; i++)
        {
            int value = presets[i];
            MakeButton(panel, TierName(value) + " " + value, .07f + i * .22f, .30f, .27f + i * .22f, .38f,
                () => ratingInput.text = value.ToString());
        }
        feedback = Label(panel, "", .07f, .21f, .93f, .29f, 19, Accent);
        Label(panel, "레이팅에 따라 반응·예측 강화 · 3000: 매 프레임 판단", .07f, .15f, .93f, .21f, 17, new Color(.7f, .67f, .78f));
        MakeButton(panel, "뒤로", .07f, .04f, .32f, .13f, Close);
        startButton = MakeButton(panel, "대전 시작", .36f, .04f, .93f, .13f, Begin);
        ratingInput.text = BotPlayerInput.DefaultRating.ToString();
        Validate();
        EventSystem.current?.SetSelectedGameObject(ratingInput.gameObject);
        ratingInput.ActivateInputField();
    }

    private bool TryRating(out int rating) => int.TryParse(ratingInput.text, out rating) &&
        rating >= BotPlayerInput.MinRating && rating <= BotPlayerInput.MaxRating;

    private static string TierName(int rating) => BotPlayerInput.RatingTier(rating) switch
    {
        6 => "특이점",
        5 => "블랙홀",
        4 => "초신성",
        3 => "항성",
        2 => "행성",
        1 => "유성",
        _ => "우주 먼지"
    };

    private void BuildRatingSlider(Transform panel)
    {
        var root = Rect(panel, "Rating Slider", new Vector2(.18f, .44f), new Vector2(.82f, .54f));
        var hitArea = root.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        var track = Rect(root, "Track", new Vector2(0, .43f), new Vector2(1, .57f));
        track.gameObject.AddComponent<Image>().color = new Color(.2f, .16f, .28f);
        var fill = Rect(track, "Fill", Vector2.zero, Vector2.one);
        fill.gameObject.AddComponent<Image>().color = Accent;
        var handleArea = Rect(root, "Handle Area", new Vector2(0, .18f), new Vector2(1, .82f));
        var handle = Rect(handleArea, "Handle", new Vector2(0, 0), new Vector2(0, 1));
        handle.sizeDelta = new Vector2(14, 0);
        var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
        ratingSlider = root.gameObject.AddComponent<Slider>();
        ratingSlider.minValue = BotPlayerInput.MinRating; ratingSlider.maxValue = BotPlayerInput.MaxRating;
        ratingSlider.wholeNumbers = true; ratingSlider.fillRect = fill; ratingSlider.handleRect = handle;
        ratingSlider.targetGraphic = handleImage;
        ratingSlider.onValueChanged.AddListener(value => ratingInput.text = Mathf.RoundToInt(value).ToString());
        MakeButton(panel, "−", .07f, .45f, .15f, .53f, () => StepRating(-1));
        MakeButton(panel, "+", .85f, .45f, .93f, .53f, () => StepRating(1));
        Label(panel, "200", .18f, .395f, .35f, .44f, 16, Accent);
        var maximum = Label(panel, "3000", .65f, .395f, .82f, .44f, 16, Accent);
        maximum.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void StepRating(int step)
    {
        int current = TryRating(out int value) ? value : Mathf.RoundToInt(ratingSlider.value);
        ratingInput.text = Mathf.Clamp(current + step, BotPlayerInput.MinRating, BotPlayerInput.MaxRating).ToString();
    }

    private void Validate()
    {
        if (feedback == null || startButton == null) return;
        bool valid = TryRating(out int rating);
        if (valid) ratingSlider.SetValueWithoutNotify(rating);
        startButton.interactable = valid;
        ratingTier.text = valid ? TierName(rating) : "—";
        feedback.color = valid ? Accent : new Color(1f, .5f, .6f);
        feedback.text = valid ? $"상대  BOT {rating}   ·   3회 피격 시 패배" :
            $"{BotPlayerInput.MinRating}~{BotPlayerInput.MaxRating} 사이의 정수를 입력하세요";
    }

    private void Begin()
    {
        if (!TryRating(out int rating)) return;
        Close();
        start(rating);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void Close()
    {
        gameObject.SetActive(false);
        onClose?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!ownsFont || font == null) return;
        foreach (var atlas in font.atlasTextures) if (atlas != null) Destroy(atlas);
        if (font.material != null) Destroy(font.material);
        Destroy(font);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private TMP_Text Label(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color color)
    {
        var text = Rect(parent, "Label", new Vector2(x0, y0), new Vector2(x1, y1)).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(Transform parent, string value, float x0, float y0, float x1, float y1, Action action)
    {
        var rect = Rect(parent, value, new Vector2(x0, y0), new Vector2(x1, y1));
        var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.16f, .1f, .23f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        button.onClick.AddListener(() => action());
        Label(rect, value, .03f, 0, .97f, 1, 19, Color.white).alignment = TextAlignmentOptions.Center;
        return button;
    }
}
