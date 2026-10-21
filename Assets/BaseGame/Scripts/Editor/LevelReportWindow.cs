using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using Core.GamePlay;
using CoreGame.GameGlobalConfig;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ "Xuất báo cáo level" (Tools/FishSort/Xuất báo cáo level): đọc từng file LevelData_{n}.json trong kho, đo
/// bằng đúng các thước của nút "Đo level đang mở" rồi ghi ra một file .xlsx mỗi level một dòng.
///
/// Cột "≈ Người chơi thắng" là bot như người lỡ tay LevelHumanBot.PlayerMistakeRate (10%), mức đã hiệu chỉnh theo tay
/// người thiết kế. Cột "Mục tiêu" cùng thang đó (LevelHumanBot.PlayerTargetWinRate: Thường 90 / Khó 50 / Siêu khó 20%),
/// không phải thang bot cũ của LevelBands. Bot vẫn bi quan với kiểu level phải "đào" (bể chờ cá chưa lên bàn, vd level 74 / 104) nên cột
/// Solver đứng cạnh: "Giải được" mà bot rất thấp thì nên tự chơi thử.
///
/// File xlsx ghi tay bằng System.IO.Compression (xlsx là một file zip chứa XML) để khỏi kéo thêm thư viện vào project.
/// </summary>
public class LevelReportWindow : EditorWindow
{
    private sealed class Row
    {
        public int Level;
        public string Tag = "";
        public int TotalFish;
        public int Matches;
        public int Orders;
        public int Species;
        public int Bubbles;
        public string Solver = "";
        public float OldBot = -1f;
        public float PlayerWin = -1f;
        public float Target = -1f;
        public string Obstacles = "";
        public string Error = "";
    }

    private int fromLevel = 1;
    private int toLevel;
    private int playouts = 200;
    private bool runSolver = true;
    private string lastPath;
    private string summary;

    [MenuItem("Tools/FishSort/Xuất báo cáo level (xlsx)")]
    public static void Open()
    {
        var window = GetWindow<LevelReportWindow>("Báo cáo level");
        window.minSize = new Vector2(420f, 260f);
        window.Show();
    }

    private void OnEnable()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        if (toLevel <= 0) toLevel = config != null ? Mathf.Max(1, config.LevelMaxInConfig) : 1;
    }

    private void OnGUI()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        if (config == null)
        {
            EditorGUILayout.HelpBox("Không thấy LevelGlobalConfig trong Assets/Resources/GlobalConfig", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField($"Kho: {config.LevelMaxInConfig} level · {config.LevelFolder}", EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);

        fromLevel = Mathf.Clamp(EditorGUILayout.IntField("Từ level", fromLevel), 1, Mathf.Max(1, config.LevelMaxInConfig));
        toLevel = Mathf.Clamp(EditorGUILayout.IntField("Tới level", toLevel), fromLevel, Mathf.Max(fromLevel, config.LevelMaxInConfig));
        playouts = Mathf.Clamp(EditorGUILayout.IntField(
            new GUIContent("Số ván bot / level", "Nhiều ván thì số ổn định hơn nhưng lâu hơn. 200 ≈ sai số ±3%."),
            playouts), 20, 1000);
        runSolver = EditorGUILayout.Toggle(
            new GUIContent("Chạy solver", "Cột Giải được / Chạm trần / Không giải được. Tắt đi thì xuất nhanh hơn."),
            runSolver);

        EditorGUILayout.Space(6f);
        if (GUILayout.Button($"Xuất {toLevel - fromLevel + 1} level ra xlsx", GUILayout.Height(28f))) Export(config);

        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, MessageType.Info);
        if (!string.IsNullOrEmpty(lastPath) && GUILayout.Button("Mở thư mục chứa file")) EditorUtility.RevealInFinder(lastPath);
    }

    private void Export(LevelGlobalConfig config)
    {
        string defaultName = $"LevelReport_{fromLevel}-{toLevel}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string folder = string.IsNullOrEmpty(lastPath) ? Path.GetDirectoryName(Application.dataPath) : Path.GetDirectoryName(lastPath);
        string path = EditorUtility.SaveFilePanel("Lưu báo cáo level", folder, defaultName, "xlsx");
        if (string.IsNullOrEmpty(path)) return;

        var rows = new List<Row>();
        bool cancelled = false;
        try
        {
            for (int level = fromLevel; level <= toLevel; level++)
            {
                float progress = (float)(level - fromLevel) / Mathf.Max(1, toLevel - fromLevel + 1);
                if (EditorUtility.DisplayCancelableProgressBar("Xuất báo cáo level", $"Level {level} / {toLevel}", progress))
                {
                    cancelled = true;
                    break;
                }

                rows.Add(Measure(config, level));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (rows.Count == 0) return;

        try
        {
            WriteXlsx(path, rows);
        }
        catch (Exception e)
        {
            summary = $"Không ghi được file: {e.Message}\n(File đang mở trong Excel thì đóng lại rồi xuất lại.)";
            return;
        }

        lastPath = path;
        int errors = rows.FindAll(r => !string.IsNullOrEmpty(r.Error)).Count;
        summary = $"{(cancelled ? "Đã huỷ giữa chừng - " : "")}Đã xuất {rows.Count} level" +
                  (errors > 0 ? $", {errors} level đọc lỗi (xem cột Ghi chú)" : "") + $"\n{path}";
        EditorUtility.RevealInFinder(path);
    }

    private Row Measure(LevelGlobalConfig config, int level)
    {
        var row = new Row { Level = level };
        string file = config.GetLevelFilePath(level);
        if (!File.Exists(file))
        {
            row.Error = "không có file " + Path.GetFileName(file);
            return row;
        }

        LevelData data;
        try
        {
            data = DataSerializer.Deserialize<LevelData>(File.ReadAllText(file));
        }
        catch (Exception e)
        {
            row.Error = "đọc file lỗi: " + e.Message;
            return row;
        }

        if (data == null)
        {
            row.Error = "giải mã ra null";
            return row;
        }

        row.Tag = data.Difficulty.ToString();
        row.Orders = data.OrderQueue?.Length ?? 0;
        row.Target = LevelHumanBot.PlayerTargetWinRate(level, data.Difficulty);

        var species = new HashSet<int>();
        int ice = 0, hp = 0, hidden = 0, spawner = 0, layered = 0, bundle = 0, locks = 0, rock = 0;
        foreach (SoftBubbleData bubble in data.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>())
        {
            if (bubble == null) continue;
            if (bubble.isRock)
            {
                rock++;
                continue;
            }

            row.Bubbles++;
            if (bubble.isIce) ice++;
            if (bubble.isBubbleHp) hp++;
            if (bubble.isSpawner) spawner++;
            if (bubble.isMultiLayer) layered++;
            if (bubble.isBundleBubble) bundle++;
            if (bubble.isLock) locks++;

            for (int layer = 0; layer < bubble.LayerCount; layer++)
            {
                foreach (FishData fish in bubble.GetLayerFish(layer))
                {
                    if (fish == null) continue;
                    row.TotalFish++;
                    species.Add(fish.FishId);
                    if (fish.isHidden) hidden++;
                }
            }

            if (!bubble.isSpawner || bubble.fishOnSpawner == null) continue;
            foreach (FishData fish in bubble.fishOnSpawner)
            {
                if (fish == null) continue;
                row.TotalFish++;
                species.Add(fish.FishId);
            }
        }

        row.Species = species.Count;
        row.Matches = row.TotalFish / Mathf.Max(1, GamePlayGlobalConfig.TankGoal);
        row.Obstacles = JoinObstacles(("Đá", rock), ("Băng", ice), ("HP", hp), ("Cá ẩn", hidden),
            ("Spawner", spawner), ("Nhiều lớp", layered), ("Cá link", bundle), ("Khoá", locks));

        if (row.TotalFish != row.Orders * GamePlayGlobalConfig.TankGoal)
        {
            row.Error = $"tổng cá {row.TotalFish} ≠ {row.Orders} đơn × {GamePlayGlobalConfig.TankGoal}";
        }

        if (runSolver)
        {
            LevelSolver.Result result = LevelSolver.Solve(data, 30000);
            row.Solver = result.Solved ? "Giải được" : result.NodeCapHit ? "Chạm trần" : "KHÔNG giải được";
        }

        row.OldBot = LevelSolver.EstimateWinRate(data, playouts);
        row.PlayerWin = LevelHumanBot.EstimateWinRate(data, LevelHumanBot.PlayerMistakeRate, playouts);

        // Số thấp thì đo lại bằng nhiều ván hơn: ở 5-10% thì 200 ván không tách được Khó với Siêu khó.
        int more = LevelHumanBot.PlayoutsFor(row.PlayerWin, playouts);
        if (more > playouts) row.PlayerWin = LevelHumanBot.EstimateWinRate(data, LevelHumanBot.PlayerMistakeRate, more);
        return row;
    }

    private static string JoinObstacles(params (string name, int count)[] items)
    {
        var parts = new List<string>();
        foreach ((string name, int count) in items)
        {
            if (count > 0) parts.Add($"{name} {count}");
        }

        return string.Join(" · ", parts);
    }

    // ---------- Ghi xlsx ----------

    private static readonly string[] Headers =
    {
        "Level", "Tag", "Số match (tổng cá / 3)", "Số loài cá", "≈ Người chơi thắng", "Độ khó (100% - thắng)",
        "Mục tiêu (≈ người chơi)", "Solver", "Bot cũ (bấm bừa 15%)", "Số bóng", "Vật cản", "Ghi chú"
    };

    private static readonly float[] Widths = { 8, 11, 22, 11, 19, 20, 15, 17, 20, 9, 42, 36 };

    private static void WriteXlsx(string path, List<Row> rows)
    {
        if (File.Exists(path)) File.Delete(path);
        using FileStream stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>");
        Add(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>");
        Add(zip, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Levels\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">Levels!$A$1:$L$" +
            (rows.Count + 1) + "</definedName></definedNames></workbook>");
        Add(zip, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>");

        // Style: 0 thường, 1 tiêu đề đậm nền xám, 2 phần trăm 0%, 3 phần trăm tô đỏ nhạt (thắng chưa tới nửa mục tiêu).
        Add(zip, "xl/styles.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9D9D9\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF8CBAD\"/></patternFill></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>" +
            "<cellXfs count=\"4\"><xf/>" +
            "<xf fontId=\"1\" fillId=\"2\" applyFont=\"1\" applyFill=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"9\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"9\" fillId=\"3\" applyNumberFormat=\"1\" applyFill=\"1\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>");

        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                     "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                     "<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>" +
                     "<cols>");
        for (int c = 0; c < Widths.Length; c++)
        {
            sheet.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{Widths[c].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }

        sheet.Append("</cols><sheetData>");
        sheet.Append("<row r=\"1\" ht=\"32\" customHeight=\"1\">");
        for (int c = 0; c < Headers.Length; c++) Text(sheet, c, 1, Headers[c], 1);
        sheet.Append("</row>");

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            int r = i + 2;
            bool below = row.PlayerWin >= 0f && row.Target >= 0f && row.PlayerWin < row.Target - Mathf.Max(0.03f, row.Target * 0.5f);
            sheet.Append($"<row r=\"{r}\">");
            Number(sheet, 0, r, row.Level);
            Text(sheet, 1, r, row.Tag);
            Number(sheet, 2, r, row.Matches);
            Number(sheet, 3, r, row.Species);
            Percent(sheet, 4, r, row.PlayerWin, below ? 3 : 2);
            Percent(sheet, 5, r, row.PlayerWin >= 0f ? 1f - row.PlayerWin : -1f, 2);
            Percent(sheet, 6, r, row.Target, 2);
            Text(sheet, 7, r, row.Solver);
            Percent(sheet, 8, r, row.OldBot, 2);
            Number(sheet, 9, r, row.Bubbles);
            Text(sheet, 10, r, row.Obstacles);
            Text(sheet, 11, r, row.Error);
            sheet.Append("</row>");
        }

        sheet.Append($"</sheetData><autoFilter ref=\"A1:L{rows.Count + 1}\"/></worksheet>");
        Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static string Cell(int column, int row) => $"{(char)('A' + column)}{row}";

    private static void Text(StringBuilder sb, int column, int row, string value, int style = 0)
    {
        if (string.IsNullOrEmpty(value)) return;
        string s = style > 0 ? $" s=\"{style}\"" : "";
        sb.Append($"<c r=\"{Cell(column, row)}\" t=\"inlineStr\"{s}><is><t>{SecurityElement.Escape(value)}</t></is></c>");
    }

    private static void Number(StringBuilder sb, int column, int row, int value)
    {
        sb.Append($"<c r=\"{Cell(column, row)}\"><v>{value}</v></c>");
    }

    private static void Percent(StringBuilder sb, int column, int row, float value, int style)
    {
        if (value < 0f) return;
        sb.Append($"<c r=\"{Cell(column, row)}\" s=\"{style}\"><v>{value.ToString("0.###", CultureInfo.InvariantCulture)}</v></c>");
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
