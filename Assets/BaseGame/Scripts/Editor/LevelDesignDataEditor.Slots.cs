using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEngine;

/// <summary>
/// Vẽ ô: khung bo góc, icon cá, ô trống - cộng hai khâu phục vụ cho chúng là theo dõi ô đang bị chuột đè và
/// cache chuỗi nhãn. Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    /// <summary>Bán kính bo góc của mọi ô vuông trong tool.</summary>
    private const float CornerRadius = 4f;

    /// <summary>Cạnh checkbox isHidden và nút xoá đè lên hai góc trên của ô cá.</summary>
    private const float OverlaySize = 16f;

    private static readonly Color OverlayBackdrop = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color FishHighlight = new Color(0.2f, 0.9f, 0.28f, 1f);
    private static readonly Color FishHighlightFill = new Color(FishHighlight.r, FishHighlight.g, FishHighlight.b, 0.4f);
    private static readonly Color HiddenIconTint = new Color(1f, 1f, 1f, 0.3f);

    /// <summary>
    /// Rect của mọi ô đổi viền theo chuột ở lần Repaint gần nhất, quy về toạ độ màn hình: rect gom được nằm trong
    /// group của scroll view nên toạ độ của nó là toạ độ trong group, còn chuột lúc so thì ở ngoài mọi group.
    /// Không quy về cùng một hệ là so kiểu gì cũng lệch.
    /// </summary>
    private readonly List<Rect> hoverRects = new List<Rect>(512);

    /// <summary>Ô đang bị chuột đè ở lần Repaint gần nhất (toạ độ màn hình); width 0 = không đè ô nào.</summary>
    private Rect hoverRect;

    /// <summary>Chuột quy về toạ độ màn hình, lấy một lần ở đầu pass Repaint thay vì quy đổi lại cho từng ô.</summary>
    private Vector2 hoverMouse;

    /// <summary>Chuỗi trong ô "ID cá" trên lưới bóng, và số đã parse (-1 = không lọc).</summary>
    private string highlightFishIdText = "";

    private int highlightFishId = -1;

    // Nhãn kiểu "ID 12" / "#12" / "1.4" bị dựng lại cho từng bóng ở *mọi* pass GUILayout, level 60 bóng là vài trăm
    // chuỗi rác mỗi lần chuột nhúc nhích. Cache theo số; tool chỉ chạy quanh vài chục số nên bảng không phình.
    private static readonly Dictionary<int, string> BubbleIdLabels = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> IndexLabels = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> NumberLabels = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> RadiusLabels = new Dictionary<int, string>();

    private static string CachedLabel(Dictionary<int, string> cache, string prefix, int value)
    {
        if (cache.TryGetValue(value, out string text)) return text;
        text = prefix + value;
        cache[value] = text;
        return text;
    }

    private static string BubbleIdLabel(int id) => CachedLabel(BubbleIdLabels, "ID ", id);

    private static string IndexLabel(int index) => CachedLabel(IndexLabels, "#", index);

    private static string NumberLabel(int value) => CachedLabel(NumberLabels, string.Empty, value);

    /// <summary>Nhãn size bóng, khoá theo đúng một chữ số thập phân mà nó hiện ra.</summary>
    private static string RadiusLabel(float radius)
    {
        int key = Mathf.RoundToInt(radius * 10f);
        if (RadiusLabels.TryGetValue(key, out string text)) return text;
        text = (key / 10f).ToString("0.0");
        RadiusLabels[key] = text;
        return text;
    }

    private void RefreshHighlightFishId()
    {
        highlightFishId = !string.IsNullOrWhiteSpace(highlightFishIdText) &&
                          int.TryParse(highlightFishIdText.Trim(), out int id) && id >= 0
            ? id
            : -1;
    }

    /// <summary>Mở một pass Repaint: danh sách hover dựng lại từ đầu theo đúng những ô sắp vẽ.</summary>
    private void BeginHoverPass()
    {
        hoverRects.Clear();
        hoverRect = default;
        hoverMouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
    }

    private void TrackHover(Rect rect)
    {
        var screen = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
        hoverRects.Add(screen);
        if (hoverRect.width <= 0f && screen.Contains(hoverMouse)) hoverRect = screen;
    }

    /// <summary>
    /// Chuột chuyển sang ô khác chưa? Rê trong lòng một ô thì hình không đổi tí nào, mà mỗi lần vẽ lại là chạy hết
    /// Layout + Repaint trên vài trăm ô - đây là chỗ tốn nhất của tool khi level nhiều bóng.
    /// </summary>
    private bool HoverTargetChanged(Vector2 mousePosition)
    {
        Vector2 point = GUIUtility.GUIToScreenPoint(mousePosition);
        Rect next = default;
        for (int i = 0; i < hoverRects.Count; i++)
        {
            if (!hoverRects[i].Contains(point)) continue;
            next = hoverRects[i];
            break;
        }

        return next != hoverRect;
    }

    /// <summary>
    /// Ô bo góc. IMGUI không có sẵn hình chữ nhật bo góc, nhưng GUI.DrawTexture có tham số cornerRadii / borderWidths:
    /// gọi lần đầu không viền để tô ruột, lần hai đặt borderWidths &gt; 0 để vẽ mỗi vành viền. Cả hai chỉ chạy ở Repaint.
    /// </summary>
    private static void DrawRoundedRect(Rect rect, Color fill, Color border, float borderWidth = 1f)
    {
        if (Event.current.type != EventType.Repaint) return;

        Vector4 radii = Vector4.one * CornerRadius;
        if (fill.a > 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero, radii);
        }

        if (border.a > 0f && borderWidth > 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border,
                Vector4.one * borderWidth, radii);
        }
    }

    /// <summary>Màu viền ô theo chuột. Chỉ gọi trong pass Repaint nên tiện ghi luôn rect vào danh sách hover.</summary>
    private Color SlotBorder(Rect rect, DesignConfig design)
    {
        TrackHover(rect);
        return rect.Contains(Event.current.mousePosition) ? design.HoverColor : SirenixGUIStyles.BorderColor;
    }

    /// <summary>
    /// Ô cá. Thuần trang trí - không có control IMGUI nào bên trong - nên bỏ hẳn ở các pass không phải Repaint:
    /// Layout khỏi phải tra icon, dựng chuỗi và test chuột cho vài trăm ô mỗi lần có event.
    /// </summary>
    private void DrawFishIcon(Rect rect, int fishId, DesignConfig design, bool hidden, bool selected = false,
        bool showIdLabel = true, bool error = false, bool lossSpot = false)
    {
        if (Event.current.type != EventType.Repaint) return;

        // Đỏ (lỗi Check) đè cam (hay thua, xem LossSpots), cam đè xanh (ô tìm cá).
        bool hot = !error && lossSpot;
        bool match = !error && !hot && highlightFishId >= 0 && fishId == highlightFishId;
        Color fill = error ? CheckErrorFill : hot ? LossSpotFill : match ? FishHighlightFill : design.SlotFilledColor;
        Color border = selected ? design.HoverColor
            : error ? CheckError
            : hot ? LossSpot
            : match ? FishHighlight
            : SlotBorder(rect, design);
        DrawRoundedRect(rect, fill, border, selected || match || error || hot ? 2f : 1f);

        Texture icon = icons?.Get(fishId);
        if (icon != null)
        {
            const float pad = 3f;
            var drawRect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
            Color previous = GUI.color;
            if (hidden) GUI.color = HiddenIconTint;
            GUI.DrawTexture(drawRect, icon, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        if (!showIdLabel) return;
        GUI.Label(new Rect(rect.x + 2f, rect.yMax - 15f, rect.width - 5f, 12f), NumberLabel(fishId),
            SirenixGUIStyles.RightAlignedWhiteMiniLabel);
    }

    private void DrawEmptySlot(Rect rect, DesignConfig design, string label)
    {
        if (Event.current.type != EventType.Repaint) return;

        DrawRoundedRect(rect, design.SlotEmptyColor, SlotBorder(rect, design));
        GUI.Label(rect, label, SirenixGUIStyles.LabelCentered);
    }
}
