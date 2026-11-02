using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared, prepared UI artwork. Decoration is built once; interaction uses Unity's existing tint transitions.</summary>
public static class NeonUI
{
    public static readonly Color Ink = new(.018f, .022f, .047f, 1);
    public static readonly Color Surface = new(.038f, .043f, .078f, 1);
    public static readonly Color Raised = new(.067f, .065f, .12f, 1);
    public static readonly Color White = new(.96f, .97f, 1);
    public static readonly Color Muted = new(.58f, .63f, .76f);
    public static readonly Color Pink = new(1, .32f, .76f);
    public static readonly Color Cyan = new(.34f, .87f, 1);
    public static readonly Color Violet = new(.69f, .49f, 1);
    private static Dictionary<string, Sprite> sprites;

    public static Sprite Asset(string name)
    {
        if (sprites == null)
        {
            sprites = new Dictionary<string, Sprite>(32);
            foreach (var sprite in Resources.LoadAll<Sprite>("UI/NeonUIAtlas")) sprites.Add(sprite.name, sprite);
        }
        if (sprites.TryGetValue(name, out var asset)) return asset;
        throw new System.InvalidOperationException("Missing prepared UI artwork: " + name + ". Run GlowGlow/Bake UI Theme in the editor.");
    }

    public static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var existing = parent.Find(name);
        var rect = existing != null ? (RectTransform)existing : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (existing == null) rect.SetParent(parent, false);
        Place(rect, x0, y0, x1, y1);
        return rect;
    }

    public static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Image Art(Transform parent, string name, string asset, Color tint, float x0, float y0, float x1, float y1)
    {
        var rect = Rect(parent, name, x0, y0, x1, y1);
        var image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = Asset(asset); image.color = tint; image.raycastTarget = false;
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        return image;
    }

    public static Image Panel(Image image, Color accent, bool raised = false)
    {
        image.sprite = Asset("Panel Fill"); image.type = Image.Type.Sliced;
        image.color = raised ? Raised : Surface;
        image.preserveAspect = false;
        var outline = image.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        var frame = Art(image.transform, "Neon Frame", "Panel Frame", new Color(accent.r, accent.g, accent.b, .48f), 0, 0, 1, 1);
        frame.type = Image.Type.Sliced;
        frame.transform.SetAsFirstSibling();
        return frame;
    }

    public static Image Panel(RectTransform rect, Color accent, bool raised = false)
    {
        var image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        return Panel(image, accent, raised);
    }

    public static Image Button(Button button, Color accent, bool primary = false)
    {
        var visual = button.transform.Find("Hover Visual") ?? button.transform;
        var image = visual.GetComponent<Image>();
        if (image == null) image = visual.gameObject.AddComponent<Image>();
        image.sprite = Asset("Button Fill"); image.type = Image.Type.Sliced;
        image.color = primary ? Color.Lerp(Raised, accent, .13f) : Surface;
        var stripe=visual.Find("Accent")?.GetComponent<Image>();
        if(stripe!=null) stripe.enabled=false;
        var border = button.GetComponent<Outline>(); if (border != null) border.enabled = false;
        border = image.GetComponent<Outline>(); if (border != null) border.enabled = false;
        var frame = Art(visual, "Neon Frame", primary ? "Selected Frame" : "Button Frame", accent, 0, 0, 1, 1);
        frame.type = Image.Type.Sliced;
        frame.transform.SetAsFirstSibling();
        button.targetGraphic = frame;
        var colors = button.colors;
        colors.normalColor = new Color(.76f, .76f, .76f, primary ? 1 : .7f);
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.65f, .65f, .65f, 1);
        colors.disabledColor = new Color(.3f, .3f, .35f, .25f);
        colors.fadeDuration = .09f;
        button.colors = colors;
        return frame;
    }

    public static void Selection(Button button, bool selected, Color accent)
    {
        var visual = button.transform.Find("Hover Visual") ?? button.transform;
        var frame = visual.Find("Neon Frame")?.GetComponent<Image>();
        if (frame == null) frame = Button(button, accent);
        frame.sprite = Asset(selected ? "Selected Frame" : "Button Frame");
        frame.color = selected ? accent : new Color(accent.r, accent.g, accent.b, .4f);
        visual.GetComponent<Image>().color = selected ? Color.Lerp(Raised, accent, .1f) : Surface;
    }

    public static void Input(TMP_InputField input, Color accent)
    {
        var image = input.GetComponent<Image>();
        if (image == null) image = input.gameObject.AddComponent<Image>();
        var frame = Panel(image, accent);
        image.color = Ink;
        input.targetGraphic = frame;
        input.caretColor = accent;
        input.selectionColor = new Color(accent.r, accent.g, accent.b, .24f);
    }

    public static void Slider(Slider slider, Color accent)
    {
        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            fill.sprite = Asset("Rule"); fill.type = Image.Type.Simple; fill.color = accent;
        }
        if (slider.handleRect != null)
        {
            var image = slider.handleRect.GetComponent<Image>();
            image.sprite = Asset("Diamond"); image.preserveAspect = true; image.color = accent;
            slider.handleRect.sizeDelta=new Vector2(42,slider.handleRect.sizeDelta.y);
            var area=(RectTransform)slider.handleRect.parent;
            if(Mathf.Abs(area.anchorMax.y-area.anchorMin.y)<.001f) area.sizeDelta=new Vector2(area.sizeDelta.x,42);
            var border = image.GetComponent<Outline>(); if (border != null) border.enabled = false;
        }
    }

    public static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color,
        float x0, float y0, float x1, float y1, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var rect = Rect(parent, name, x0, y0, x1, y1);
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = value; label.fontSize = size; label.color = color;
        label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size;
        label.alignment = alignment; label.raycastTarget = false;
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        return label;
    }

    public static void Rule(Transform parent, string name, Color tint, float x0, float y, float x1)
        => Art(parent, name, "Rule", tint, x0, y, x1, y + .004f);

    public static void Emblem(Transform parent, string glyph, Color color, float x0, float y0, float x1, float y1)
    {
        var image = Art(parent, "UI Emblem", glyph, color, x0, y0, x1, y1);
        image.preserveAspect = true;
    }
}
