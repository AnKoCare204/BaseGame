using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Mô tả một name effect. Thêm effect mới = tạo thêm 1 asset, không sửa code.
    ///
    /// Một effect có thể chỉ có style chữ (không prefab), chỉ có prefab, hoặc cả hai.
    /// </summary>
    [CreateAssetMenu(fileName = "NameEffect_", menuName = "GlobalConfigs/Name Effect Definition")]
    public class NameEffectDefinition : ScriptableObject
    {
        [Tooltip("Trùng với players.info.EffectNameIndex mà server trả về.")]
        [SerializeField] private int id;

        [SerializeField] private string displayName;
        [SerializeField] private NameEffectRarity rarity = NameEffectRarity.Common;

        [Tooltip("Trần chất lượng của effect này. Effect nặng có thể ép xuống Medium ở mọi nơi.")]
        [SerializeField] private NameEffectQuality maxQuality = NameEffectQuality.Full;

        [Tooltip("Prefab chứa các layer động. Bỏ trống = effect chỉ đổi style chữ.")]
        [SerializeField] private NameEffectView viewPrefab;

        [SerializeField] private bool overrideTextStyle;
        [SerializeField] private NameEffectTextStyle textStyle = new NameEffectTextStyle();

        public int Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public NameEffectRarity Rarity => rarity;
        public NameEffectQuality MaxQuality => maxQuality;
        public NameEffectView ViewPrefab => viewPrefab;
        public bool HasTextStyle => overrideTextStyle;
        public NameEffectTextStyle TextStyle => textStyle;

        /// <summary>Chất lượng thực tế = min(chỗ hiện yêu cầu, trần của effect).</summary>
        public NameEffectQuality ResolveQuality(NameEffectQuality requested)
        {
            return requested < maxQuality ? requested : maxQuality;
        }

#if UNITY_EDITOR
        /// <summary>Chỉ dùng cho script editor dựng sẵn effect mẫu.</summary>
        public void EditorSetup(int newId, string newDisplayName, NameEffectRarity newRarity,
                                NameEffectView newViewPrefab)
        {
            id = newId;
            displayName = newDisplayName;
            rarity = newRarity;
            viewPrefab = newViewPrefab;
        }
#endif
    }
}
