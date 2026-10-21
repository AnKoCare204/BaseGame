using UnityEngine;

/// Toán thuần của bóng nước nền trên BG gameplay (hệ tọa độ lớp, pivot giữa, đơn vị canvas).
public static class BgBubbleMath
{
    /// <summary>Bóng to lên nhanh hơn: nội suy tốc độ theo vị trí của size trong khoảng size (kẹp).</summary>
    public static float RiseSpeedForSize(float size, Vector2 sizeRange, Vector2 speedRange)
        => Mathf.Lerp(speedRange.x, speedRange.y, Mathf.InverseLerp(sizeRange.x, sizeRange.y, size));

    /// <summary>Độ cao chuẩn hóa trong phần màn hình nhìn thấy: 0 = mép dưới, 1 = mép trên.</summary>
    public static float NormalizedY(float y, float visibleHalfHeight)
        => visibleHalfHeight > 0f ? (y + visibleHalfHeight) / (2f * visibleHalfHeight) : 0f;

    /// <summary>Alpha: hiện dần trong fadeIn giây đầu, mờ dần trong dải fadeOutBand trên cùng, bằng 0 từ mép trên trở lên.</summary>
    public static float AlphaAt(float age, float normalizedY, float maxAlpha, float fadeIn, float fadeOutBand)
    {
        float appear = fadeIn > 0f ? Mathf.Clamp01(age / fadeIn) : 1f;
        float vanish = fadeOutBand > 0f ? Mathf.Clamp01((1f - normalizedY) / fadeOutBand) : (normalizedY < 1f ? 1f : 0f);
        return maxAlpha * appear * vanish;
    }

    /// <summary>Phồng nhẹ khi lên cao: 1 ở mép dưới, 1 + growth ở mép trên.</summary>
    public static float ScaleAt(float normalizedY, float growth) => 1f + growth * Mathf.Clamp01(normalizedY);

    /// <summary>x sinh bóng trong bề ngang nhìn thấy, chừa lề hai bên (lề quá lớn thì sinh ở giữa).</summary>
    public static float SpawnX(float u, float visibleHalfWidth, float edgeMargin)
    {
        float half = Mathf.Max(0f, visibleHalfWidth - edgeMargin);
        return Mathf.Lerp(-half, half, Mathf.Clamp01(u));
    }

    /// <summary>y sinh bóng: dưới mép dưới màn hình một khoảng bằng đường kính, để cả quả còn khuất.</summary>
    public static float SpawnY(float visibleHalfHeight, float size) => -visibleHalfHeight - size;

    /// <summary>Đã lên quá mép trên (cả quả khuất) chưa.</summary>
    public static bool IsAboveTop(float y, float visibleHalfHeight, float size) => y > visibleHalfHeight + size;
}
