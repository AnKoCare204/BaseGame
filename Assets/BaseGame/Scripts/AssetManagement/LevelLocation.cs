using UnityEngine.ResourceManagement.ResourceLocations;

namespace CoreData
{
    /// <summary>Tên nguồn của level đang chơi, ghi vào log và hiện trên cheat panel.</summary>
    public static class LevelSource
    {
        public const string Apk = "apk";
        public const string Cdn = "cdn";
        public const string TestPrefix = "test:";
        public const string EditorFile = "editor-file";
        public const string VariantSeparator = "/v";

        /// <summary>"cdn" + biến thể 3 -> "cdn/v3"; biến thể 0 (bản gốc) giữ nguyên.</summary>
        public static string WithVariant(string source, int variant) => variant > 0 ? $"{source}{VariantSeparator}{variant}" : source;
    }

    /// <summary>Gate đang làm gì, để UI hiện loading phù hợp.</summary>
    public enum LevelGatePhase
    {
        Idle,
        Waiting,
        Downloading,
    }

    /// <summary>
    /// Yêu cầu nạp một level. Level = số hiển thị cho người chơi, SourceLevel = level thật dùng để nạp nội dung
    /// (khác nhau khi đã vượt max và đang mượn level cũ, xem LevelLoopResolver). RetryCount = số lần chơi lại của lượt này
    /// (lượt đầu = 0), dùng để chọn biến thể (xem LevelVariantRule).
    /// </summary>
    public readonly struct LevelRequest
    {
        public readonly int Level;
        public readonly int SourceLevel;
        public readonly int RetryCount;

        public LevelRequest(int level, int sourceLevel, int retryCount = 0)
        {
            Level = level;
            SourceLevel = sourceLevel;
            RetryCount = retryCount;
        }

        public string Address => LevelAddress.GetLevelAddress(SourceLevel);

        /// <summary>Bộ test thay level theo SỐ; level đang mượn nội dung thì không áp bộ test.</summary>
        public bool AllowTest => Level == SourceLevel;

        public bool IsValid => SourceLevel >= 1;
    }

    /// <summary>Kết quả tra một address ở cả hai catalog (APK và CDN).</summary>
    public readonly struct LevelLocation
    {
        /// <summary>Location trong catalog level trên CDN; null nếu catalog chưa nạp hoặc không có address.</summary>
        public readonly IResourceLocation Remote;

        /// <summary>Location trong catalog của APK (level bake); null nếu bản build không có level này.</summary>
        public readonly IResourceLocation Local;

        /// <summary>Bundle remote đã nằm trong cache máy, load không cần mạng.</summary>
        public readonly bool RemoteCached;

        /// <summary>Số biến thể của location này (LevelData_{n}_{x}); 0 = bản gốc hoặc bản test.</summary>
        public readonly int Variant;

        private readonly string testVersion;

        public LevelLocation(IResourceLocation remote, IResourceLocation local, bool remoteCached, string testVersion, int variant = 0)
        {
            Remote = remote;
            Local = local;
            RemoteCached = remoteCached;
            this.testVersion = testVersion;
            Variant = variant;
        }

        /// <summary>Cùng location, gắn số biến thể (tra address biến thể xong mới biết là bậc nào).</summary>
        public LevelLocation WithVariant(int variant) => new LevelLocation(Remote, Local, RemoteCached, testVersion, variant);

        /// <summary>Chỉ bản bake trong app (LevelSourceRule.IsApkOnly): không có bản CDN, không bộ test.</summary>
        public static LevelLocation ApkOnly(IResourceLocation local) => new LevelLocation(null, local, false, "");

        /// <summary>Tên bộ test của location này; "" = level production.</summary>
        public string TestVersion => testVersion ?? "";

        public bool HasAny => Remote != null || Local != null;

        /// <summary>Chơi được NGAY, không cần mạng.</summary>
        public bool IsReadyOffline => RemoteCached || Local != null;

        /// <summary>Bản dùng để chơi: bản server đã tải, rồi bản bake, rồi bản server phải tải.</summary>
        public IResourceLocation Preferred => RemoteCached ? Remote : Local ?? Remote;

        /// <summary>
        /// Bản để thử tiếp khi <paramref name="failed"/> không nạp hoặc không giải mã được. Chỉ có một trường hợp:
        /// bản CDN hỏng mà APK có bản bake. Bản APK hỏng thì không quay sang bản CDN chưa tải (việc đó là của gate).
        /// </summary>
        public IResourceLocation FallbackFor(IResourceLocation failed)
        {
            if (failed == null || failed != Remote) return null;
            return Local;
        }

        public string DescribeSource(IResourceLocation chosen)
        {
            if (chosen == null) return "";
            string source = chosen == Remote
                ? TestVersion.Length > 0 ? LevelSource.TestPrefix + TestVersion : LevelSource.Cdn
                : LevelSource.Apk;
            return LevelSource.WithVariant(source, Variant);
        }
    }

    public readonly struct LevelLoadResult
    {
        public readonly LevelData Data;
        public readonly string Source;

        /// <summary>Biến thể đã nạp; 0 = bản gốc (hoặc bản test).</summary>
        public readonly int Variant;

        public LevelLoadResult(LevelData data, string source, int variant = 0)
        {
            Data = data;
            Source = source ?? "";
            Variant = variant;
        }

        public bool Success => Data != null;
    }
}
