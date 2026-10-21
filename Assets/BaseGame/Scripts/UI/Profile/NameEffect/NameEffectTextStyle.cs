using System;
using TMPro;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Phần "rẻ nhất mà đẹp nhất" của một name effect: đổi material preset /
    /// gradient của TMP. Riêng nó đã đủ làm cả nhóm effect Common (chữ vàng,
    /// chữ bạc, viền neon...) mà không tốn một animation nào.
    /// </summary>
    [Serializable]
    public class NameEffectTextStyle
    {
        [Tooltip("Material preset của TMP (Create Material Preset từ font asset). Bỏ trống = giữ material gốc.")]
        [SerializeField] private Material fontMaterial;

        [SerializeField] private bool overrideVertexGradient;
        [SerializeField] private VertexGradient vertexGradient;

        [SerializeField] private bool overrideColor;
        [SerializeField] private Color color = Color.white;

        [Tooltip("Đổi cách TMP trải UV cho face texture. Character = mỗi chữ cái một hoa văn riêng (mặc định của TMP); Line/Paragraph = hoa văn trải liền trên cả tên — bắt buộc cho các effect kiểu mặt nước, dung nham.")]
        [SerializeField] private bool overrideTextureMapping;
        [SerializeField] private TextureMappingOptions horizontalMapping = TextureMappingOptions.Line;
        [SerializeField] private TextureMappingOptions verticalMapping = TextureMappingOptions.Line;

        public Material FontMaterial => fontMaterial;

        public void Apply(TMP_Text text)
        {
            if (text == null)
                return;

            if (fontMaterial != null)
                text.fontSharedMaterial = fontMaterial;

            if (overrideVertexGradient)
            {
                text.enableVertexGradient = true;
                text.colorGradient = vertexGradient;
            }

            if (overrideColor)
                text.color = color;

            if (overrideTextureMapping)
            {
                text.horizontalMapping = horizontalMapping;
                text.verticalMapping = verticalMapping;
            }
        }
    }

    /// <summary>
    /// Ảnh chụp style gốc của một TMP trước khi effect ghi đè. Bắt buộc phải có:
    /// text trong list ảo hoá bị tái sử dụng, không trả style về thì effect của
    /// hạng 1 sẽ dính lại ở hạng 500.
    /// </summary>
    public struct NameEffectTextState
    {
        public bool Captured;
        public Material FontMaterial;
        public bool EnableVertexGradient;
        public VertexGradient Gradient;
        public Color Color;
        public TextureMappingOptions HorizontalMapping;
        public TextureMappingOptions VerticalMapping;

        public static NameEffectTextState Capture(TMP_Text text)
        {
            if (text == null)
                return default;

            return new NameEffectTextState
            {
                Captured = true,
                FontMaterial = text.fontSharedMaterial,
                EnableVertexGradient = text.enableVertexGradient,
                Gradient = text.colorGradient,
                Color = text.color,
                HorizontalMapping = text.horizontalMapping,
                VerticalMapping = text.verticalMapping,
            };
        }

        public void Restore(TMP_Text text)
        {
            if (!Captured || text == null)
                return;

            if (FontMaterial != null)
                text.fontSharedMaterial = FontMaterial;

            text.enableVertexGradient = EnableVertexGradient;
            text.colorGradient = Gradient;
            text.color = Color;
            text.horizontalMapping = HorizontalMapping;
            text.verticalMapping = VerticalMapping;
        }
    }
}
