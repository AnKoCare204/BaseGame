using System;
using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Khối "Function random level", nay nằm ở cửa sổ nổi <see cref="LevelRandomWindow"/> chứ không còn trong Inspector:
/// ba khâu sinh seed tách rời, bấm riêng từng nút - đơn → bóng → xáo.
/// Sinh bóng rải cá đúng thứ tự đơn (bóng đầu chứa cá của đơn đầu) nên ra level dễ thở; muốn khó thì dùng nút xáo,
/// chọn mốc bóng để giữ nguyên đoạn mở đầu.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    /// <summary>
    /// Bốn ô nhập này là static vì có hai instance LevelDesignDataEditor cùng sống: một của Inspector, một do
    /// LevelRandomWindow dựng riêng. EvacuateFishFromRock đọc randomMaxFishPerBubble mà nó chạy từ CẢ hai đường
    /// (checkbox Đá ở panel Obstacle của Inspector, và nút Random Đá ở cửa sổ), nên hai bên buộc phải thấy cùng
    /// một trần. Để instance thì gõ số ở cửa sổ xong tick Đá trong Inspector là rải cá theo một trần khác.
    /// </summary>
    private static int randomOrderCount = 6;

    private static int randomFishTypes = 3;
    private static int randomMinFishPerBubble = 1;
    private static int randomMaxFishPerBubble = 2;

    /// <summary>
    /// Tỉ lệ (%) bóng đạt đúng "Cá/bóng nhiều nhất" khi sinh. Bốc đều tay thì cỡ to chiếm ngang các cỡ khác, cá dồn
    /// vào ít bóng; hạ số này xuống là ra nhiều bóng nhỏ hơn. Phần còn lại chia đều cho các cỡ từ min tới max-1.
    /// </summary>
    private static int randomLargestSharePercent = Mathf.RoundToInt(LevelData.DefaultLargestBubbleShare * 100f);

    private bool showRandomPanel = true;

    /// <summary>Bề ngang tối thiểu mỗi cột trong cửa sổ Random Level; hẹp hơn thì các ô nhập bị nuốt mất nhãn.</summary>
    private const float MinRandomColumnWidth = 260f;

    /// <summary>Mốc xáo cá, đếm theo ID bóng hiện trên khung (1..n) chứ không phải index, cho khớp thứ tự đang nhìn thấy.</summary>
    private int shuffleStartBubbleId = 1;

    /// <summary>
    /// Điểm vào cho cửa sổ nổi <see cref="LevelRandomWindow"/>: vẽ đúng khối random cộng ô báo trạng thái, không có
    /// lưới bóng hay panel phải. Cửa sổ dựng một LevelDesignDataEditor riêng chỉ để gọi hàm này, nhờ vậy toàn bộ
    /// logic sinh seed nằm nguyên tại chỗ, không phải bê sang lớp khác rồi nuôi hai bản.
    /// </summary>
    public void DrawRandomWindowGUI()
    {
        if (target is not LevelDesignData data) return;
        data.levelData ??= new LevelData();

        DesignConfig design = DesignConfig.Instance;
        if (design == null)
        {
            EditorGUILayout.HelpBox("Chưa có DesignConfig trong Assets/Resources/GlobalConfig", MessageType.Error);
            return;
        }

        fishIds ??= ToolFishCatalog.GetIds();   // ID đúng như game gán (xem ToolFishCatalog), không dùng bảng FishModelConfigs cũ
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

        // Hai cột: bên trái là khâu 1-2-3 rải cá đều tay, bên phải sinh thẳng theo mức độ khó đo từ bộ nguồn.
        float column = Mathf.Max(MinRandomColumnWidth, (EditorGUIUtility.currentViewWidth - 24f) * 0.5f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(column));
        DrawRandomPanel(data, design);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(column));
        DrawDifficultyPanel(data);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (pending == null) return;
        Action action = pending;
        pending = null;
        action();

        // Sửa data từ cửa sổ thì Inspector đang mở seed đó vẫn giữ hình cũ - Repaint() ở đây chỉ vẽ lại chính cửa sổ.
        InternalEditorUtility.RepaintAllViews();
    }

    private void DrawRandomPanel(LevelDesignData data, DesignConfig design)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);

        // Tiêu đề kiêm nút đóng/mở, cùng lối với nút "Hiện data thô" ở DrawRawSection: mũi tên là ký tự nằm ngay
        // trong chuỗi và nền là style label, nên cả dòng nằm gọn trong lề box như mọi tiêu đề khác.
        // KHÔNG dùng EditorGUI(Layout).Foldout / BeginFoldoutHeaderGroup: chúng là control kiểu prefix, tự mang
        // minWidth = labelWidth + fieldWidth (labelWidth mặc định ~45% bề ngang cửa sổ) nên nong cả khung box rộng
        // hơn cột, kéo cửa sổ to ra cũng không hết vì labelWidth to theo cửa sổ luôn.
        if (GUILayout.Button(showRandomPanel ? "▼ Function random level" : "▶ Function random level",
                SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true)))
        {
            showRandomPanel = !showRandomPanel;
        }

        if (showRandomPanel)
        {
            GUILayout.Label($"mỗi đơn = {GamePlayGlobalConfig.TankGoal} cá", SirenixGUIStyles.CenteredGreyMiniLabel,
                GUILayout.ExpandWidth(true));

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 104f;

            DrawRandomOrderSection(data);
            EditorGUILayout.Space(6f);
            DrawRandomBubbleSection(data);
            EditorGUILayout.Space(6f);
            DrawShuffleSection(data);
            EditorGUILayout.Space(6f);
            DrawRandomObstacleSection(data, design);

            EditorGUIUtility.labelWidth = labelWidth;
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>Khâu 1: chỉ sinh OrderQueue, không đụng bóng - số cá sẽ lệch cho tới khi bấm tiếp nút sinh bóng.</summary>
    private void DrawRandomOrderSection(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("1. Order", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        randomOrderCount = Mathf.Max(1, EditorGUILayout.IntField("Số lượng Order", randomOrderCount));
        randomFishTypes = Mathf.Clamp(EditorGUILayout.IntField("Số loài cá", randomFishTypes), 1,
            Mathf.Max(1, fishIds.Length));

        GUILayout.Label($"→ {randomOrderCount} đơn, cần {randomOrderCount * GamePlayGlobalConfig.TankGoal} cá",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        GUI.enabled = fishIds.Length > 0;
        if (GUILayout.Button("Random Order", GUILayout.Height(26f))) pending = () => RandomOrders(data);
        GUI.enabled = true;

        if (fishIds.Length == 0)
        {
            GUILayout.Label("FishModelGlobalConfig trống nên chưa bốc được loài cá nào",
                SirenixGUIStyles.MultiLineCenteredLabel);
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>Số cá trung bình mỗi bóng theo min / max / tỉ lệ bóng to, dùng để đoán trước số bóng sẽ sinh ra.</summary>
    private static float AverageFishPerBubble()
    {
        int min = randomMinFishPerBubble;
        int max = randomMaxFishPerBubble;
        if (max <= min) return min;

        float largest = randomLargestSharePercent / 100f;
        float restAverage = (min + max - 1) * 0.5f;
        return largest * max + (1f - largest) * restAverage;
    }

    /// <summary>Một dòng "2 cá 38% · 3 cá 38% · 4 cá 25%" cho thấy ngay tỉ lệ từng cỡ bóng trước khi bấm sinh.</summary>
    private static string DescribeSizeShares()
    {
        int min = randomMinFishPerBubble;
        int max = randomMaxFishPerBubble;
        if (max <= min) return $"Mọi bóng đều {min} cá";

        float largest = randomLargestSharePercent / 100f;
        float rest = (1f - largest) / (max - min);
        var parts = new List<string>();
        for (int size = min; size < max; size++) parts.Add($"{size} cá {rest:P0}");
        parts.Add($"{max} cá {largest:P0}");
        return string.Join(" · ", parts);
    }

    /// <summary>Khâu 2: sinh lại toàn bộ bóng từ OrderQueue đang có, cá rải lần lượt theo thứ tự đơn.</summary>
    private void DrawRandomBubbleSection(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("2. Bubble", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        randomMinFishPerBubble = Mathf.Max(1, EditorGUILayout.IntField("Cá/bóng ít nhất", randomMinFishPerBubble));
        // Vẽ sau ô "ít nhất" nên kẹp cận dưới theo nó luôn: gõ min lớn hơn max thì max tự nhích lên, không ra khoảng rỗng.
        randomMaxFishPerBubble = Mathf.Max(randomMinFishPerBubble,
            EditorGUILayout.IntField("Cá/bóng nhiều nhất", randomMaxFishPerBubble));

        randomLargestSharePercent = Mathf.Clamp(
            EditorGUILayout.IntField("% bóng to nhất", randomLargestSharePercent), 0, 100);

        int orderCount = data.levelData.OrderQueue?.Length ?? 0;
        int totalFish = orderCount * GamePlayGlobalConfig.TankGoal;
        float average = AverageFishPerBubble();
        GUILayout.Label(
            orderCount == 0
                ? "OrderQueue đang trống"
                : $"→ {totalFish} cá, khoảng {Mathf.CeilToInt(totalFish / average)} bóng " +
                  $"(trung bình {average:0.0} cá/bóng)",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        GUILayout.Label(DescribeSizeShares(), SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        GUI.enabled = orderCount > 0;
        if (GUILayout.Button("Random Bubble", GUILayout.Height(26f))) pending = () => RandomBubbles(data);
        GUI.enabled = true;

        GUILayout.Label("Cá rải lần lượt theo OrderQueue: bóng đầu chứa cá của đơn đầu.",
            SirenixGUIStyles.MultiLineCenteredLabel);

        // Cỡ bóng đã đi theo sàn bán kính của số cá (BubbleRadiusFloors) nên không còn cảnh chật như trước; chỉ
        // nhắc khi số cá vượt hàng cuối bảng, vì lúc đó sàn là suy tiếp chứ không phải con số ai đó ngắm rồi chọn.
        int topRow = TopRadiusFloorFishCount();
        if (topRow > 0 && randomMaxFishPerBubble > topRow)
        {
            GUILayout.Label(
                $"Bảng sàn bán kính chỉ có hàng tới {topRow} cá/bóng. Bóng nhiều cá hơn sẽ lấy sàn suy tiếp theo bước " +
                "nhảy của hai hàng cuối - muốn chắc thì thêm hàng vào BubbleRadiusFloors (GamePlayGlobalConfig).",
                SirenixGUIStyles.MultiLineCenteredLabel);
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>Số cá của hàng cao nhất trong bảng sàn bán kính; 0 khi chưa có config hoặc bảng trống.</summary>
    private static int TopRadiusFloorFishCount()
    {
        List<BubbleRadiusFloor> rows = GamePlayGlobalConfig.Instance != null
            ? GamePlayGlobalConfig.Instance.BubbleRadiusFloors
            : null;
        if (rows == null) return 0;

        int top = 0;
        foreach (BubbleRadiusFloor row in rows)
        {
            if (row != null && row.FishCount > top) top = row.FishCount;
        }

        return top;
    }

    /// <summary>Khâu 3: xáo cá giữa các bóng từ một mốc trở đi, mốc đếm theo ID bóng đang hiện trên khung (1..n).</summary>
    private void DrawShuffleSection(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("3. Shuffle", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        int bubbleCount = data.levelData.SoftBubbleDataArray?.Length ?? 0;
        shuffleStartBubbleId = Mathf.Clamp(EditorGUILayout.IntField("Bubble bắt đầu", shuffleStartBubbleId), 1,
            Mathf.Max(1, bubbleCount));

        GUILayout.Label(
            bubbleCount == 0
                ? "Chưa có bóng nào để xáo"
                : shuffleStartBubbleId <= 1
                    ? $"→ xáo cá của cả {bubbleCount} bóng"
                    : $"→ giữ nguyên bóng 1..{shuffleStartBubbleId - 1}, xáo bóng {shuffleStartBubbleId}..{bubbleCount}",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        GUI.enabled = bubbleCount > 0;
        if (GUILayout.Button("Shuffle cá trong bóng", GUILayout.Height(26f))) pending = () => ShuffleFish(data);
        GUI.enabled = true;

        GUILayout.Label("Số cá mỗi bóng giữ nguyên, chỉ đổi con nào nằm bóng nào.",
            SirenixGUIStyles.MultiLineCenteredLabel);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Sinh lại OrderQueue theo số lượng đã nhập; bóng để nguyên nên số cá lệch cho tới khi sinh lại bóng.</summary>
    private void RandomOrders(LevelDesignData data)
    {
        if (fishIds.Length == 0)
        {
            SetStatus(false, "FishModelGlobalConfig trống - không có loài cá nào để random");
            return;
        }

        Mutate(data, "Random order", () => data.levelData.RandomOrderQueue(fishIds, randomOrderCount, randomFishTypes));

        int orderCount = data.levelData.OrderQueue?.Length ?? 0;
        int required = data.RequiredFish;
        int total = data.TotalFishInBubbles;
        string summary = $"Đã random {orderCount} đơn, cần {required} cá";
        // Cố ý không chạy Validate ở đây: bóng vẫn là bóng cũ nên kiểu gì cũng kêu lệch, chỉ nhắc bấm nút sinh bóng.
        SetStatus(true, total == required ? summary : $"{summary} - bóng đang có {total} cá, bấm Random Bubble để sinh lại",
            total == required ? (MessageType?)null : MessageType.Warning);
    }

    /// <summary>Sinh lại toàn bộ bóng từ OrderQueue: mỗi đơn góp TankGoal con cá, rải lần lượt theo thứ tự đơn.</summary>
    private void RandomBubbles(LevelDesignData data)
    {
        if ((data.levelData.OrderQueue?.Length ?? 0) == 0)
        {
            SetStatus(false, "OrderQueue đang trống - bấm Random Order trước");
            return;
        }

        Mutate(data, "Random bubble", () =>
        {
            data.levelData.RandomBubbles(randomMinFishPerBubble, randomMaxFishPerBubble, false,
                randomLargestSharePercent / 100f);
            data.NormalizeBubbleIds();
        });

        ResetSelection();
        ReportSeed(data, $"Đã sinh {data.levelData.BubbleCount} bóng, {data.TotalFishInBubbles} cá rải lần lượt theo đơn");
    }

    /// <summary>Xáo cá giữa các bóng từ bóng shuffleStartBubbleId trở đi; các bóng trước mốc giữ nguyên.</summary>
    private void ShuffleFish(LevelDesignData data)
    {
        int bubbleCount = data.levelData.SoftBubbleDataArray?.Length ?? 0;
        if (bubbleCount == 0)
        {
            SetStatus(false, "Chưa có bóng nào để xáo");
            return;
        }

        int startId = Mathf.Clamp(shuffleStartBubbleId, 1, bubbleCount);
        int changed = 0;
        Mutate(data, "Xáo cá trong bóng", () => changed = data.levelData.ShuffleFishFromBubble(startId - 1));

        if (changed == 0)
        {
            SetStatus(false, $"Từ bóng {startId} trở đi không có con cá nào để xáo");
            return;
        }

        ResetSelection();
        ReportSeed(data, $"Đã xáo cá của {changed} bóng, tính từ bóng {startId}");
    }

    /// <summary>
    /// Báo kết quả một nút sinh seed, kèm phần Validate kêu (nếu có). Data vừa sinh vẫn giữ nguyên dù chưa hợp lệ,
    /// chỉ đổi sang báo vàng để người dựng level tự chỉnh tiếp.
    /// </summary>
    private void ReportSeed(LevelDesignData data, string summary)
    {
        var errors = new List<string>();
        bool valid = data.levelData.Validate(errors);
        SetStatus(true, valid ? summary : $"{summary}; seed chưa hợp lệ: {string.Join("; ", errors)}",
            valid ? (MessageType?)null : MessageType.Warning);
    }
}
