using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(Image))]
[DisallowMultipleComponent]
public sealed class UIHoverSlide : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float distance = 14f;
    [SerializeField] private float smoothTime = .08f;

    private Button button;
    [SerializeField] private RectTransform visual;
    private bool hovered;
    private float offset;
    private float velocity;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void PrepareVisual()
    {
        if (visual != null) return;
        button = GetComponent<Button>();
        var background = GetComponent<Image>();
        var go = new GameObject("Hover Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visual = go.GetComponent<RectTransform>();
        visual.SetParent(transform, false);
        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = visual.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = background.sprite;
        image.color = background.color;
        image.material = background.material;
        image.type = background.type;
        image.preserveAspect = background.preserveAspect;
        image.raycastTarget = false;

        // Keep the layout slot and pointer hit area still; slide only the artwork.
        while (transform.GetChild(0) != visual)
            transform.GetChild(0).SetParent(visual, false);
        background.color = Color.clear;
        background.canvasRenderer.cullTransparentMesh = false;
        button.targetGraphic = image;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = button.IsInteractable();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    private void Update()
    {
        if (!button.IsInteractable())
        {
            ResetPosition();
            return;
        }

        offset = Mathf.SmoothDamp(offset, hovered ? distance : 0f, ref velocity,
            smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        visual.anchoredPosition = new Vector2(offset, 0f);
    }

    private void OnDisable() => ResetPosition();

    private void ResetPosition()
    {
        hovered = false;
        offset = velocity = 0f;
        if (visual != null) visual.anchoredPosition = Vector2.zero;
    }
}
