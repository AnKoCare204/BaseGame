using System;
using UnityEngine;

/// <summary>
/// Vùng an toàn cục bộ, thay cho Crystal.SafeArea ở những panel không muốn bị đẩy cả dải.
/// Cạnh trên chỉ né những cutout (notch / punch-hole / pill) giao với dải ngang của chính rect này: punch-hole góc trái chỉ hạ zone trái,
/// notch giữa chỉ hạ zone giữa, zone không dính cutout đứng yên. Các cạnh còn lại lấy theo Screen.safeArea (home indicator, bo góc).
/// Cách dùng: đặt zone làm con của một panel phủ toàn màn hình (anchor thiết kế của zone tính theo màn hình), UI cần né làm con của zone
/// và neo vào cạnh của zone; zone bị thu từ trên xuống thì UI neo cạnh trên tự bị đẩy xuống, kích thước UI giữ nguyên.
/// TopLift: mỗi khi zone thực sự bị đẩy xuống (inset trên lớn hơn 0) thì nâng ngược lên một khoảng cố định, cho phép UI lấn ra ngoài vùng an toàn
/// một chút (kể cả vào phần cutout) để đỡ thừa khoảng trống; không bao giờ nâng cao hơn vị trí thiết kế.
/// Anchor thiết kế được ghi nhớ ở Awake (hoặc lần Refresh đầu), mọi lần tính lại đều xuất phát từ anchor đó.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaZone : MonoBehaviour
{
    [Flags]
    public enum Edges
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8,
        All = Top | Bottom | Left | Right
    }

    public enum TopMode
    {
        /// <summary>Chỉ né cutout giao với dải ngang của zone (iOS không có cutouts thì tự dùng safeArea).</summary>
        CutoutsInZone,
        /// <summary>Luôn dùng inset trên của Screen.safeArea như Crystal.SafeArea cũ.</summary>
        FullSafeArea
    }

    [field: SerializeField, Tooltip("Những cạnh cần né. Zone chỉ chứa UI neo cạnh trên thì để mỗi Top")]
    public Edges ConformEdges { get; set; } = Edges.Top;

    [field: SerializeField, Tooltip("Cách tính cạnh trên: CutoutsInZone chỉ né cutout giao với zone; FullSafeArea đẩy cả dải như cũ")]
    public TopMode Top { get; set; } = TopMode.CutoutsInZone;

    [field: SerializeField, Min(0f), Tooltip("Khi zone bị đẩy xuống vì safe area / cutout thì nâng ngược lên bấy nhiêu (đơn vị canvas, cùng đơn vị với anchoredPosition của UI con), cho phép UI lấn ra ngoài vùng an toàn một chút. Không nâng cao hơn vị trí thiết kế. 0 = tắt")]
    public float TopLift { get; set; }

    [field: SerializeField, Tooltip("Log anchor mỗi lần áp dụng (debug)")]
    private bool Logging { get; set; }

    /// <summary>Anchor thiết kế (trước khi né), ghi ở Awake / Refresh đầu tiên.</summary>
    public Vector2 DesignAnchorMin { get; private set; }

    public Vector2 DesignAnchorMax { get; private set; }

    private RectTransform rect;
    private bool designCaptured;
    private int appliedVersion = -1;
    // Thiết lập đã áp dụng lần trước: đổi trong Inspector lúc Play (hoặc qua code) thì frame sau tự tính lại dù màn hình không đổi.
    private Edges appliedEdges;
    private TopMode appliedTop;
    private float appliedLift = float.NaN;

    private void Awake()
    {
        CaptureDesign();
    }

    private void OnEnable()
    {
        Refresh(true);
    }

    private void Update()
    {
        Refresh(false);
    }

    /// <summary>Ghi nhớ anchor hiện tại làm anchor thiết kế. Gọi lại nếu chủ động đổi anchor ở runtime.</summary>
    public void CaptureDesign()
    {
        if (rect == null) rect = (RectTransform)transform;
        DesignAnchorMin = rect.anchorMin;
        DesignAnchorMax = rect.anchorMax;
        designCaptured = true;
    }

    /// <summary>Tính lại anchor theo SafeAreaInfo. force = bỏ qua cache Version (dùng ngoài Play mode hoặc sau SetOverride).</summary>
    public void Refresh(bool force)
    {
        if (!designCaptured) CaptureDesign();
        SafeAreaInfo.Poll(force);
        bool settingsChanged = appliedEdges != ConformEdges || appliedTop != Top || !Mathf.Approximately(appliedLift, TopLift);
        if (!force && !settingsChanged && appliedVersion == SafeAreaInfo.Version) return;
        appliedVersion = SafeAreaInfo.Version;
        appliedEdges = ConformEdges;
        appliedTop = Top;
        appliedLift = TopLift;

        Vector2Int size = SafeAreaInfo.ScreenSize;
        if (size.x <= 0 || size.y <= 0) return;

        // Rect thiết kế theo pixel màn hình (giả định cha phủ toàn màn hình).
        float xMin = DesignAnchorMin.x * size.x;
        float xMax = DesignAnchorMax.x * size.x;
        float yMin = DesignAnchorMin.y * size.y;
        float yMax = DesignAnchorMax.y * size.y;
        float spanXMin = xMin, spanXMax = xMax, spanYMin = yMin, spanYMax = yMax;

        if (Has(Edges.Top))
        {
            float inset = Top == TopMode.FullSafeArea ? SafeAreaInfo.SafeTopInset : SafeAreaInfo.TopInset(spanXMin, spanXMax);
            // Có bị đẩy xuống thì nâng ngược lên TopLift (đổi sang px màn hình), tối đa về lại vị trí thiết kế.
            if (inset > 0f && TopLift > 0f) inset = Mathf.Max(0f, inset - TopLift * CanvasScale());
            yMax = Mathf.Min(yMax, size.y - inset);
        }
        if (Has(Edges.Bottom)) yMin = Mathf.Max(yMin, SafeAreaInfo.BottomInset(spanXMin, spanXMax));
        if (Has(Edges.Left)) xMin = Mathf.Max(xMin, SafeAreaInfo.LeftInset(spanYMin, spanYMax));
        if (Has(Edges.Right)) xMax = Mathf.Min(xMax, size.x - SafeAreaInfo.RightInset(spanYMin, spanYMax));

        // Zone bị nuốt trọn (cutout cao hơn zone): ép về một đường để UI neo cạnh vẫn bị đẩy hết ra ngoài vùng che, không lật ngược.
        if (yMax < yMin)
        {
            if (Has(Edges.Top) && !Has(Edges.Bottom)) yMin = yMax;
            else if (Has(Edges.Bottom) && !Has(Edges.Top)) yMax = yMin;
            else yMin = yMax = (yMin + yMax) * 0.5f;
        }
        if (xMax < xMin)
        {
            if (Has(Edges.Left) && !Has(Edges.Right)) xMax = xMin;
            else if (Has(Edges.Right) && !Has(Edges.Left)) xMin = xMax;
            else xMin = xMax = (xMin + xMax) * 0.5f;
        }

        Vector2 anchorMin = new(xMin / size.x, yMin / size.y);
        Vector2 anchorMax = new(xMax / size.x, yMax / size.y);
        // Một số máy Samsung trả safeArea NaN ở frame đầu: bỏ qua, frame sau Version đổi sẽ áp lại.
        if (float.IsNaN(anchorMin.x) || float.IsNaN(anchorMin.y) || float.IsNaN(anchorMax.x) || float.IsNaN(anchorMax.y)
            || anchorMin.x < 0f || anchorMin.y < 0f || anchorMax.x < 0f || anchorMax.y < 0f)
        {
            appliedVersion = -1;
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;

        if (Logging)
        {
            Debug.Log($"[SafeAreaZone] {name}: anchorMin={anchorMin} anchorMax={anchorMax} (design {DesignAnchorMin}-{DesignAnchorMax}, screen {size}, cutouts {SafeAreaInfo.Cutouts.Length})", this);
        }
    }

    private bool Has(Edges edge) => (ConformEdges & edge) != 0;

    /// <summary>Đổi đơn vị canvas sang pixel màn hình (CanvasScaler). Không nằm trong canvas thì coi 1 đơn vị = 1 px.</summary>
    private float CanvasScale()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
    }
}
