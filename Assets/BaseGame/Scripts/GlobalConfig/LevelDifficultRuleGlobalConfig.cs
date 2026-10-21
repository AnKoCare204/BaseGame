using System;
using System.Collections.Generic;
using System.IO;
using Core.GamePlay;
using CoreData;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace CoreGame.GameGlobalConfig
{
    /// <summary>
    /// Một khoảng level cùng luật độ khó. Độ khó xét theo HÀNG ĐƠN VỊ của số level
    /// (ví dụ band 1..50 với HardUnitDigits = {5} thì 15, 25, 35... là Hard).
    /// SuperHard được ưu tiên trước Hard khi một chữ số nằm ở cả hai danh sách.
    /// </summary>
    [Serializable]
    public class LevelDifficultyBand
    {
        [field: SerializeField, Min(1)] public int FromLevel { get; private set; } = 1;

        /// <summary>Level cuối của band. Để 0 = không giới hạn (band phủ mọi level lớn hơn FromLevel).</summary>
        [field: SerializeField, Min(0)] public int ToLevel { get; private set; } = 50;

        [field: SerializeField, Tooltip("Hàng đơn vị của level -> Hard. Ví dụ {5} nghĩa là 5, 15, 25, 35...")]
        public List<int> HardUnitDigits { get; private set; } = new();

        [field: SerializeField, Tooltip("Hàng đơn vị của level -> SuperHard. {0} nghĩa là các level chia hết cho 10.")]
        public List<int> SuperHardUnitDigits { get; private set; } = new();

        public LevelDifficultyBand() { }

        public LevelDifficultyBand(int fromLevel, int toLevel, List<int> hardUnitDigits, List<int> superHardUnitDigits)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
            HardUnitDigits = hardUnitDigits ?? new List<int>();
            SuperHardUnitDigits = superHardUnitDigits ?? new List<int>();
        }

        public bool Contains(int level) => level >= FromLevel && (ToLevel <= 0 || level <= ToLevel);

        /// <summary>Độ khó của level theo luật của riêng band này (không kiểm tra level có nằm trong band hay không).</summary>
        public LevelDifficulty GetDifficulty(int level)
        {
            int unitDigit = level % 10;
            if (SuperHardUnitDigits != null && SuperHardUnitDigits.Contains(unitDigit)) return LevelDifficulty.SuperHard;
            if (HardUnitDigits != null && HardUnitDigits.Contains(unitDigit)) return LevelDifficulty.Hard;
            return LevelDifficulty.Normal;
        }
    }

    /// <summary>
    /// Luật quy đổi "số level người chơi đang ở" -> độ khó mong muốn. Dùng cho các level vượt quá kho level thật
    /// (xem <see cref="LevelLoopResolver"/>): biết được level 104 phải là Hard thì mới random được một level cũ cùng Hard để chơi.
    /// Level 1..MaxLevel mặc định vẫn giữ Difficulty ghi sẵn trong LevelData; muốn ép chúng theo luật này thì bấm nút
    /// "Sửa tag độ khó toàn bộ level theo rule" (chỉ đổi nhãn, không đụng nội dung màn).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDifficultRuleGlobalConfig", menuName = "GlobalConfigs/LevelDifficultRuleGlobalConfig")]
    [GlobalConfig("Assets/Resources/GlobalConfig/")]
    public class LevelDifficultRuleGlobalConfig : GlobalConfig<LevelDifficultRuleGlobalConfig>
    {
        /// <summary>
        /// Đây là nơi DUY NHẤT giữ luật tag độ khó theo số level (LevelBands.Difficulty cũ đã xoá).
        /// Bộ band mặc định chép đúng luật đang dùng của bộ level để bấm nút sửa không làm lệch các màn đã thiết kế:
        /// dưới 9 luôn Normal; level 10 là Hard chứ không phải SuperHard (màn SuperHard đầu tiên của bộ là level 20);
        /// 11..70 đuôi 5 = Hard; trên 70 đuôi 4/7 = Hard; đuôi 0 = SuperHard.
        /// </summary>
        [field: SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
        public List<LevelDifficultyBand> Bands { get; private set; } = new()
        {
            new LevelDifficultyBand(1, 8, new List<int>(), new List<int>()),
            new LevelDifficultyBand(9, 10, new List<int> { 5, 0 }, new List<int>()),
            new LevelDifficultyBand(11, 70, new List<int> { 5 }, new List<int> { 0 }),
            new LevelDifficultyBand(71, 0, new List<int> { 4, 7 }, new List<int> { 0 }),
        };

        /// <summary>Độ khó mà level này PHẢI có theo rule. Band đầu tiên phủ level quyết định; không band nào phủ thì lấy band cuối.</summary>
        public LevelDifficulty GetDifficulty(int level)
        {
            if (Bands == null || Bands.Count == 0) return LevelDifficulty.Normal;
            for (int i = 0; i < Bands.Count; i++)
            {
                if (Bands[i] != null && Bands[i].Contains(level)) return Bands[i].GetDifficulty(level);
            }

            LevelDifficultyBand last = Bands[Bands.Count - 1];
            return last?.GetDifficulty(level) ?? LevelDifficulty.Normal;
        }

        /// <summary>
        /// Tag độ khó của level trong cache của LevelGlobalConfig đã khớp rule chưa.
        /// Trả về kết quả CHECK (false = đang sai), còn <paramref name="autoFix"/> = true thì sửa luôn cho đúng
        /// (ghi cả entry trong LevelGlobalConfig lẫn file LevelData_{n}.json). Việc sửa chỉ chạy trong Editor.
        /// Level nằm ngoài kho hoặc entry trống thì coi như không có gì để kiểm, trả về true.
        /// </summary>
        public bool IsLevelDifficultyMatched(int level, bool autoFix = true)
        {
            LevelGlobalConfig levelConfig = LevelGlobalConfig.Instance;
            if (levelConfig == null) return true;
            if (!levelConfig.TryGetCachedDifficulty(level, out LevelDifficulty current)) return true;

            LevelDifficulty wanted = GetDifficulty(level);
            if (current == wanted) return true;

#if UNITY_EDITOR
            if (autoFix && !FixLevelDifficulty(level, out string message))
            {
                Debug.LogError($"[LevelDifficultRule] Level {level}: {current} -> {wanted} sửa thất bại - {message}");
            }
#endif
            return false;
        }

        [Button("Xem thử 20 level tiếp theo sau MaxLevel")]
        private void PreviewAfterMaxLevel()
        {
            int maxLevel = LevelGlobalConfig.Instance != null ? LevelGlobalConfig.Instance.LevelMaxInConfig : 100;
            var lines = new List<string>();
            for (int level = maxLevel + 1; level <= maxLevel + 20; level++)
            {
                lines.Add($"Level {level} -> {GetDifficulty(level)}");
            }

            Debug.Log(string.Join("\n", lines));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ép tag độ khó của một level production về đúng rule: ghi lại LevelData_{n}.json (file là nguồn sự thật, runtime
        /// đọc Difficulty từ đó) rồi đồng bộ dòng cache trong LevelGlobalConfig. Chỉ đổi NHÃN, không đụng nội dung màn.
        /// </summary>
        public bool FixLevelDifficulty(int level, out string message)
        {
            LevelGlobalConfig levelConfig = LevelGlobalConfig.Instance;
            if (levelConfig == null)
            {
                message = "Không thấy LevelGlobalConfig";
                return false;
            }

            if (level < 1 || level > levelConfig.LevelMaxInConfig)
            {
                message = $"Level {level} nằm ngoài kho level (1..{levelConfig.LevelMaxInConfig})";
                return false;
            }

            if (!FixLevelFileDifficulty(levelConfig.GetLevelFilePath(level), level, out message)) return false;
            levelConfig.SyncLevelCache(level, GetDifficulty(level));
            return true;
        }

        /// <summary>
        /// Ép tag độ khó của MỘT FILE level về đúng rule của số level đó. Dùng được cho cả file của bộ test
        /// (TestLevel/{Version}/LevelData_{n}.json), khi đó không có cache nào để đồng bộ.
        /// </summary>
        public bool FixLevelFileDifficulty(string path, int level, out string message)
        {
            LevelData data = LevelFileReader.Read(path);
            if (data == null)
            {
                message = $"Không đọc được {path}";
                return false;
            }

            data.EditorSetDifficulty(GetDifficulty(level));
            if (!LevelFileReader.Write(path, data, out message)) return false;
            // File ngoài thư mục Assets (test ghi vào Temp/) thì AssetDatabase không quản lý, không import.
            if (path.Replace('\\', '/').StartsWith("Assets/")) UnityEditor.AssetDatabase.ImportAsset(path);
            return true;
        }

        [Button("Kiểm tra tag độ khó toàn bộ level (chỉ log, không sửa)")]
        private void CheckAllLevelDifficulty()
        {
            LevelGlobalConfig levelConfig = LevelGlobalConfig.Instance;
            if (levelConfig == null)
            {
                Debug.LogError("[LevelDifficultRule] Không thấy LevelGlobalConfig");
                return;
            }

            var mismatched = new List<string>();
            for (int level = 1; level <= levelConfig.LevelMaxInConfig; level++)
            {
                if (!levelConfig.TryGetCachedDifficulty(level, out LevelDifficulty current)) continue;
                LevelDifficulty wanted = GetDifficulty(level);
                if (current != wanted) mismatched.Add($"Level {level}: {current} -> {wanted}");
            }

            Debug.Log(mismatched.Count == 0
                ? $"[LevelDifficultRule] {levelConfig.LevelMaxInConfig} level đều khớp rule."
                : $"[LevelDifficultRule] {mismatched.Count} level lệch rule:\n{string.Join("\n", mismatched)}");
        }

        [Button("Sửa tag độ khó toàn bộ level theo rule")]
        private void FixAllLevelDifficulty()
        {
            LevelGlobalConfig levelConfig = LevelGlobalConfig.Instance;
            if (levelConfig == null)
            {
                Debug.LogError("[LevelDifficultRule] Không thấy LevelGlobalConfig");
                return;
            }

            var fixedLevels = new List<string>();
            var failed = new List<string>();
            for (int level = 1; level <= levelConfig.LevelMaxInConfig; level++)
            {
                if (!levelConfig.TryGetCachedDifficulty(level, out LevelDifficulty current)) continue;
                LevelDifficulty wanted = GetDifficulty(level);
                if (current == wanted) continue;

                if (FixLevelDifficulty(level, out string message)) fixedLevels.Add($"Level {level}: {current} -> {wanted}");
                else failed.Add($"Level {level}: {message}");
            }

            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"[LevelDifficultRule] Đã sửa {fixedLevels.Count} level" +
                      (fixedLevels.Count > 0 ? $":\n{string.Join("\n", fixedLevels)}" : "") +
                      (failed.Count > 0 ? $"\nLỗi {failed.Count} level:\n{string.Join("\n", failed)}" : ""));
        }
#endif
    }
}
