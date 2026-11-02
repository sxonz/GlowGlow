using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Bot difficulty setup. Rating is an AI difficulty setting, not a calibrated ladder score.</summary>
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
        panel.sizeDelta = new Vector2(1020, 660);
        NeonUI.Panel(panel,NeonUI.Cyan);
        Label(panel,"SINGLEPLAYER / DIFFICULTY",.06f,.88f,.47f,.95f,15,NeonUI.Cyan);
        Label(panel, "BOT MATCH", .06f, .74f, .47f, .87f, 42, Color.white);
        Label(panel, "반응 속도와 회피 판단을\n원하는 난도로 조절하세요.", .06f, .17f, .47f, .32f, 21, NeonUI.Muted);
        NeonUI.Art(panel,"Bot Orbit","Orbit Seal",new Color(.34f,.87f,1,.18f),.08f,.31f,.43f,.73f).preserveAspect=true;
        NeonUI.Emblem(panel,"Bot",NeonUI.Cyan,.16f,.40f,.35f,.64f);
        Label(panel, "상대 봇 레이팅", .52f, .84f, .94f, .93f, 26, Color.white);
        ratingTier = Label(panel, "", .52f, .75f, .94f, .81f, 22, Accent);
        ratingTier.alignment = TextAlignmentOptions.MidlineRight;
        var inputRect = Rect(panel, "Rating", new Vector2(.52f, .60f), new Vector2(.94f, .74f));
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
        NeonUI.Input(ratingInput,NeonUI.Cyan);
        ratingInput.onValueChanged.AddListener(_ => Validate());
        BuildRatingSlider(panel);
        int[] presets = { 600, 1200, 1800, 2400 };
        for (int i = 0; i < presets.Length; i++)
        {
            int value = presets[i];
            float x=.52f+(i%2)*.22f, y=.30f-(i/2)*.095f;
            MakeButton(panel, TierName(value) + " " + value, x,y,x+.20f,y+.077f,
                () => ratingInput.text = value.ToString());
        }
        feedback = Label(panel, "", .52f, .12f, .94f, .19f, 17, Accent);
        MakeButton(panel, "BACK", .06f, .05f, .30f, .13f, Close);
        startButton = MakeButton(panel, "대전 시작", .52f, .035f, .94f, .115f, Begin);
        NeonUI.Button(startButton,NeonUI.Cyan,true);
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
        var root = Rect(panel, "Rating Slider", new Vector2(.59f, .47f), new Vector2(.87f, .55f));
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
        NeonUI.Slider(ratingSlider,NeonUI.Cyan);
        ratingSlider.onValueChanged.AddListener(value => ratingInput.text = Mathf.RoundToInt(value).ToString());
        MakeButton(panel, "−", .52f, .47f, .58f, .55f, () => StepRating(-1));
        MakeButton(panel, "+", .88f, .47f, .94f, .55f, () => StepRating(1));
        Label(panel, "200", .59f, .415f, .72f, .465f, 16, Accent);
        var maximum = Label(panel, "3000", .74f, .415f, .87f, .465f, 16, Accent);
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
        feedback.text = valid ? $"상대  BOT {rating}" :
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
        text.enableAutoSizing=true; text.fontSizeMin=size*.8f; text.fontSizeMax=size;
        return text;
    }

    private Button MakeButton(Transform parent, string value, float x0, float y0, float x1, float y1, Action action)
    {
        var rect = Rect(parent, value, new Vector2(x0, y0), new Vector2(x1, y1));
        var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.16f, .1f, .23f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        NeonUI.Button(button,NeonUI.Cyan);
        button.onClick.AddListener(() => action());
        Label(rect, value, .03f, 0, .97f, 1, 19, Color.white).alignment = TextAlignmentOptions.Center;
        return button;
    }
}
