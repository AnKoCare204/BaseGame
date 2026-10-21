using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Bảng tra id → effect. Nằm cùng chỗ với các GlobalConfig khác
    /// (Assets/Resources/GlobalConfig) nên truy cập được ở mọi scene.
    /// </summary>
    [GlobalConfig("Assets/Resources/GlobalConfig/")]
    public class NameEffectDatabase : GlobalConfig<NameEffectDatabase>
    {
        [Title("Name Effects")]
        [SerializeField] private List<NameEffectDefinition> effects = new List<NameEffectDefinition>();

        [Title("Runtime")]
        [Tooltip("Id dùng khi client chưa có effect mà server trả về (bản cũ gặp effect mới). -1 = không vẽ gì.")]
        [SerializeField] private int fallbackId = 0;

        [Tooltip("Số effect được chạy animation cùng lúc. 0 = không giới hạn.")]
        [SerializeField] private int maxConcurrentAnimated = 8;

        private Dictionary<int, NameEffectDefinition> map;

        public IReadOnlyList<NameEffectDefinition> All => effects;

        public int MaxConcurrentAnimated => maxConcurrentAnimated;

        /// <summary>
        /// Tra effect theo id, tự rơi về <see cref="fallbackId"/> khi không có.
        /// Trả <c>null</c> nghĩa là không vẽ gì — hợp lệ, không phải lỗi.
        /// </summary>
        public NameEffectDefinition Get(int id)
        {
            EnsureMap();

            if (map.TryGetValue(id, out NameEffectDefinition definition))
                return definition;

            if (id != fallbackId && map.TryGetValue(fallbackId, out definition))
                return definition;

            return null;
        }

        public bool TryGet(int id, out NameEffectDefinition definition)
        {
            definition = Get(id);
            return definition != null;
        }

        public bool Contains(int id)
        {
            EnsureMap();
            return map.ContainsKey(id);
        }

        private void EnsureMap()
        {
            if (map != null)
                return;

            map = new Dictionary<int, NameEffectDefinition>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
            {
                NameEffectDefinition definition = effects[i];
                if (definition == null)
                    continue;

                // Trùng id: giữ cái đầu tiên để hành vi không đổi theo thứ tự list.
                if (!map.ContainsKey(definition.Id))
                    map[definition.Id] = definition;
                else
                    Debug.LogWarning($"[NameEffect] Trùng id {definition.Id} giữa '{map[definition.Id].name}' và '{definition.name}'.");
            }
        }

        private void OnValidate()
        {
            map = null;
            if (maxConcurrentAnimated < 0)
                maxConcurrentAnimated = 0;
        }

#if UNITY_EDITOR
        /// <summary>Chỉ dùng cho script editor dựng sẵn effect mẫu.</summary>
        public void EditorRegister(NameEffectDefinition definition)
        {
            if (definition == null || effects.Contains(definition))
                return;

            effects.Add(definition);
            map = null;
        }
#endif
    }
}
