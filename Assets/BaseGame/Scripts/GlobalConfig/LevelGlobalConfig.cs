using System;
using System.Collections.Generic;
using Core.GamePlay;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace CoreGame.GameGlobalConfig
{
    /// <summary>
    /// Cấu hình level của template: độ khó theo chu kỳ. Game mới thêm dữ liệu riêng (file level, Addressables...) vào LevelConfig
    /// và nạp trong Level.OnStartGame.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelGlobalConfig", menuName = "GlobalConfigs/LevelGlobalConfig")]
    [GlobalConfig("Assets/Resources/GlobalConfig/")]
    public class LevelGlobalConfig : GlobalConfig<LevelGlobalConfig>
    {
        [field: SerializeField, Tooltip("Level (bắt đầu từ 1) có độ khó cố định, ưu tiên hơn chu kỳ bên dưới")]
        public List<LevelDifficultyEntry> FixedDifficulties { get; private set; } = new();

        [field: SerializeField, Tooltip("Chu kỳ độ khó lặp lại cho các level không có trong FixedDifficulties (level 1 = phần tử 0)")]
        public List<LevelDifficulty> DifficultyCycle { get; private set; } = new()
        {
            LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.Hard,
            LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.Normal, LevelDifficulty.SuperHard,
        };

        public LevelDifficulty GetDifficulty(int level)
        {
            for (int i = 0; i < FixedDifficulties.Count; i++)
            {
                if (FixedDifficulties[i].Level == level) return FixedDifficulties[i].Difficulty;
            }

            if (DifficultyCycle == null || DifficultyCycle.Count == 0 || level < 1) return LevelDifficulty.Normal;
            return DifficultyCycle[(level - 1) % DifficultyCycle.Count];
        }

        /// <summary>retryCount = số lần chơi lại của lượt sẽ nạp (lượt đầu = 0).</summary>
        public LevelConfig GetLevelConfig(int level, int retryCount = 0)
        {
            return new LevelConfig
            {
                Level = level,
                RetryCount = retryCount,
                LevelType = GetDifficulty(level),
            };
        }

        [Button]
        private void LogDifficulty(int level)
        {
            Debug.Log($"[LevelGlobalConfig] Level {level}: {GetDifficulty(level)}");
        }
    }

    [Serializable]
    public class LevelDifficultyEntry
    {
        [field: SerializeField] public int Level { get; private set; }
        [field: SerializeField] public LevelDifficulty Difficulty { get; private set; }
    }

    public class LevelConfig
    {
        /// <summary>Số level hiển thị cho người chơi.</summary>
        public int Level;

        /// <summary>Số lần chơi lại của lượt này (lượt đầu = 0).</summary>
        public int RetryCount;

        public LevelDifficulty LevelType;
    }
}
