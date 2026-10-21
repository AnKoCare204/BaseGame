using System;
using System.Collections.Generic;
using System.IO;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ "Check tag level" (Tools/FishSort/Check Tag Level): soi tag độ khó của cả bộ level và sửa lại cho khớp luật.
///
/// Hai nguồn:
/// - Main Level: LevelData_{n}.json trong LevelGlobalConfig.LevelFolder. Tag nằm ở file (runtime đọc chỗ này) và ở bảng
///   cache của LevelGlobalConfig (tool editor và LevelLoopResolver đọc); tool soi và sửa cả hai.
/// - Test Level: file trong TextAssets/TestLevel/{Version}/. Bộ test KHÔNG có cache, tool chỉ soi và sửa file.
///
/// Luật chuẩn và thao tác sửa đều lấy từ <see cref="LevelDifficultRuleGlobalConfig"/> chứ không chép lại ở đây.
/// </summary>
public class LevelTagWindow : EditorWindow
{
    public enum LevelSource
    {
        MainLevel,
        TestLevel,
    }

    public sealed class Row
    {
        public int Level;

        /// <summary>Thư mục con so với thư mục quét (tên bộ test); "" khi file nằm ngay trong thư mục quét.</summary>
        public string Version = "";

        public string Path;
        public LevelDifficulty Expected;
        public LevelDifficulty InFile;
        public LevelDifficulty InConfig;
        public bool HasConfigEntry;

        /// <summary>Số Level ghi bên trong file, khi nó khác số trên tên file (file chép tay quên sửa).</summary>
        public int LevelInside;

        public string ReadError;

        public bool FileWrong => ReadError == null && InFile != Expected;
        public bool ConfigWrong => ReadError == null && HasConfigEntry && InConfig != Expected;
        public bool Wrong => FileWrong || ConfigWrong;
    }

    private const string AllVersionsLabel = "Tất cả bộ test";

    private LevelSource source = LevelSource.MainLevel;
    private string folder;
    private bool recursive;
    private string[] testVersions = Array.Empty<string>();
    private int testVersionIndex;

    private bool onlyWrong = true;
    private Vector2 scroll;
    private List<Row> rows = new List<Row>();
    private string summary;
    private MessageType summaryType = MessageType.Info;

    [MenuItem("Tools/FishSort/Check Tag Level")]
    public static void Open()
    {
        LevelTagWindow window = GetWindow<LevelTagWindow>("Check Tag Level");
        window.minSize = new Vector2(460f, 380f);
        window.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(folder)) ApplySource();
    }

    /// <summary>Đặt lại thư mục quét theo nguồn và bộ test đang chọn.</summary>
    private void ApplySource()
    {
        rows.Clear();
        summary = null;

        if (source == LevelSource.MainLevel)
        {
            LevelGlobalConfig config = LevelGlobalConfig.Instance;
            folder = config != null ? config.LevelFolder : "";
            recursive = false;
            return;
        }

        testVersions = LevelFileReader.GetTestVersions().ToArray();
        testVersionIndex = Mathf.Clamp(testVersionIndex, 0, testVersions.Length);
        if (testVersionIndex == 0)
        {
            folder = LevelFileReader.TestLevelFolder;
            recursive = true;
        }
        else
        {
            folder = $"{LevelFileReader.TestLevelFolder}/{testVersions[testVersionIndex - 1]}";
            recursive = false;
        }
    }

    private void OnGUI()
    {
        LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;

        EditorGUILayout.Space(4f);
        GUILayout.Label(DescribeRule(rule), SirenixGUIStyles.MultiLineCenteredLabel);
        if (rule != null && GUILayout.Button("Mở LevelDifficultRuleGlobalConfig để sửa luật"))
        {
            Selection.activeObject = rule;
            EditorGUIUtility.PingObject(rule);
        }

        EditorGUILayout.Space(2f);
        EditorGUI.BeginChangeCheck();
        source = (LevelSource)EditorGUILayout.EnumPopup(
            new GUIContent("Nguồn level", "Main Level: level production + cache trong LevelGlobalConfig.\nTest Level: các bộ trong TextAssets/TestLevel, chỉ có file."),
            source);
        if (EditorGUI.EndChangeCheck())
        {
            testVersionIndex = 0;
            ApplySource();
        }

        if (source == LevelSource.TestLevel)
        {
            EditorGUILayout.BeginHorizontal();
            string[] options = new string[testVersions.Length + 1];
            options[0] = AllVersionsLabel;
            Array.Copy(testVersions, 0, options, 1, testVersions.Length);
            EditorGUI.BeginChangeCheck();
            testVersionIndex = EditorGUILayout.Popup("Bộ test", Mathf.Clamp(testVersionIndex, 0, options.Length - 1), options);
            if (EditorGUI.EndChangeCheck()) ApplySource();
            if (GUILayout.Button("↻", GUILayout.Width(28f))) ApplySource();
            EditorGUILayout.EndHorizontal();
        }

        folder = EditorGUILayout.TextField("Thư mục level", folder);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Kiểm tra", GUILayout.Height(26f))) Scan();

        int wrong = CountWrong();
        GUI.enabled = wrong > 0;
        if (GUILayout.Button($"Sửa hết ({wrong})", GUILayout.Height(26f))) FixAll();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        onlyWrong = EditorGUILayout.ToggleLeft("Chỉ hiện level sai tag", onlyWrong);

        if (source == LevelSource.TestLevel)
        {
            EditorGUILayout.HelpBox("Bộ test không có cache trong LevelGlobalConfig: tool chỉ soi và sửa file.", MessageType.None);
        }

        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, summaryType);
        if (rows.Count == 0) return;

        EditorGUILayout.Space(2f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row row in rows)
        {
            if (onlyWrong && !row.Wrong && row.ReadError == null && row.LevelInside == row.Level) continue;

            DrawRow(row);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>Mô tả luật ngay từ các band đang cấu hình, để cái nhãn trên cửa sổ không bao giờ nói khác config.</summary>
    private static string DescribeRule(LevelDifficultRuleGlobalConfig rule)
    {
        if (rule == null || rule.Bands == null || rule.Bands.Count == 0)
        {
            return "Chưa có LevelDifficultRuleGlobalConfig (hoặc chưa khai band nào) - mọi level sẽ bị coi là Normal";
        }

        var parts = new List<string>();
        foreach (LevelDifficultyBand band in rule.Bands)
        {
            if (band == null) continue;
            string range = band.ToLevel <= 0 ? $"{band.FromLevel}+" : $"{band.FromLevel}-{band.ToLevel}";
            string hard = DescribeDigits(band.HardUnitDigits, "Hard");
            string superHard = DescribeDigits(band.SuperHardUnitDigits, "SuperHard");
            string rules = string.Join(", ", TrimEmpty(superHard, hard));
            parts.Add($"{range}: {(rules.Length == 0 ? "Normal hết" : rules)}");
        }

        return "Luật tag (theo hàng đơn vị) · " + string.Join(" · ", parts);
    }

    private static string DescribeDigits(List<int> digits, string label)
    {
        if (digits == null || digits.Count == 0) return "";
        return $"đuôi {string.Join("/", digits)} = {label}";
    }

    private static List<string> TrimEmpty(params string[] values)
    {
        var kept = new List<string>();
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value)) kept.Add(value);
        }

        return kept;
    }

    private void DrawRow(Row row)
    {
        Color previous = GUI.color;
        if (row.ReadError != null || row.Wrong) GUI.color = SirenixGUIStyles.YellowWarningColor;

        EditorGUILayout.BeginHorizontal(SirenixGUIStyles.BoxContainer);
        if (source == LevelSource.TestLevel)
        {
            GUILayout.Label(string.IsNullOrEmpty(row.Version) ? "(thư mục quét)" : row.Version, GUILayout.Width(110f));
        }

        GUILayout.Label($"Level {row.Level}", EditorStyles.boldLabel, GUILayout.Width(74f));

        if (row.ReadError != null)
        {
            GUILayout.Label($"đọc lỗi - {row.ReadError}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
            GUI.color = previous;
            return;
        }

        string file = row.FileWrong ? $"file {row.InFile} → {row.Expected}" : $"file {row.InFile}";
        string config = source == LevelSource.TestLevel
            ? ""
            : !row.HasConfigEntry
                ? "· cache chưa có level này"
                : row.ConfigWrong
                    ? $"· cache {row.InConfig} → {row.Expected}"
                    : "";
        string inside = row.LevelInside == row.Level ? "" : $" · Level bên trong file ghi {row.LevelInside}";
        GUILayout.Label($"{file} {config}{inside}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);

        GUILayout.FlexibleSpace();
        GUI.enabled = row.Wrong;
        if (GUILayout.Button("Sửa", GUILayout.Width(52f))) FixOne(row, true);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        GUI.color = previous;
    }

    private int CountWrong()
    {
        int n = 0;
        foreach (Row row in rows)
        {
            if (row.Wrong) n++;
        }

        return n;
    }

    /// <summary>
    /// Đọc mọi file LevelData_{n}.json trong thư mục và đối chiếu tag với luật. Nguồn Main Level đối chiếu thêm bảng cache
    /// của LevelGlobalConfig. recursive = true quét cả thư mục con, tên thư mục con ghi vào Row.Version.
    /// Thư mục không tồn tại trả danh sách rỗng.
    /// </summary>
    public static List<Row> ScanFolder(string folder, LevelSource source, bool recursive)
    {
        List<Row> found = new List<Row>();
        LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;
        if (rule == null || string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return found;

        LevelGlobalConfig config = source == LevelSource.MainLevel ? LevelGlobalConfig.Instance : null;
        string root = folder.Replace('\\', '/').TrimEnd('/');
        string[] files = Directory.GetFiles(folder, LevelAddress.LevelFilePrefix + "*.json",
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');
            if (!LevelAddress.TryParseLevelFileName(Path.GetFileNameWithoutExtension(path), out int level)) continue;

            string directory = (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');
            string version = directory.Length > root.Length ? directory.Substring(root.Length).TrimStart('/') : "";

            Row row = new Row
            {
                Level = level,
                Version = version,
                Path = path,
                Expected = rule.GetDifficulty(level),
                LevelInside = level,
            };

            LevelData data = LevelFileReader.Read(path);
            if (data == null)
            {
                row.ReadError = "không giải mã được";
            }
            else
            {
                row.InFile = data.Difficulty;
                row.LevelInside = data.Level;
            }

            if (config != null && config.TryGetCachedDifficulty(level, out LevelDifficulty inConfig))
            {
                row.HasConfigEntry = true;
                row.InConfig = inConfig;
            }

            found.Add(row);
        }

        found.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : string.CompareOrdinal(a.Version, b.Version));
        return found;
    }

    /// <summary>
    /// Sửa một dòng. Main Level: ghi file rồi đồng bộ cache (LevelDifficultRuleGlobalConfig.FixLevelDifficulty).
    /// Test Level: chỉ ghi file (FixLevelFileDifficulty). Không lưu asset; nơi gọi tự SaveAssets.
    /// </summary>
    public static bool FixRow(Row row, LevelSource source, out string message)
    {
        if (row == null || row.ReadError != null)
        {
            message = "File không đọc được, không sửa";
            return false;
        }

        LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;
        if (rule == null)
        {
            message = "Không thấy LevelDifficultRuleGlobalConfig";
            return false;
        }

        bool ok = source == LevelSource.MainLevel
            ? rule.FixLevelDifficulty(row.Level, out message)
            : rule.FixLevelFileDifficulty(row.Path, row.Level, out message);
        if (!ok) return false;

        row.InFile = row.Expected;
        if (source == LevelSource.MainLevel)
        {
            row.InConfig = row.Expected;
            row.HasConfigEntry = true;
        }

        return true;
    }

    private void Scan()
    {
        summary = null;
        if (!Directory.Exists(folder))
        {
            rows = new List<Row>();
            SetSummary($"Không thấy thư mục {folder}", MessageType.Error);
            return;
        }

        if (LevelDifficultRuleGlobalConfig.Instance == null)
        {
            rows = new List<Row>();
            SetSummary("Không thấy LevelDifficultRuleGlobalConfig trong Assets/Resources/GlobalConfig", MessageType.Error);
            return;
        }

        rows = ScanFolder(folder, source, recursive);

        int wrong = CountWrong();
        int unreadable = 0, renamed = 0;
        foreach (Row row in rows)
        {
            if (row.ReadError != null) unreadable++;
            else if (row.LevelInside != row.Level) renamed++;
        }

        string extra = (unreadable > 0 ? $", {unreadable} file đọc lỗi" : "") +
                       (renamed > 0 ? $", {renamed} file có số Level bên trong khác tên file" : "");
        SetSummary(wrong == 0
                ? $"Quét {rows.Count} level: tag đúng hết{extra}"
                : $"Quét {rows.Count} level: {wrong} level sai tag{extra}",
            wrong == 0 && unreadable == 0 && renamed == 0 ? MessageType.Info : MessageType.Warning);
    }

    private void FixAll()
    {
        int fixedCount = 0;
        var failed = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!rows[i].Wrong) continue;
            if (FixOne(rows[i], false)) fixedCount++;
            else failed.Add(string.IsNullOrEmpty(rows[i].Version) ? $"level {rows[i].Level}" : $"{rows[i].Version}/{rows[i].Level}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetSummary($"Đã sửa {fixedCount} level" +
                   (failed.Count > 0 ? $", {failed.Count} level không sửa được ({string.Join(", ", failed)}) - xem Console" : "") +
                   ". Bấm Kiểm tra lại để xác nhận.",
            failed.Count > 0 ? MessageType.Warning : MessageType.Info);
    }

    private bool FixOne(Row row, bool saveNow)
    {
        if (!FixRow(row, source, out string message))
        {
            Debug.LogError($"[Check Tag Level] Level {row.Level}: {message}");
            SetSummary($"Level {row.Level}: {message}", MessageType.Error);
            return false;
        }

        if (saveNow)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetSummary($"Đã sửa level {row.Level} thành {row.Expected}.", MessageType.Info);
        }

        return true;
    }

    private void SetSummary(string message, MessageType type)
    {
        summary = message;
        summaryType = type;
        Repaint();
    }
}
