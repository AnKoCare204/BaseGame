namespace CoreData
{
    /// <summary>
    /// Địa chỉ CDN của level: Cloudflare R2, bucket và tên miền riêng của FishSort, mọi thứ nằm dưới prefix FishSort/.
    /// Chỗ DUY NHẤT khai báo host / prefix / tên catalog; runtime (LevelCatalog) và editor (LevelAddressableSetup) dùng chung.
    /// Variant lấy từ define của Build Profile đang active: bản Cheat và bản Release đọc hai thư mục khác nhau.
    /// </summary>
    public static class LevelCdn
    {
#if CHEAT_ONLY && RELEASE_ONLY
#error CHEAT_ONLY va RELEASE_ONLY khong duoc cung bat: ban Release se doc nham thu muc CDN cua ban Cheat.
#endif

        public const string Host = "https://cdn-fishyjam.abi-games.com";
        public const string GamePrefix = "FishSort";
        public const string BucketName = "fishy-jam-levels";

        /// <summary>
        /// Tên remote trong rclone.conf của máy dev. Cố ý KHÔNG dùng "r2": remote đó giữ token của game khác trên cùng máy,
        /// ghi đè lên nó sẽ làm hỏng upload của game kia.
        /// </summary>
        public const string RcloneRemote = "r2fishsort";

        /// <summary>Gốc bucket theo cú pháp rclone.</summary>
        public const string RemoteBucket = RcloneRemote + ":" + BucketName;

        /// <summary>Player version của layout Levels; Addressables đặt tên catalog là catalog_{CatalogVersion}.bin.</summary>
        public const string CatalogVersion = "levels";

        public const string CatalogFileName = "catalog_" + CatalogVersion + ".bin";
        public const string CatalogHashFileName = "catalog_" + CatalogVersion + ".hash";
        public const string VariantCheat = "Cheat";
        public const string VariantRelease = "Release";

#if CHEAT_ONLY
        public const string Variant = VariantCheat;
#else
        public const string Variant = VariantRelease;
#endif

#if UNITY_IOS
        public const string Platform = "iOS";
#else
        public const string Platform = "Android";
#endif

        public static string CatalogUrl => BuildCatalogUrl(Variant, Platform);

        public static string BuildCatalogUrl(string variant, string platform)
        {
            return $"{Host}/{GamePrefix}/{variant}/{platform}/{CatalogFileName}";
        }

        /// <summary>Giá trị biến Remote.LoadPath của profile Addressables; [BuildTarget] do Addressables thay lúc build.</summary>
        public static string BuildRemoteLoadPath(string variant)
        {
            return $"{Host}/{GamePrefix}/{variant}/[BuildTarget]";
        }

        /// <summary>Đích upload theo cú pháp rclone, cho một variant và một platform.</summary>
        public static string BuildRemoteDest(string variant, string platform)
        {
            return $"{RemoteBucket}/{BuildUploadPrefix(variant)}/{platform}";
        }

        /// <summary>Thư mục trong bucket, chưa gồm platform.</summary>
        public static string BuildUploadPrefix(string variant)
        {
            return $"{GamePrefix}/{variant}";
        }
    }
}
