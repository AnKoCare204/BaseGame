using System;
using UnityEngine;

/// <summary>
/// Nguồn dữ liệu vùng an toàn dùng chung cho SafeAreaZone / SafeAreaDebugOverlay.
/// Gom Screen.safeArea và Screen.cutouts (từng vùng bị chiếm thật: notch, punch-hole, pill; pixel màn hình, gốc góc dưới-trái như
/// Screen.safeArea). Poll() cache theo frame và tăng Version khi dữ liệu đổi (đổi độ phân giải, xoay, đổi thiết bị trong Device Simulator),
/// nên zone chỉ tính lại khi cần. SetOverride giả lập thủ công cho test trong Editor.
/// Lưu ý: iOS thường không cấp cutouts (mảng rỗng) nên các hàm inset tự fallback về Screen.safeArea như cách cũ.
/// </summary>
public static class SafeAreaInfo
{
    public enum Edge { Top, Bottom, Left, Right }

    /// <summary>Tăng mỗi khi ScreenSize / SafeArea / Cutouts đổi. 0 = chưa poll lần nào.</summary>
    public static int Version { get; private set; }

    public static Vector2Int ScreenSize { get; private set; }
    public static Rect SafeArea { get; private set; }

    /// <summary>Các vùng bị chiếm thật (bản sao, không sửa). Rỗng khi thiết bị / nền tảng không cấp dữ liệu.</summary>
    public static Rect[] Cutouts { get; private set; } = Array.Empty<Rect>();

    public static bool HasOverride { get; private set; }

    public static float SafeTopInset => Mathf.Max(0f, ScreenSize.y - SafeArea.yMax);
    public static float SafeBottomInset => Mathf.Max(0f, SafeArea.yMin);
    public static float SafeLeftInset => Mathf.Max(0f, SafeArea.xMin);
    public static float SafeRightInset => Mathf.Max(0f, ScreenSize.x - SafeArea.xMax);

    private static int lastPolledFrame = -1;
    private static ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;
    private static Vector2Int overrideSize;
    private static Rect overrideSafeArea;
    private static Rect[] overrideCutouts = Array.Empty<Rect>();

    /// <summary>Đọc lại dữ liệu màn hình nếu frame này chưa đọc (force = luôn đọc, cần khi gọi ngoài Play mode vì Time.frameCount không tăng).</summary>
    public static void Poll(bool force = false)
    {
        if (!force && Application.isPlaying && lastPolledFrame == Time.frameCount) return;
        lastPolledFrame = Time.frameCount;

        Vector2Int size;
        Rect safe;
        if (HasOverride)
        {
            size = overrideSize;
            safe = overrideSafeArea;
        }
        else
        {
            size = new Vector2Int(Screen.width, Screen.height);
            safe = Screen.safeArea;
        }

        bool baseChanged = Version == 0 || size != ScreenSize || safe != SafeArea || Screen.orientation != lastOrientation;
        // Screen.cutouts cấp mảng mới mỗi lần gọi: trên thiết bị chỉ đọc lại khi size / safeArea / hướng đổi (tránh GC mỗi frame);
        // trong Editor đọc mỗi frame để Device Simulator đổi giữa hai máy cùng độ phân giải (Punch Hole Left / Right) vẫn nhận ra.
        if (!baseChanged && !Application.isEditor) return;

        Rect[] cutouts = HasOverride ? overrideCutouts : (Screen.cutouts ?? Array.Empty<Rect>());
        if (!baseChanged && SameCutouts(cutouts, Cutouts)) return;

        ScreenSize = size;
        SafeArea = safe;
        lastOrientation = Screen.orientation;
        Cutouts = (Rect[])cutouts.Clone();
        Version++;
    }

    /// <summary>Giả lập dữ liệu màn hình (test / Editor). Gọi ClearOverride để quay lại dữ liệu thật.</summary>
    public static void SetOverride(Vector2Int screenSize, Rect safeArea, params Rect[] cutouts)
    {
        HasOverride = true;
        overrideSize = screenSize;
        overrideSafeArea = safeArea;
        overrideCutouts = cutouts ?? Array.Empty<Rect>();
        Poll(true);
    }

    public static void ClearOverride()
    {
        HasOverride = false;
        Poll(true);
    }

    /// <summary>Cạnh màn hình gần cutout nhất (punch-hole gần đỉnh dù nằm lệch trái/phải vẫn là cạnh trên).</summary>
    public static Edge ClassifyEdge(Rect cutout)
    {
        float dTop = ScreenSize.y - cutout.yMax;
        float dBottom = cutout.yMin;
        float dLeft = cutout.xMin;
        float dRight = ScreenSize.x - cutout.xMax;
        float min = Mathf.Min(Mathf.Min(dTop, dBottom), Mathf.Min(dLeft, dRight));
        if (min == dTop) return Edge.Top;
        if (min == dBottom) return Edge.Bottom;
        return min == dLeft ? Edge.Left : Edge.Right;
    }

    /// <summary>
    /// Inset cạnh trên (px) chỉ do cutout vật lý phía trên giao với dải ngang [xMin, xMax] (đáy thấp nhất của các cutout đó).
    /// 0 khi không cutout nào giao hoặc không có dữ liệu cutouts. Đây là "sàn cứng": UI không được lấn vào.
    /// </summary>
    public static float CutoutTopInset(float xMin, float xMax)
    {
        float inset = 0f;
        foreach (Rect c in Cutouts)
        {
            if (ClassifyEdge(c) != Edge.Top || !Overlaps(c.xMin, c.xMax, xMin, xMax)) continue;
            inset = Mathf.Max(inset, ScreenSize.y - c.yMin);
        }
        return inset;
    }

    /// <summary>
    /// Inset cạnh trên (px) cho dải ngang [xMin, xMax]: chỉ xét cutout phía trên giao với dải. Có cutout giao thì lấy max(cutout, safeArea)
    /// cho chắc; không cutout nào giao thì 0. Không có dữ liệu cutouts (iOS) thì trả về inset của Screen.safeArea như cũ.
    /// </summary>
    public static float TopInset(float xMin, float xMax)
    {
        float cutoutInset = CutoutTopInset(xMin, xMax);
        // Không có dữ liệu cutouts (iOS) thì coi cả dải safeArea là thật; có dữ liệu mà không cutout nào giao thì dải này không liên quan tới zone.
        if (cutoutInset > 0f || Cutouts.Length == 0) return Mathf.Max(cutoutInset, SafeTopInset);
        return 0f;
    }

    /// <summary>Inset cạnh dưới (px): luôn ít nhất bằng Screen.safeArea (home indicator không phải cutout), cộng cutout phía dưới giao với dải nếu có.</summary>
    public static float BottomInset(float xMin, float xMax)
    {
        float inset = SafeBottomInset;
        foreach (Rect c in Cutouts)
        {
            if (ClassifyEdge(c) != Edge.Bottom || !Overlaps(c.xMin, c.xMax, xMin, xMax)) continue;
            inset = Mathf.Max(inset, c.yMax);
        }
        return inset;
    }

    /// <summary>Inset cạnh trái (px) cho dải dọc [yMin, yMax] (landscape). Portrait thường bằng 0.</summary>
    public static float LeftInset(float yMin, float yMax)
    {
        float inset = SafeLeftInset;
        foreach (Rect c in Cutouts)
        {
            if (ClassifyEdge(c) != Edge.Left || !Overlaps(c.yMin, c.yMax, yMin, yMax)) continue;
            inset = Mathf.Max(inset, c.xMax);
        }
        return inset;
    }

    /// <summary>Inset cạnh phải (px) cho dải dọc [yMin, yMax] (landscape). Portrait thường bằng 0.</summary>
    public static float RightInset(float yMin, float yMax)
    {
        float inset = SafeRightInset;
        foreach (Rect c in Cutouts)
        {
            if (ClassifyEdge(c) != Edge.Right || !Overlaps(c.yMin, c.yMax, yMin, yMax)) continue;
            inset = Mathf.Max(inset, ScreenSize.x - c.xMin);
        }
        return inset;
    }

    private static bool Overlaps(float aMin, float aMax, float bMin, float bMax) => aMax > bMin && aMin < bMax;

    private static bool SameCutouts(Rect[] a, Rect[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
