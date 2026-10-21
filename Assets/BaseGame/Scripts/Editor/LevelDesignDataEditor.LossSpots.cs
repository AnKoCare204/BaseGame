using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// "Hay thua ở đâu": nút "Đo level đang mở" cho bot như người (lỡ tay ≈ người chơi) chơi nhiều ván, soi từng ván thua
/// ngay lúc kẹt (LevelHumanBot.AnalyzeLosses), rồi tô CAM lên lưới: ô đơn hay "giết" người chơi ở cột OrderQueue, và
/// bóng đang giữ cá bể cần mà người chơi không lấy ra được (băng, vỏ HP, cá ẩn, hàng chờ spawner, lớp trong, đàn cá
/// link không lọt). Trong các bóng đó, con cá đúng loài của đơn hay thua cũng được tô cam.
///
/// Màu: đỏ = lỗi của nút Check, xanh lá = ô "tìm cá", cam = hay thua. Lỗi đè lên cam, cam đè lên xanh.
///
/// Danh sách tô là STATIC: nút Đo nằm ở cửa sổ Random Level còn lưới bóng ở cửa sổ tool / Inspector, mỗi bên là một
/// instance editor riêng. Sửa data bất kỳ (Mutate), nạp hay swap level thì xoá tô - chỗ thua cũ không còn đúng với
/// data mới, bấm Đo lại để tô theo data mới.
/// </summary>
public partial class LevelDesignDataEditor
{
    private static readonly Color LossSpot = new Color(1f, 0.6f, 0.08f, 1f);
    private static readonly Color LossSpotFill = new Color(1f, 0.6f, 0.08f, 0.4f);

    /// <summary>Số ván cho bot soi chỗ thua - nhiều hơn lúc đo tỉ lệ vì cần đủ ván thua để thấy điểm nóng.</summary>
    private const int LossPlayouts = 300;

    /// <summary>Đơn / bóng có mặt ở ít nhất bấy nhiêu phần ván thua thì tô cam.</summary>
    private const float LossSpotShare = 0.2f;

    private const int MaxLossOrders = 3;
    private const int MaxLossBubbles = 6;

    private static readonly HashSet<int> lossOrders = new HashSet<int>();
    private static readonly HashSet<int> lossBubbles = new HashSet<int>();
    private static readonly HashSet<int> lossSpecies = new HashSet<int>();

    /// <summary>Số đơn / số bóng của level lúc tô - lệch với data đang mở thì bỏ qua, phòng khi data đổi đường khác.</summary>
    private static int lossOrderCount = -1;

    private static int lossBubbleCount = -1;

    private static bool LossSpotsMatch(LevelDesignData data)
    {
        LevelData level = data != null ? data.levelData : null;
        return level != null && lossOrderCount == (level.OrderQueue?.Length ?? 0) &&
               lossBubbleCount == (level.SoftBubbleDataArray?.Length ?? 0);
    }

    private static bool IsLossOrder(LevelDesignData data, int orderIndex) =>
        lossOrders.Contains(orderIndex) && LossSpotsMatch(data);

    private static bool IsLossBubble(LevelDesignData data, int bubbleIndex) =>
        lossBubbles.Contains(bubbleIndex) && LossSpotsMatch(data);

    private static bool IsLossFish(LevelDesignData data, int bubbleIndex, int fishId) =>
        lossSpecies.Contains(fishId) && IsLossBubble(data, bubbleIndex);

    /// <summary>
    /// Báo cáo chỗ thua của từng mức lỡ tay trong MistakeRange (cùng thứ tự), giữ lại để nút chọn mức tô lại mà không
    /// phải chơi lại. Null khi chưa đo hoặc đã xoá tô.
    /// </summary>
    private static LevelHumanBot.LossReport[] lossReports;

    private static int[] lossQueue;

    /// <summary>Mức lỡ tay (index trong MistakeRange) đang tô cam.</summary>
    private static int lossShown = -1;

    private static void ClearLossSpots()
    {
        lossReports = null;
        lossDetails = null;
        lossQueue = null;
        ClearObstacleImpact();
        lossShown = -1;
        if (lossOrderCount < 0) return;
        lossOrders.Clear();
        lossBubbles.Clear();
        lossSpecies.Clear();
        lossOrderCount = -1;
        lossBubbleCount = -1;
        InternalEditorUtility.RepaintAllViews();
    }

    /// <summary>Đủ ván thua để nói về điểm nóng: ít nhất 5 ván và 1/30 số ván.</summary>
    private static bool EnoughLosses(LevelHumanBot.LossReport report) =>
        report.Losses >= Mathf.Max(5, report.Playouts / 30);

    private static List<KeyValuePair<int, int>> HotOrders(LevelHumanBot.LossReport report)
    {
        int need = Mathf.CeilToInt(report.Losses * LossSpotShare);
        return report.OrderHits.Where(kv => kv.Value >= need).OrderByDescending(kv => kv.Value).Take(MaxLossOrders)
            .ToList();
    }

    private static List<KeyValuePair<int, int>> HotBubbles(LevelHumanBot.LossReport report)
    {
        int need = Mathf.CeilToInt(report.Losses * LossSpotShare);
        return report.BubbleHits.Where(kv => kv.Value >= need).OrderByDescending(kv => kv.Value).Take(MaxLossBubbles)
            .ToList();
    }

    /// <summary>
    /// Soi chỗ thua ở TỪNG mức lỡ tay (0 / 5 / 10 / 15%), tô cam theo mức ≈ người chơi, trả về mấy dòng chữ nối vào
    /// báo cáo Đo. Tách được hai nguồn thua:
    /// - Mức 0%: bot không bấm nhầm lần nào mà vẫn thua - đó là bẫy của level. Đơn hay thua ở mức này được gắn [bẫy]
    ///   ở các mức khác.
    /// - Mức > 0%: mỗi mức ghi phần trăm ván thua ngay sau một lần lỡ tay (trong RecentMistakeMoves nước) - phần đó
    ///   là thua vì bấm nhầm, không phải vì level.
    /// </summary>
    private static string MeasureLossSpots(LevelData level)
    {
        ClearLossSpots();
        int[] orders = level?.OrderQueue;
        if (orders == null || orders.Length == 0) return string.Empty;

        var reports = new LevelHumanBot.LossReport[MistakeRange.Length];
        int playerIndex = 0;
        for (int i = 0; i < MistakeRange.Length; i++)
        {
            reports[i] = LevelHumanBot.AnalyzeLosses(level, MistakeRange[i], LossPlayouts);
            if (Mathf.Approximately(MistakeRange[i], PlayerMistakeRate)) playerIndex = i;
        }

        lossReports = reports;
        lossQueue = (int[])orders.Clone();
        lossOrderCount = orders.Length;
        lossBubbleCount = level.SoftBubbleDataArray?.Length ?? 0;
        // Giữ mức người thiết kế đang chọn (tab trước đó / bảng chỉnh độ khó); chưa chọn thì mức ≈ người chơi.
        ShowLossLevel(tuneRateIndex >= 0 ? TuneRateIndex : playerIndex);

        // Đơn vẫn hay thua khi bot không lỡ tay lần nào = bẫy của level.
        var trapOrders = new HashSet<int>();
        if (EnoughLosses(reports[0]) && Mathf.Approximately(MistakeRange[0], 0f))
        {
            foreach (KeyValuePair<int, int> kv in HotOrders(reports[0])) trapOrders.Add(kv.Key);
        }

        // Chi tiết từng mức dựng sẵn một lần; báo cáo Đo chỉ giữ dòng tóm tắt, chi tiết hiện dưới hàng nút chọn mức.
        lossDetails = new LossDetail[reports.Length];
        for (int i = 0; i < reports.Length; i++)
        {
            lossDetails[i] = DescribeLossReport(reports[i], orders, trapOrders, i == playerIndex);
        }

        MeasureObstacleImpact(level);

        return $"\n\nHay thua ({LossPlayouts} ván mỗi mức) - số ván thua: " + string.Join(" · ",
            reports.Select(r => $"{Mathf.RoundToInt(r.MistakeRate * 100f)}%: {r.Losses}"));
    }

    /// <summary>Một dòng trong bảng chi tiết: nhãn, tỉ lệ (0..1, vẽ thanh), chữ bên phải.</summary>
    private struct ShareRow
    {
        public string Label;
        public float Share;
        public bool Trap;
    }

    /// <summary>Chi tiết chỗ thua của một mức lỡ tay, chia mục để vẽ thành bảng.</summary>
    private sealed class LossDetail
    {
        public string Title;
        public bool Enough;
        public readonly List<ShareRow> Orders = new();
        public readonly List<ShareRow> Phases = new();
        public readonly List<ShareRow> Spots = new();

        /// <summary>Phần ván thua ngay sau lỡ tay; âm ở mức 0% (không lỡ tay lần nào).</summary>
        public float MistakeShare = -1f;

        public string HighlightNote;
    }

    /// <summary>Chi tiết chỗ thua của từng mức lỡ tay (cùng thứ tự lossReports), hiện theo nút đang chọn.</summary>
    private static LossDetail[] lossDetails;

    private static readonly Color LossBarBack = new Color(0.5f, 0.5f, 0.5f, 0.22f);
    private static readonly Color CauseMistakeBar = new Color(0.62f, 0.62f, 0.62f, 0.9f);
    private static readonly Color CauseBoardBar = new Color(0.9f, 0.25f, 0.2f, 0.9f);

    private static LossDetail DescribeLossReport(LevelHumanBot.LossReport report, int[] orders, HashSet<int> trapOrders,
        bool player)
    {
        int percent = Mathf.RoundToInt(report.MistakeRate * 100f);
        var detail = new LossDetail
        {
            Title = $"Lỡ tay {percent}%{(player ? " (≈ người chơi)" : "")} - thua {report.Losses}/{report.Playouts} ván",
            Enough = EnoughLosses(report)
        };

        if (!detail.Enough) return detail;

        float losses = report.Losses;
        foreach (KeyValuePair<int, int> kv in HotOrders(report).OrderBy(kv => kv.Key))
        {
            detail.Orders.Add(new ShareRow
            {
                Label = $"Đơn {kv.Key + 1} (loài {orders[kv.Key]})",
                Share = kv.Value / losses,
                Trap = report.MistakeRate > 0f && trapOrders.Contains(kv.Key)
            });
        }

        string[] phaseNames = { "0-20%", "20-40%", "40-60%", "60-80%", "80-100%" };
        for (int i = 0; i < report.Phases.Length; i++)
        {
            detail.Phases.Add(new ShareRow { Label = phaseNames[i], Share = report.Phases[i] / losses });
        }

        float spotTotal = report.Spots.Values.Sum();
        if (spotTotal > 0f)
        {
            foreach (KeyValuePair<LevelHumanBot.NeedSpot, int> kv in report.Spots.OrderByDescending(kv => kv.Value))
            {
                if (kv.Value * 20 < spotTotal) continue;
                detail.Spots.Add(new ShareRow { Label = NeedSpotName(kv.Key), Share = kv.Value / spotTotal });
            }
        }

        if (report.MistakeRate > 0f) detail.MistakeShare = report.RecentMistakeLosses / losses;

        int hotBubbles = HotBubbles(report).Count;
        detail.HighlightNote = hotBubbles > 0 ? $"Ô cam trên lưới = đơn / bóng hay thua ({hotBubbles} bóng giữ cá cần)"
            : detail.Orders.Count > 0 ? "Ô cam trên lưới = đơn hay thua"
            : null;
        return detail;
    }

    /// <summary>Một hàng bảng: nhãn bên trái, thanh tỉ lệ ở giữa, chữ bên phải.</summary>
    private static void DrawBarRow(string label, float fill, string value, Color color)
    {
        Rect row = EditorGUILayout.GetControlRect(false, 16f);
        float labelWidth = Mathf.Min(170f, row.width * 0.45f);
        const float valueWidth = 96f;

        var labelRect = new Rect(row.x, row.y, labelWidth, row.height);
        var valueRect = new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height);
        var bar = new Rect(labelRect.xMax + 4f, row.y + 4f, Mathf.Max(10f, valueRect.x - labelRect.xMax - 8f),
            row.height - 8f);

        GUI.Label(labelRect, label, EditorStyles.miniLabel);
        EditorGUI.DrawRect(bar, LossBarBack);
        EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), color);
        GUI.Label(valueRect, value, EditorStyles.miniLabel);
    }

    private static void DrawShareSection(string title, List<ShareRow> rows, Color color)
    {
        if (rows.Count == 0) return;

        EditorGUILayout.Space(3f);
        GUILayout.Label(title, EditorStyles.miniBoldLabel);
        foreach (ShareRow row in rows)
        {
            DrawBarRow("  • " + row.Label + (row.Trap ? "  [bẫy]" : ""), row.Share, $"{row.Share:P0}",
                row.Trap ? CauseBoardBar : color);
        }
    }

    private static void DrawLossDetail(LossDetail detail)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label(detail.Title, EditorStyles.boldLabel);

        if (!detail.Enough)
        {
            GUILayout.Label("Hiếm khi thua - không có điểm nóng.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(2f);
        GUILayout.Label("Nguyên nhân", EditorStyles.miniBoldLabel);
        if (detail.MistakeShare < 0f)
        {
            DrawBarRow("  • Do bàn (bẫy / vật cản)", 1f, "100%", CauseBoardBar);
            GUILayout.Label("  Bot không bấm nhầm lần nào mà vẫn thua → bẫy của level.", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            DrawBarRow("  • Do bàn (bẫy / vật cản)", 1f - detail.MistakeShare, $"{1f - detail.MistakeShare:P0}",
                CauseBoardBar);
            DrawBarRow($"  • Ngay sau lỡ tay (≤{LevelHumanBot.RecentMistakeMoves} nước)", detail.MistakeShare,
                $"{detail.MistakeShare:P0}", CauseMistakeBar);
        }

        if (detail.Orders.Count > 0) DrawShareSection("Đơn hay thua", detail.Orders, LossSpot);
        else
        {
            EditorGUILayout.Space(3f);
            GUILayout.Label("Đơn hay thua", EditorStyles.miniBoldLabel);
            GUILayout.Label("  Không đơn nào nổi bật - thua rải khắp level", EditorStyles.wordWrappedMiniLabel);
        }

        DrawShareSection("Thua ở chặng nào", detail.Phases, LossSpot);
        DrawShareSection("Lúc kẹt, cá bể cần đang nằm ở", detail.Spots, LossSpot);

        if (!string.IsNullOrEmpty(detail.HighlightNote))
        {
            EditorGUILayout.Space(3f);
            GUILayout.Label(detail.HighlightNote, EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Label("[bẫy] = đơn vẫn hay thua khi không lỡ tay → do level", EditorStyles.centeredGreyMiniLabel);
    }

    /// <summary>Tô cam theo báo cáo của mức lỡ tay index (trong MistakeRange).</summary>
    private static void ShowLossLevel(int index)
    {
        if (lossReports == null || lossQueue == null || index < 0 || index >= lossReports.Length) return;

        lossShown = index;
        tuneRateIndex = index;   // hai nút Làm khó / Làm dễ chỉnh theo đúng mức đang xem
        lossOrders.Clear();
        lossBubbles.Clear();
        lossSpecies.Clear();

        LevelHumanBot.LossReport report = lossReports[index];
        if (EnoughLosses(report))
        {
            foreach (KeyValuePair<int, int> kv in HotOrders(report))
            {
                lossOrders.Add(kv.Key);
                lossSpecies.Add(lossQueue[kv.Key]);
            }

            foreach (KeyValuePair<int, int> kv in HotBubbles(report)) lossBubbles.Add(kv.Key);
        }

        InternalEditorUtility.RepaintAllViews();
    }

    /// <summary>Hàng nút chọn mức lỡ tay để tô cam, hiện dưới báo cáo Đo khi đã có số liệu cho level đang mở.</summary>
    private static void DrawLossLevelPicker(LevelDesignData data)
    {
        if (lossReports == null || !LossSpotsMatch(data)) return;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Xem theo lỡ tay:", EditorStyles.miniLabel, GUILayout.Width(100f));
        for (int i = 0; i < lossReports.Length; i++)
        {
            LevelHumanBot.LossReport report = lossReports[i];
            string label = $"{Mathf.RoundToInt(report.MistakeRate * 100f)}%";
            if (!EnoughLosses(report)) label += " ·";

            bool on = lossShown == i;
            if (GUILayout.Toggle(on, label, EditorStyles.miniButton) && !on) ShowLossLevel(i);
        }

        EditorGUILayout.EndHorizontal();

        if (lossDetails != null && lossShown >= 0 && lossShown < lossDetails.Length)
        {
            EditorGUILayout.Space(2f);
            DrawLossDetail(lossDetails[lossShown]);
        }

        DrawObstacleImpact();
    }

    private static string NeedSpotName(LevelHumanBot.NeedSpot spot)
    {
        switch (spot)
        {
            case LevelHumanBot.NeedSpot.Visible: return "đang hiện (ô chờ đầy)";
            case LevelHumanBot.NeedSpot.Ice: return "trong băng";
            case LevelHumanBot.NeedSpot.HpShell: return "vỏ HP";
            case LevelHumanBot.NeedSpot.Hidden: return "cá ẩn";
            case LevelHumanBot.NeedSpot.SpawnerQueue: return "hàng chờ spawner";
            case LevelHumanBot.NeedSpot.InnerLayer: return "lớp trong";
            case LevelHumanBot.NeedSpot.School: return "trong đàn cá link";
            case LevelHumanBot.NeedSpot.Locked: return "trong ổ khoá";
            case LevelHumanBot.NeedSpot.BelowScreen: return "dưới màn hình";
            default: return "chưa lên bàn";
        }
    }
}
