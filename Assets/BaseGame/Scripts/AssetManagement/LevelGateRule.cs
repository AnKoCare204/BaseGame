using System;

namespace CoreData
{
    /// <summary>Gate phải đợi những gì trước khi quyết định.</summary>
    [Flags]
    public enum LevelGateWait
    {
        None = 0,
        RemoteConfig = 1,
        Catalog = 2,
    }

    /// <summary>
    /// Luật đợi của gate, tách thành hàm thuần để test. Nguyên tắc: chỉ đợi khi câu trả lời có thể đổi kết quả.
    /// Level đã có trên máy, Remote Config đã đọc và không có bộ test nào bật thì vào ngay, kể cả khi CDN đang hỏng.
    /// </summary>
    public static class LevelGateRule
    {
        /// <summary>Sau một lần nạp catalog thất bại, gate không thử lại trong khoảng này (về Home và nút Refresh vẫn thử).</summary>
        public const double CatalogRetryBackoffSeconds = 45d;

        public static LevelGateWait Decide(bool online, bool readyOffline, bool remoteConfigRead, bool testActive,
            bool catalogLoaded, bool catalogRetryAllowed)
        {
            if (!online) return LevelGateWait.None;

            LevelGateWait wait = LevelGateWait.None;
            // Chưa đọc Remote Config thì chưa biết có bộ test nào đang bật.
            if (!remoteConfigRead) wait |= LevelGateWait.RemoteConfig;

            bool catalogMatters = !readyOffline || testActive;
            if (catalogMatters && !catalogLoaded && catalogRetryAllowed) wait |= LevelGateWait.Catalog;
            return wait;
        }

        /// <summary>secondsSinceFailure âm = chưa từng thất bại.</summary>
        public static bool IsCatalogRetryAllowed(double secondsSinceFailure)
        {
            return secondsSinceFailure < 0d || secondsSinceFailure >= CatalogRetryBackoffSeconds;
        }
    }
}
