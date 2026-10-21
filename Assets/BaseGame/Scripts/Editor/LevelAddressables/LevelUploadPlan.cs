using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Phạm vi upload. Catalog LUÔN đi kèm vì nó là mục lục của mọi bundle.</summary>
public enum LevelUploadScope
{
    All = 0,
    OriginOnly = 1,
    TestOnly = 2,
}

/// <summary>Một dòng trong kết quả `rclone lsjson`. Tên field phải khớp JSON của rclone để JsonUtility đọc được.</summary>
[Serializable]
public class RemoteFileInfo
{
    public string Path;
    public string Name;
    public long Size;
    public string ModTime;
    public bool IsDir;
}

/// <summary>Hàm thuần quyết định file nào lên CDN và file nào trên CDN là rác. Tách khỏi LevelAddressableSetup để test.</summary>
public static class LevelUploadPlan
{
    /// <summary>Marker ghi lại ServerData được build cho bản nào. Chỉ dùng trên máy, không bao giờ upload.</summary>
    public const string VariantMarkerFileName = "variant.txt";

    public const string CatalogBinFilterArgs = "--include \"catalog_*.bin\"";
    public const string CatalogHashFilterArgs = "--include \"catalog_*.hash\"";

    private const string UploadLogArgs = "--stats-one-line --stats 10s -v";

    public static bool IsCatalog(string fileName)
    {
        return fileName != null && fileName.StartsWith("catalog_", StringComparison.Ordinal);
    }

    public static bool IsBundle(string fileName)
    {
        return fileName != null && fileName.EndsWith(".bundle", StringComparison.Ordinal);
    }

    // Tên bundle do Addressables sinh: {group}_assets_{label}_{hash}.bundle, viết thường.
    // Bundle test chứa "lvl_test"; tên group "levels_test" không chứa chuỗi đó nên không nhầm.
    public static bool IsTestBundle(string fileName)
    {
        return IsBundle(fileName) && fileName.Contains("lvl_test");
    }

    public static bool IsIncluded(string fileName, LevelUploadScope scope)
    {
        if (IsCatalog(fileName)) return true;
        if (!IsBundle(fileName)) return false;
        bool isTest = IsTestBundle(fileName);
        switch (scope)
        {
            case LevelUploadScope.TestOnly: return isTest;
            case LevelUploadScope.OriginOnly: return !isTest;
            default: return true;
        }
    }

    /// <summary>Filter rclone cho bước 1 (bundle). rclone xét filter theo thứ tự, filter cuối loại mọi thứ còn lại.</summary>
    public static string BundleFilterArgs(LevelUploadScope scope)
    {
        switch (scope)
        {
            case LevelUploadScope.TestOnly:
                return "--filter \"+ *lvl_test*.bundle\" --filter \"- *\"";
            case LevelUploadScope.OriginOnly:
                return "--filter \"- *lvl_test*\" --filter \"+ *.bundle\" --filter \"- *\"";
            default:
                return "--filter \"+ *.bundle\" --filter \"- *\"";
        }
    }

    /// <summary>
    /// Ba bước upload, theo đúng thứ tự phải chạy:
    /// 1. Bundle, cache 1 năm immutable (tên có hash nên không bao giờ bị ghi đè).
    /// 2. catalog_*.bin với no-cache.
    /// 3. catalog_*.hash với max-age=300.
    /// Client chỉ tải .bin khi thấy .hash đổi. Nếu .bin cũng được cache ở edge thì có khoảng thời gian client thấy .hash mới
    /// nhưng nhận .bin CŨ, rồi lưu catalog cũ dưới hash mới và bỏ qua cả lần phát hành đó. Vì vậy .bin không cache ở edge,
    /// và .hash lên sau cùng, khi mọi thứ nó trỏ tới đã sẵn sàng.
    /// </summary>
    public static List<string> BuildUploadSteps(string serverDataDir, string remoteDest, LevelUploadScope scope)
    {
        string copy = $"copy \"{serverDataDir}\" \"{remoteDest}\"";
        return new List<string>
        {
            $"{copy} {BundleFilterArgs(scope)} --header-upload \"Cache-Control: public, max-age=31536000, immutable\" {UploadLogArgs}",
            $"{copy} {CatalogBinFilterArgs} --header-upload \"Cache-Control: no-cache\" {UploadLogArgs}",
            $"{copy} {CatalogHashFilterArgs} --header-upload \"Cache-Control: public, max-age=300\" {UploadLogArgs}",
        };
    }

    /// <summary>
    /// Chỉ được dọn CDN khi catalog trên máy CHÍNH LÀ catalog đang chạy trên CDN. ServerData vừa build mà chưa upload
    /// (hoặc build từ commit khác) thì những bundle catalog đang chạy trỏ tới sẽ bị coi nhầm là rác.
    /// </summary>
    public static bool CanClean(string localCatalogHash, string remoteCatalogHash, out string reason)
    {
        string local = (localCatalogHash ?? "").Trim();
        string remote = (remoteCatalogHash ?? "").Trim();
        if (local.Length == 0)
        {
            reason = "Không đọc được hash của catalog trong ServerData. Bấm 'Build Levels' trước.";
            return false;
        }

        if (remote.Length == 0)
        {
            reason = "Không đọc được hash của catalog đang chạy trên CDN, không dám xoá gì.";
            return false;
        }

        if (!string.Equals(local, remote, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Catalog trong ServerData khác catalog đang chạy trên CDN (build xong chưa Upload, hoặc build từ commit khác). " +
                     "Upload bản build này trước, rồi mới dọn.";
            return false;
        }

        reason = "";
        return true;
    }

    public static string ScopeLabel(LevelUploadScope scope)
    {
        switch (scope)
        {
            case LevelUploadScope.OriginOnly: return "CHỈ level gốc";
            case LevelUploadScope.TestOnly: return "CHỈ level test";
            default: return "level gốc + test";
        }
    }

    /// <summary>Chặn build bản này rồi upload nhầm sang bản kia, và chặn upload ServerData của một lần build lỗi.</summary>
    public static bool CanUpload(string builtVariant, string currentVariant, out string reason)
    {
        if (string.IsNullOrEmpty(currentVariant))
        {
            reason = "Build profile hiện tại không có define CHEAT_ONLY hoặc RELEASE_ONLY.";
            return false;
        }

        if (string.IsNullOrEmpty(builtVariant))
        {
            reason = "ServerData chưa rõ được build cho bản nào (thiếu marker). Bấm 'Build Levels' rồi hãy upload.";
            return false;
        }

        if (builtVariant != currentVariant)
        {
            reason = $"ServerData đang là bản {builtVariant}, không khớp bản {currentVariant} hiện tại. Bấm 'Build Levels' để build lại.";
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// File trên CDN không có trong lần build hiện tại VÀ cũ hơn graceDays. Thời gian ân hạn để client còn cầm catalog cũ
    /// (cache Cloudflare 5 phút, phiên đang mở) không bị 404. File không đọc được ngày thì giữ lại cho chắc.
    /// </summary>
    public static List<RemoteFileInfo> FindStale(IEnumerable<RemoteFileInfo> remote, ICollection<string> currentFileNames,
        int graceDays, DateTime nowUtc)
    {
        List<RemoteFileInfo> stale = new List<RemoteFileInfo>();
        if (remote == null) return stale;

        HashSet<string> current = new HashSet<string>(currentFileNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        DateTime cutoff = nowUtc.AddDays(-Math.Max(0, graceDays));

        foreach (RemoteFileInfo file in remote)
        {
            if (file == null || file.IsDir || string.IsNullOrEmpty(file.Path)) continue;
            if (current.Contains(file.Path) || (file.Name != null && current.Contains(file.Name))) continue;
            if (!DateTime.TryParse(file.ModTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime modified))
            {
                continue;
            }

            if (modified.ToUniversalTime() <= cutoff) stale.Add(file);
        }

        stale.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return stale;
    }
}
