using System;
using System.Collections.Generic;
using Core.GamePlay;
using CoreData;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace CoreGame.GameGlobalConfig
{
    /// <summary>Một dòng của bảng cache độ khó. Không giữ tham chiếu asset nào.</summary>
    [Serializable]
    public class LevelCacheEntry
    {
        public int Level;
        public LevelDifficulty Difficulty;
    }

    /// <summary>
    /// Cấu hình kho level. Nội dung level KHÔNG nằm trong asset này: file LevelData_{n}.json được nạp qua Addressables
    /// (bake trong APK + bản trên CDN, xem LevelAddressableProvider), config chỉ dựng address theo quy ước.
    /// Vượt quá MaxPlayableLevel thì mượn nội dung một level cũ cùng độ khó, xem LevelLoopResolver.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelGlobalConfig", menuName = "GlobalConfigs/LevelGlobalConfig")]
    [GlobalConfig("Assets/Resources/GlobalConfig/")]
    public class LevelGlobalConfig : GlobalConfig<LevelGlobalConfig>
    {
        [field: SerializeField, Min(1), Tooltip("Level đầu tiên được phép dùng làm nguồn khi người chơi vượt MaxPlayableLevel.")]
        public int LoopSourceFromLevel { get; private set; } = 50;

        [field: SerializeField, Min(0), Tooltip("Chừa lại bao nhiêu level cuối, không dùng làm nguồn. Vùng nguồn kết thúc ở MaxPlayableLevel - giá trị này.")]
        public int LoopSourceEndOffset { get; private set; } = 10;

        [field: SerializeField, Min(0), Tooltip("Tránh quay trúng lại N level nguồn gần nhất.")]
        public int LoopAvoidRecentCount { get; private set; } = 10;

        /// <summary>Tiền tố tên file seed: LevelData_{n}.json.</summary>
        public const string LevelFilePrefix = LevelAddress.LevelFilePrefix;

        [field: SerializeField, FolderPath,
                Tooltip("Thư mục chứa LevelData_{n}.json của level production. Tool Level Addressables và Check Tag Level đều đọc chỗ này.")]
        public string LevelFolder { get; private set; } = "Assets/BaseGame/TextAssets/LevelDataConverted";

        public string GetLevelFilePath(int level) => $"{LevelFolder}/{LevelFilePrefix}{level}.json";

        [field: SerializeField] public bool IsDlbEnabled { get; private set; } = true;

        [field: SerializeField, Min(1)] public int DlbCap { get; private set; } = 3;
        [field: SerializeField] public int GeneratorSeed { get; private set; } = 12345;

        [SerializeField, Min(1),
         Tooltip("Level cao nhất được bake vào bản build VÀ đưa lên CDN (1..giá trị này). Nhập tay: thư mục level có thể chứa level đang làm dở số cao hơn.")]
        private int levelMaxInConfig = 150;

        [SerializeField, ListDrawerSettings(NumberOfItemsPerPage = 10),
         Tooltip("Cache độ khó dựng lại từ file level. Chỉ dùng cho tool editor và LevelLoopResolver; gameplay lấy độ khó từ file đã nạp.")]
        private List<LevelCacheEntry> levelCache = new();

        /// <summary>Level tối đa từ Remote Config (level_ingame_config). 0 = chưa nhận.</summary>
        [ShowInInspector, ReadOnly] public int RemoteMaxLevel { get; private set; }

        public int LevelMaxInConfig => levelMaxInConfig;
        public IReadOnlyList<LevelCacheEntry> LevelCache => levelCache;

        [ShowInInspector, ReadOnly]
        public int MaxPlayableLevel
        {
            get
            {
                int currentLevel = Application.isPlaying ? UserData.Instance.CurrentLevel : 0;
                return LevelMaxRule.Resolve(RemoteMaxLevel, levelMaxInConfig, currentLevel);
            }
        }

        /// <summary>Level có nội dung thật của riêng nó (không phải mượn level cũ).</summary>
        public bool IsLevelPlayable(int level) => level >= 1 && level <= MaxPlayableLevel;

        /// <summary>Gọi sau khi RemoteConfigManager.FetchRemote() đã chạy (LoadingManager đảm bảo thứ tự).</summary>
        public void ReloadRemoteConfig()
        {
            RemoteMaxLevel = Mathf.Max(0, LevelRemoteConfigData.Instance.LevelMax);
            Debug.Log($"[Level] RemoteMaxLevel = {RemoteMaxLevel}, MaxPlayableLevel = {MaxPlayableLevel}");
        }

        /// <summary>
        /// Dựng config cho một level, CHƯA nạp data. LevelType ở đây là giá trị tạm theo luật; độ khó thật nằm trong file
        /// và được gán lại trong LevelConfig.LoadLevelDataAsync. retryCount = số lần chơi lại của lượt sẽ nạp (chọn biến thể).
        /// </summary>
        public LevelConfig GetLevelConfig(int level, int retryCount = 0)
        {
            int sourceLevel = LevelLoopResolver.ResolveSourceLevel(level);
            LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;
            return new LevelConfig
            {
                Level = level,
                SourceLevel = sourceLevel,
                RetryCount = retryCount,
                LevelType = rule != null ? rule.GetDifficulty(level) : LevelDifficulty.Normal,
                LevelAddress = LevelAddress.GetLevelAddress(sourceLevel),
            };
        }

        /// <summary>
        /// Độ khó trong bảng cache, đọc được mà không phải nạp file. LevelLoopResolver dùng hàm này để lọc level nguồn.
        /// Trả false khi level không có trong cache (level chỉ có trên CDN, hoặc chưa chạy Rebuild Difficulty Cache).
        /// </summary>
        public bool TryGetCachedDifficulty(int level, out LevelDifficulty difficulty)
        {
            difficulty = LevelDifficulty.Normal;
            if (levelCache == null || level < 1) return false;

            // Cache sắp theo Level tăng dần và thường liền mạch nên thử vị trí level - 1 trước.
            int index = level - 1;
            if (index < levelCache.Count && levelCache[index] != null && levelCache[index].Level == level)
            {
                difficulty = levelCache[index].Difficulty;
                return true;
            }

            for (int i = 0; i < levelCache.Count; i++)
            {
                if (levelCache[i] == null || levelCache[i].Level != level) continue;
                difficulty = levelCache[i].Difficulty;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>Hai đường dẫn thư mục có trỏ cùng một chỗ không (bỏ qua dấu gạch chéo, dấu / cuối, hoa thường).</summary>
        public static bool IsSameFolder(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(NormalizeFolder(a), NormalizeFolder(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFolder(string path) => path.Replace('\\', '/').TrimEnd('/');

        /// <summary>Cập nhật một dòng cache sau khi tool ghi file level production.</summary>
        public void SyncLevelCache(int level, LevelDifficulty difficulty)
        {
            level = Mathf.Max(1, level);
            levelCache ??= new List<LevelCacheEntry>();
            LevelCacheEntry entry = levelCache.Find(e => e != null && e.Level == level);
            if (entry == null)
            {
                entry = new LevelCacheEntry { Level = level };
                levelCache.Add(entry);
                levelCache.Sort((x, y) => x.Level.CompareTo(y.Level));
            }

            entry.Difficulty = difficulty;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>Dựng lại cache cho level 1..levelMaxInConfig từ file. Trả về các level thiếu file hoặc không giải mã được.</summary>
        public List<int> RebuildLevelCacheFromFolder()
        {
            List<int> missing = new List<int>();
            List<LevelCacheEntry> cache = new List<LevelCacheEntry>(levelMaxInConfig);
            for (int level = 1; level <= levelMaxInConfig; level++)
            {
                LevelData data = LevelFileReader.Read(GetLevelFilePath(level));
                if (data == null)
                {
                    missing.Add(level);
                    continue;
                }

                cache.Add(new LevelCacheEntry { Level = level, Difficulty = data.Difficulty });
            }

            levelCache = cache;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            return missing;
        }

        // Asset ScriptableObject sống trong bộ nhớ editor qua các lần Play, nên RemoteMaxLevel (không serialize) phải tự xoá
        // khi vào / ra Play mode; nếu không, giá trị remote của phiên trước làm sai tool và test ở Edit mode.
        [UnityEditor.InitializeOnLoadMethod]
        private static void ResetRemoteMaxOnPlayModeChange()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state != UnityEditor.PlayModeStateChange.EnteredEditMode &&
                    state != UnityEditor.PlayModeStateChange.ExitingEditMode) return;
                if (HasInstanceLoaded) Instance.RemoteMaxLevel = 0;
            };
        }

        [UnityEditor.MenuItem("Tools/FishSort/Level Addressables/Rebuild Difficulty Cache")]
        private static void RebuildLevelCacheMenu()
        {
            LevelGlobalConfig config = Instance;
            List<int> missing = config.RebuildLevelCacheFromFolder();
            Debug.Log($"[LevelCache] rebuilt entries={config.levelCache.Count} max={config.levelMaxInConfig} missing={missing.Count}" +
                      (missing.Count > 0 ? $" ({string.Join(", ", missing)})" : ""));
        }

        [Button("Dựng lại cache độ khó từ file")]
        private void RebuildLevelCacheButton() => RebuildLevelCacheMenu();

        /// <summary>Đếm level nguồn có sẵn theo từng độ khó để biết LoopSourceFromLevel đặt đã hợp lý chưa.</summary>
        [Button("Kiểm tra vùng level nguồn khi vượt MaxPlayableLevel")]
        public void LogLoopSourceRange()
        {
            int maxLevel = MaxPlayableLevel;
            int fromLevel = Mathf.Clamp(LoopSourceFromLevel, 1, maxLevel);
            int toLevel = Mathf.Clamp(maxLevel - LoopSourceEndOffset, fromLevel, maxLevel);
            var byDifficulty = new Dictionary<LevelDifficulty, List<int>>();
            for (int level = fromLevel; level <= toLevel; level++)
            {
                if (!TryGetCachedDifficulty(level, out LevelDifficulty difficulty)) continue;
                if (!byDifficulty.TryGetValue(difficulty, out List<int> levels))
                {
                    levels = new List<int>();
                    byDifficulty[difficulty] = levels;
                }

                levels.Add(level);
            }

            var report = new List<string> { $"Vùng level nguồn: {fromLevel}-{toLevel} (MaxPlayableLevel {maxLevel})" };
            foreach (LevelDifficulty difficulty in (LevelDifficulty[])Enum.GetValues(typeof(LevelDifficulty)))
            {
                List<int> levels = byDifficulty.TryGetValue(difficulty, out List<int> found) ? found : new List<int>();
                report.Add($"{difficulty}: {levels.Count} level" + (levels.Count > 0 ? $" ({string.Join(", ", levels)})" : " — LEVEL VƯỢT MAX Ở ĐỘ KHÓ NÀY SẼ PHẢI LẤY TẠM ĐỘ KHÓ KHÁC"));
            }

            Debug.Log(string.Join("\n", report));
        }

        [Button("Kiểm tra lại toàn bộ seed")]
        public void ValidateAll()
        {
            int ok = 0;
            int fragile = 0;
            for (int level = 1; level <= levelMaxInConfig; level++)
            {
                LevelData data = LevelFileReader.Read(GetLevelFilePath(level));
                if (data == null)
                {
                    Debug.LogWarning($"Level {level}: thiếu file hoặc không giải mã được");
                    continue;
                }

                var errors = new List<string>();
                if (!data.Validate(errors))
                {
                    Debug.LogWarning($"Level {level}: {string.Join("; ", errors)}");
                    continue;
                }

                LevelSolver.Result r = LevelSolver.Solve(data);
                if (r.Solved) ok++;
                else if (r.SolvedIgnoringIce) fragile++;
                string verdict = r.Solved ? "giải được" : r.SolvedIgnoringIce ? "MONG MANH (chỉ giải được nếu coi băng vỡ tự do)" : "KHÔNG giải được";
                Debug.Log(
                    $"Level {level} {data.Difficulty}: {verdict} moves={r.Moves} holding={r.MaxHoldingUsed}/{LevelBands.DefaultHoldingSlots} nodes={r.NodesExplored}");
            }

            Debug.Log($"ValidateAll: {ok}/{levelMaxInConfig} giải được, {fragile} mong manh ở phần băng");
        }
#endif
    }

    [Serializable]
    public class LevelConfig
    {
        /// <summary>Số level hiển thị cho người chơi (vẫn tăng đều 151, 152... dù nội dung là level cũ).</summary>
        public int Level;

        /// <summary>Level thật đang được dùng để nạp nội dung. Khác Level khi đã vượt MaxPlayableLevel.</summary>
        public int SourceLevel;

        /// <summary>Số lần chơi lại của lượt này (lượt đầu = 0), để chọn biến thể LevelData_{n}_{x}.</summary>
        public int RetryCount;

        /// <summary>Biến thể đã nạp (0 = bản gốc). Ghi sau LoadLevelDataAsync; analytics gửi kèm param variant.</summary>
        public int Variant;

        /// <summary>Trước khi nạp: giá trị tạm theo luật. Sau LoadLevelDataAsync: độ khó ghi trong file.</summary>
        public LevelDifficulty LevelType;

        /// <summary>Address Addressables theo quy ước "Levels/Level_{SourceLevel}".</summary>
        public string LevelAddress;

        /// <summary>Nguồn của bản vừa nạp: apk, cdn, test:{Version}, editor-file; biến thể thêm hậu tố /v{x}.</summary>
        public string Source;

        public LevelData LevelData;

        public bool HasLevelAsset => !string.IsNullOrEmpty(LevelAddress);

        public LevelRequest ToRequest() => new LevelRequest(Level, SourceLevel, RetryCount);

        /// <summary>Nạp nội dung level. null khi không có bản nào nạp được; khi đó các field giữ nguyên.</summary>
        public async UniTask<LevelData> LoadLevelDataAsync()
        {
            LevelLoadResult result = await LevelAddressableProvider.LoadLevelDataForRequestAsync(ToRequest());
            if (!result.Success) return null;
            LevelData = result.Data;
            LevelType = result.Data.Difficulty;
            Source = result.Source;
            Variant = result.Variant;
            return LevelData;
        }
    }
}
