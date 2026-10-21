using UnityEngine;

/// Toán thuần của đàn cá xa trên BG gameplay (hệ tọa độ lớp, pivot giữa, đơn vị canvas).
public static class BgFishSchoolMath
{
    /// <summary>+1 bơi sang phải, -1 bơi sang trái.</summary>
    public static int PickDirection(float u) => u < 0.5f ? 1 : -1;

    /// <summary>Lề ngoài mép thực dùng: ít nhất nửa bề ngang con lớn nhất, để cá xuất phát và biến mất khi đã khuất hẳn.</summary>
    public static float EffectiveMargin(float edgeMargin, float fishWidth, float maxScale)
        => Mathf.Max(edgeMargin, fishWidth * maxScale * 0.5f);

    /// <summary>x xuất phát: ngoài mép phía sau lưng (ngược hướng bơi), lùi thêm `behind` cho các con sau trong đàn.</summary>
    public static float StartX(int direction, float visibleHalfWidth, float margin, float behind)
        => -direction * (visibleHalfWidth + margin + Mathf.Max(0f, behind));

    /// <summary>Đã qua hẳn mép phía trước (theo hướng bơi) chưa.</summary>
    public static bool IsOutside(float x, int direction, float visibleHalfWidth, float margin)
        => direction > 0 ? x > visibleHalfWidth + margin : x < -(visibleHalfWidth + margin);

    /// <summary>y gốc của đàn. heightBand tính theo chiều cao lớp: 0 = đáy, 1 = đỉnh.</summary>
    public static float SchoolBaseY(Vector2 heightBand, float u, float layerHeight)
        => (BgLayerMath.InRange(heightBand, u) - 0.5f) * layerHeight;

    /// <summary>Dấu localScale.x: fish.png quay đầu sang trái, nên bơi sang phải thì lật.</summary>
    public static float FacingSign(int direction) => direction > 0 ? -1f : 1f;
}
