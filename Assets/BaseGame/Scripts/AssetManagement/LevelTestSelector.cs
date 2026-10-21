using System.Collections.Generic;
using UnityEngine;

namespace CoreData
{
    /// <summary>
    /// Trả lời "bộ level test nào đang bật". Thứ tự ưu tiên: ghi đè của cheat panel, rồi tới Remote Config.
    /// Ghi đè chỉ tồn tại trong bản Cheat và trong Editor; bản Release luôn theo Remote Config.
    /// </summary>
    public static class LevelTestSelector
    {
        public const string OverrideKey = "cheat_level_test_override";

        /// <summary>Không ghi đè, theo Remote Config.</summary>
        public const string OverrideRemote = "";

        /// <summary>Ép chơi bản production dù Remote Config đang bật bộ test.</summary>
        public const string OverrideOff = "off";

        /// <summary>Hàm thuần. Trả tên Version đang bật; "" = không dùng bộ test.</summary>
        public static string Resolve(string cheatOverride, bool remoteEnabled, string remoteVersion)
        {
            string choice = cheatOverride ?? OverrideRemote;
            if (choice == OverrideOff) return "";
            if (choice != OverrideRemote) return LevelAddress.SanitizeVersion(choice);
            return remoteEnabled ? LevelAddress.SanitizeVersion(remoteVersion) : "";
        }

        /// <summary>
        /// Lựa chọn đã lưu có còn dùng được không. Version đã bị xoá khỏi CDN (hoặc khỏi thư mục TestLevel) thì trả về
        /// OverrideRemote để dropdown của cheat không kẹt ở một bộ không tồn tại.
        /// </summary>
        /// authoritative = false khi danh sách chưa đáng tin (catalog chưa nạp được): "chưa biết" không phải là "đã xoá",
        /// nên giữ nguyên lựa chọn đã lưu.
        public static string NormalizeOverride(string cheatOverride, IList<string> availableVersions, bool authoritative = true)
        {
            string choice = cheatOverride ?? OverrideRemote;
            if (choice == OverrideRemote || choice == OverrideOff) return choice;
            if (!authoritative) return choice;
            if (availableVersions != null && availableVersions.Contains(choice)) return choice;
            return OverrideRemote;
        }

        /// <summary>Giá trị ghi đè theo đúng thứ tự option của dropdown cheat: Remote config, Off, rồi từng Version.</summary>
        /// keep = lựa chọn đang lưu; nếu đó là một Version chưa có trong danh sách (catalog chưa nạp) thì vẫn thêm vào để
        /// dropdown hiện đúng thứ đang có hiệu lực.
        public static List<string> BuildCheatChoices(IList<string> availableVersions, string keep = null)
        {
            List<string> choices = new List<string> { OverrideRemote, OverrideOff };
            if (availableVersions != null)
            {
                foreach (string version in availableVersions)
                {
                    if (string.IsNullOrEmpty(version) || choices.Contains(version)) continue;
                    choices.Add(version);
                }
            }

            if (!string.IsNullOrEmpty(keep) && !choices.Contains(keep)) choices.Add(keep);
            return choices;
        }

        /// <summary>Version có trong danh sách bộ test đang có không. So khớp chính xác, phân biệt hoa thường (tên thư mục).</summary>
        public static bool IsVersionAvailable(string version, IList<string> availableVersions)
        {
            return !string.IsNullOrEmpty(version) && availableVersions != null && availableVersions.Contains(version);
        }

        public static string DescribeChoice(string choice)
        {
            if (string.IsNullOrEmpty(choice)) return "Remote config";
            return choice == OverrideOff ? "Off" : choice;
        }

        public static string CheatOverride
        {
            get
            {
#if CHEAT_ONLY || UNITY_EDITOR
                return PlayerPrefs.GetString(OverrideKey, OverrideRemote);
#else
                return OverrideRemote;
#endif
            }
            set
            {
#if CHEAT_ONLY || UNITY_EDITOR
                PlayerPrefs.SetString(OverrideKey, value ?? OverrideRemote);
                PlayerPrefs.Save();
#endif
            }
        }

        /// <summary>Version đang bật cho phiên chơi này; "" = không có. Chỉ gọi trong Play mode (đụng tới RemoteConfigManager).</summary>
        public static string ActiveVersion
        {
            get
            {
                TestLevelRemoteConfigData remote = RemoteConfigManager.Instance.TestLevelRemoteConfigData;
                bool enabled = remote != null && remote.IsEnabled;
                string version = remote != null ? remote.TestVersion : "";
                return Resolve(CheatOverride, enabled, version);
            }
        }
    }
}
