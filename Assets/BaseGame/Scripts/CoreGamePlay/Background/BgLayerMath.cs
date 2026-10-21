using UnityEngine;

/// Toán dùng chung cho các lớp BG gameplay. Lớp BG phủ kín màn hình kiểu Envelope và nằm giữa canvas,
/// nên gốc tọa độ (pivot giữa) của lớp trùng tâm màn hình; phần nhìn thấy là phần giao giữa màn hình và lớp.
public static class BgLayerMath
{
    /// <summary>Nửa kích thước phần nhìn thấy theo một trục: nửa của cạnh nhỏ hơn giữa màn hình (root canvas) và lớp.</summary>
    public static float VisibleHalf(float rootSize, float layerSize) => Mathf.Max(0f, Mathf.Min(rootSize, layerSize)) * 0.5f;

    /// <summary>Giá trị trong khoảng [range.x, range.y] theo u (kẹp vào 0..1).</summary>
    public static float InRange(Vector2 range, float u) => Mathf.Lerp(range.x, range.y, Mathf.Clamp01(u));

    /// <summary>Sóng sin theo giây: biên độ × sin(2π × (t × tần số + pha)).</summary>
    public static float Wave(float time, float amplitude, float frequency, float phase)
        => amplitude * Mathf.Sin((time * frequency + phase) * 2f * Mathf.PI);
}
