using TMPro;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Mức chi tiết khi vẽ một name effect. Cùng một effect id nhưng chỗ hiện
    /// khác nhau thì chạy mức khác nhau: popup profile chạy <see cref="Full"/>,
    /// bảng xếp hạng 50 dòng chỉ cần <see cref="Static"/> (đổi style chữ, không animation).
    /// Layer nào cần mức nào do chính layer khai báo (<see cref="NameEffectLayer.MinQuality"/>).
    /// </summary>
    public enum NameEffectQuality
    {
        /// <summary>Chỉ style chữ (material/gradient) — 0 animation, 0 chi phí Update.</summary>
        Static = 0,

        /// <summary>Animation nền rẻ tiền: quét sáng, mây trôi. Không mưa/chớp/particle.</summary>
        Medium = 1,

        /// <summary>Đầy đủ mọi layer.</summary>
        Full = 2,
    }

    /// <summary>
    /// Độ hiếm — dùng để xếp ưu tiên khi số effect đang chạy vượt ngân sách
    /// (<see cref="NameEffectBudget"/>): Legendary luôn giành được slot animation
    /// trước Common.
    /// </summary>
    public enum NameEffectRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }

    /// <summary>
    /// View nằm trước hay sau chữ trong thứ tự vẽ.
    ///
    /// <see cref="WrapText"/> dành cho các effect Epic/Legendary cần cả layer sau
    /// chữ (mây, dải sáng) lẫn layer trước chữ (mưa, chớp): host sẽ nhấc chính
    /// TMP text vào <see cref="NameEffectView.TextSlot"/> của prefab, nên prefab
    /// tự quyết định thứ tự bằng cách sắp con trước/sau slot đó.
    /// </summary>
    public enum NameEffectPlacement
    {
        InFront = 0,
        Behind = 1,
        WrapText = 2,
    }

    /// <summary>
    /// Dữ liệu của một lần bind, truyền xuống mọi layer. Là struct đọc-only để
    /// layer không giữ tham chiếu sống tới host.
    /// </summary>
    public readonly struct NameEffectContext
    {
        public readonly TMP_Text Text;
        public readonly RectTransform TextRect;
        public readonly NameEffectQuality Quality;

        /// <summary>
        /// Lần bind này có được cấp slot animation không. <c>false</c> = phải vẽ
        /// ở dạng tĩnh dù <see cref="Quality"/> cao (ngân sách đã đầy).
        /// </summary>
        public readonly bool Animated;

        public NameEffectContext(TMP_Text text, NameEffectQuality quality, bool animated)
        {
            Text = text;
            TextRect = text != null ? text.rectTransform : null;
            Quality = quality;
            Animated = animated;
        }
    }
}
