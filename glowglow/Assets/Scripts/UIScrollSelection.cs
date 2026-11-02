using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Keeps keyboard/controller selection visible without polling every frame.</summary>
[DisallowMultipleComponent]
public sealed class UIScrollSelection : MonoBehaviour, ISelectHandler
{
    private ScrollRect scroll;

    public void OnSelect(BaseEventData eventData)
    {
        if (scroll == null) scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || !scroll.isActiveAndEnabled) return;
        Canvas.ForceUpdateCanvases();
        Rect viewport = scroll.viewport.rect;
        Bounds row = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)transform);
        float offset = row.max.y > viewport.yMax ? viewport.yMax - row.max.y :
            row.min.y < viewport.yMin ? viewport.yMin - row.min.y : 0;
        if (Mathf.Abs(offset) < .01f) return;
        scroll.StopMovement();
        Vector2 position = scroll.content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + offset, 0, Mathf.Max(0, scroll.content.rect.height - viewport.height));
        scroll.content.anchoredPosition = position;
    }
}
