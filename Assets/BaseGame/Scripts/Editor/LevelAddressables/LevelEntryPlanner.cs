using System.Collections.Generic;
using System.IO;
using CoreData;

public enum LevelFileKind
{
    /// <summary>Level production trong dải 1..max: thành entry của Levels_Main.</summary>
    Main,

    /// <summary>Level production số lớn hơn max (đang làm dở): không bake, không upload.</summary>
    MainAboveMax,

    /// <summary>File trong TestLevel/{Version}/: thành entry của Levels_Test.</summary>
    Test,

    /// <summary>File đặt thẳng ở gốc TestLevel/: bỏ qua, bộ test phải nằm trong thư mục con.</summary>
    TestAtRoot,

    /// <summary>Biến thể LevelData_{n}_{x} với 3 ≤ n ≤ max: entry của Levels_Main, chung chunk với level n.</summary>
    Variant,

    /// <summary>Biến thể của level số lớn hơn max: không bake, không upload.</summary>
    VariantAboveMax,

    /// <summary>Biến thể của level 1-2 (FTUE, luôn chơi bản trong app): game không dùng, không đóng gói.</summary>
    VariantApkOnly,

    /// <summary>Tên không theo dạng LevelData_{n}, hoặc nằm sâu hơn một cấp thư mục.</summary>
    BadName,
}

public struct LevelFileClass
{
    public LevelFileKind Kind;
    public int Level;
    public int Variant;
    public string Version;
    public string Address;
    public string Label;
}

/// <summary>Hàm thuần phân loại file level. Tách khỏi LevelAddressableSetup để test mà không đụng Addressables.</summary>
public static class LevelEntryPlanner
{
    public static LevelFileClass ClassifyMain(string fileNameWithoutExtension, int maxLevel)
    {
        if (!LevelAddress.TryParseLevelFileName(fileNameWithoutExtension, out int level))
        {
            return new LevelFileClass { Kind = LevelFileKind.BadName };
        }

        if (level > maxLevel)
        {
            return new LevelFileClass { Kind = LevelFileKind.MainAboveMax, Level = level };
        }

        return new LevelFileClass
        {
            Kind = LevelFileKind.Main,
            Level = level,
            Version = "",
            Address = LevelAddress.GetLevelAddress(level),
            Label = LevelAddress.GetChunkLabel(level),
        };
    }

    /// <summary>relativePath tính từ thư mục TestLevel, ví dụ "VersionA/LevelData_5.json".</summary>
    public static LevelFileClass ClassifyTest(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return new LevelFileClass { Kind = LevelFileKind.BadName };

        string[] parts = relativePath.Replace('\\', '/').Trim('/').Split('/');
        string fileName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
        bool nameOk = LevelAddress.TryParseLevelFileName(fileName, out int level);

        if (parts.Length == 1)
        {
            return new LevelFileClass { Kind = nameOk ? LevelFileKind.TestAtRoot : LevelFileKind.BadName, Level = level };
        }

        string version = LevelAddress.SanitizeVersion(parts[0]);
        if (parts.Length != 2 || !nameOk || version.Length == 0 || version != parts[0])
        {
            return new LevelFileClass { Kind = LevelFileKind.BadName };
        }

        return new LevelFileClass
        {
            Kind = LevelFileKind.Test,
            Level = level,
            Version = version,
            Address = LevelAddress.GetTestAddress(level, version),
            Label = LevelAddress.GetTestLabel(version),
        };
    }

    /// <summary>File ngay trong thư mục biến thể (LevelAddress.VariantFolder), ví dụ "LevelData_25_3".</summary>
    public static LevelFileClass ClassifyVariant(string fileNameWithoutExtension, int maxLevel)
    {
        if (!LevelAddress.TryParseVariantFileName(fileNameWithoutExtension, out int level, out int variant))
        {
            return new LevelFileClass { Kind = LevelFileKind.BadName };
        }

        if (LevelSourceRule.IsApkOnly(level))
        {
            return new LevelFileClass { Kind = LevelFileKind.VariantApkOnly, Level = level, Variant = variant };
        }

        if (level > maxLevel)
        {
            return new LevelFileClass { Kind = LevelFileKind.VariantAboveMax, Level = level, Variant = variant };
        }

        return new LevelFileClass
        {
            Kind = LevelFileKind.Variant,
            Level = level,
            Variant = variant,
            Version = "",
            Address = LevelAddress.GetVariantAddress(level, variant),
            Label = LevelAddress.GetChunkLabel(level),
        };
    }

    /// <summary>
    /// Thông báo khi còn scene chưa lưu; null khi không có scene nào. Build Levels dừng lại với thông báo này thay vì để
    /// Addressables mở hộp thoại "Unsaved Scenes" (hộp thoại chặn cứng editor khi điều khiển từ xa).
    /// </summary>
    public static string DescribeUnsavedScenes(IList<string> dirtySceneNames)
    {
        if (dirtySceneNames == null || dirtySceneNames.Count == 0) return null;
        List<string> names = new List<string>();
        foreach (string name in dirtySceneNames)
        {
            names.Add(string.IsNullOrEmpty(name) ? "(chưa đặt tên)" : name);
        }

        return $"Scene chưa lưu: {string.Join(", ", names)}. Lưu (hoặc bỏ thay đổi) rồi chạy lại Build Levels.";
    }

    /// <summary>Các level trong 1..maxLevel không có file, theo thứ tự tăng dần.</summary>
    public static List<int> FindMissingLevels(IEnumerable<int> presentLevels, int maxLevel)
    {
        HashSet<int> present = new HashSet<int>();
        if (presentLevels != null)
        {
            foreach (int level in presentLevels) present.Add(level);
        }

        List<int> missing = new List<int>();
        for (int level = 1; level <= maxLevel; level++)
        {
            if (!present.Contains(level)) missing.Add(level);
        }

        return missing;
    }
}
