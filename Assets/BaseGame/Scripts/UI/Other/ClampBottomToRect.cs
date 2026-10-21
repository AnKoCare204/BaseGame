using UnityEngine;

/// <summary>
/// Sets this rect's height once so its bottom sits on the top of <see cref="Limit"/>.
/// Anchor and pivot must stay at the top. SafeArea has already applied in Awake.
/// </summary>
public class ClampBottomToRect : MonoBehaviour
{
    [field: SerializeField] RectTransform Limit { get; set; }

    void Start()
    {
        if (Limit == null) return;

        RectTransform rect = (RectTransform)transform;
        if (rect.parent is not RectTransform parent) return;

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        Limit.GetWorldCorners(corners);
        float limitTop = corners[1].y;

        rect.GetWorldCorners(corners);
        float top = corners[1].y;

        float scaleY = rect.lossyScale.y;
        if (scaleY <= 0.0001f) return;

        // Pivot stays at the top, so only the bottom moves. sizeDelta.y is the leftover
        // after the anchor span, which is 0 when both vertical anchors sit on the top edge.
        float height = Mathf.Max(0f, (top - limitTop) / scaleY);
        float anchorSpan = rect.anchorMax.y - rect.anchorMin.y;
        Vector2 size = rect.sizeDelta;
        size.y = height - anchorSpan * parent.rect.height;
        rect.sizeDelta = size;
    }
}
