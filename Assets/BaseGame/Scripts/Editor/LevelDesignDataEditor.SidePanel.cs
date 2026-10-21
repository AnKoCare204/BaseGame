using System.Collections.Generic;
using Core.GamePlay;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Panel phải: tìm / lưu level và biến thể của level, bảng vật cản của bóng đang chọn, ô cá đang chọn và nút mở cửa sổ random.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    /// <summary>Cạnh ô vuông checkbox mà EditorStyles.toggle vẽ ra.</summary>
    private const float ChipToggleSize = 14f;

    /// <summary>Vài px chừa quanh checkbox để lộ màu nền - thứ duy nhất phân biệt vật cản sau khi bỏ chữ.</summary>
    private const float ChipColorPad = 4f;

    private const float ChipWidth = ChipToggleSize + ChipColorPad * 2f;
    private const float ChipHeight = ChipToggleSize + ChipColorPad;

    private Vector2 sideScroll;

    private void DrawSidePanel(LevelDesignData data, DesignConfig design, float width, float viewHeight)
    {
        EditorGUILayout.BeginVertical(ColumnWidth(SideColumn, width));
        sideScroll = EditorGUILayout.BeginScrollView(sideScroll, GUILayout.Height(viewHeight));
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);

        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 86f;

        EditorGUILayout.BeginHorizontal();
        findLevel = Mathf.Max(1, EditorGUILayout.IntField("Level", findLevel));
        int wanted = findLevel;
        if (GUILayout.Button("Tìm level", GUILayout.Width(74f))) pending = () => Load(data, wanted);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = findLevel > 1;
        if (GUILayout.Button("Prev")) pending = () => Load(data, Mathf.Max(1, findLevel - 1));
        GUI.enabled = true;
        if (GUILayout.Button("Next")) pending = () => Load(data, findLevel + 1);
        EditorGUILayout.EndHorizontal();

        // Biến thể: LevelData_{n}_{x}.json trong thư mục riêng (variantFolder), x = số lần chơi level n để bật biến thể.
        // "Load biến thể" lấy n từ ô Level ở trên (không cần Tìm level trước); không có file thì dọn trống đơn và bóng để
        // xếp biến thể mới, kèm cảnh báo. "Lưu biến thể" ghi data đang mở thành biến thể x của level ĐANG MỞ, không đụng
        // file gốc.
        TempContent.text = "Biến thể";
        TempContent.tooltip = "x trong LevelData_{n}_{x}.json: số lần chơi level n để bật biến thể này";
        findVariant = Mathf.Max(1, EditorGUILayout.IntField(TempContent, findVariant));
        int variant = findVariant;

        EditorGUILayout.BeginHorizontal();
        TempContent.text = "Load biến thể";
        TempContent.tooltip = $"Nạp {LevelDesignData.GetVariantFileName(wanted, variant)} từ thư mục biến thể; " +
                              "chưa có file thì xoá hết đơn và bóng để xếp biến thể mới";
        if (GUILayout.Button(TempContent)) pending = () => LoadVariant(data, wanted, variant);
        TempContent.text = "Lưu biến thể";
        TempContent.tooltip = $"Ghi data đang mở thành {LevelDesignData.GetVariantFileName(data.levelData.Level, variant)} " +
                              "trong thư mục biến thể (không đụng file level gốc)";
        if (GUILayout.Button(TempContent)) pending = () => SaveVariant(data, variant);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Đang mở", data.openVariant > 0
            ? $"level {data.levelData.Level} · biến thể {data.openVariant}"
            : $"level {data.levelData.Level} (gốc)");

        LevelDifficulty difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Độ khó", data.levelData.Difficulty);
        if (difficulty != data.levelData.Difficulty)
        {
            pending = () => Mutate(data, "Đổi độ khó", () => data.levelData.EditorSetDifficulty(difficulty));
        }

        int total = data.TotalFishInBubbles;
        int required = data.RequiredFish;
        Color previous = GUI.color;
        if (total != required) GUI.color = SirenixGUIStyles.YellowWarningColor;
        EditorGUILayout.LabelField("Số lượng cá", total == required ? NumberLabel(total) : $"{total}  (đơn cần {required})");
        GUI.color = previous;
        EditorGUILayout.LabelField("Số match", NumberLabel(total / Mathf.Max(1, GamePlayGlobalConfig.TankGoal)));
        EditorGUILayout.LabelField("Số loài cá", NumberLabel(ToolLevelStats.SpeciesCount(data)));
        EditorGUILayout.LabelField("Số bóng", NumberLabel(data.levelData.SoftBubbleDataArray?.Length ?? 0));

        // Nút Swap phải nằm NGOÀI change check: bấm nút cũng bật GUI.changed, nên nằm trong thì EndChangeCheck ghi đè
        // pending bằng "Đổi thiết lập tool" và lệnh swap không bao giờ chạy.
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        int swapLevel = Mathf.Max(1, EditorGUILayout.IntField("Swap level", data.swapLevel));
        bool settingsChanged = EditorGUI.EndChangeCheck();
        bool swapClicked = GUILayout.Button("Swap", GUILayout.Width(74f));
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        string folder = EditorGUILayout.TextField("Thư mục", data.levelFolder);
        EditorGUIUtility.labelWidth = 110f;
        string variantFolder = EditorGUILayout.TextField("Thư mục biến thể", data.variantFolder);
        EditorGUIUtility.labelWidth = 86f;
        settingsChanged |= EditorGUI.EndChangeCheck();

        if (swapClicked)
        {
            // Swap với đúng số đang gõ trong ô, kể cả khi nó chưa kịp ghi vào data.
            pending = () =>
            {
                Mutate(data, "Đổi thiết lập tool", () => data.swapLevel = swapLevel);
                Swap(data);
            };
        }
        else if (settingsChanged)
        {
            pending = () => Mutate(data, "Đổi thiết lập tool", () =>
            {
                data.swapLevel = swapLevel;
                data.levelFolder = folder;
                data.variantFolder = variantFolder;
            });
        }

        EditorGUIUtility.labelWidth = labelWidth;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Check Level", GUILayout.Height(28f))) pending = () => CheckLevel(data);
        if (GUILayout.Button("Save", GUILayout.Height(28f))) pending = () => Save(data);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        DrawObstacleLegend(data, design);
        EditorGUILayout.Space(6f);
        DrawSelectedFish(data);
        EditorGUILayout.Space(6f);

        // Khối random chuyển sang cửa sổ nổi để panel phải khỏi dài thêm; xem LevelRandomWindow.
        if (GUILayout.Button("Mở tool random level", GUILayout.Height(24f))) LevelRandomWindow.Open();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawObstacleLegend(LevelDesignData data, DesignConfig design)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("Obstacle", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        SoftBubbleData bubble = GetSelectedBubble(data);
        if (bubble == null)
        {
            GUILayout.Label("Click một bóng để sửa vật cản", SirenixGUIStyles.MultiLineCenteredLabel,
                GUILayout.ExpandWidth(true));
        }
        else
        {
            GUILayout.Label($"Bóng ID {bubble.softBubbleId}  #{selectedBubbleIndex + 1}",
                SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
            DrawObstacleToggles(data, design, bubble);
        }

        EditorGUILayout.Space(4f);
        TempContent.text = "Tự chỉnh size mọi bóng";
        TempContent.tooltip = "Bóng nào đang nhỏ hơn sàn bán kính của số cá nó chứa thì kẹp lên sàn " +
                              "(bảng BubbleRadiusFloors trong GamePlayGlobalConfig). Bóng đang to hơn sàn giữ nguyên.";
        if (GUILayout.Button(TempContent, GUILayout.Height(24f)))
            pending = () => ApplyAutoSizeToAllBubbles(data);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Hàng ô bật/tắt vật cản + field HP / keyId của bóng đang chọn, vẽ ở panel Obstacle bên phải.</summary>
    private void DrawObstacleToggles(LevelDesignData data, DesignConfig design, SoftBubbleData bubble)
    {
        List<ObstacleStyle> styles = design.ObstacleStyles;
        if (styles == null || styles.Count == 0) return;

        bool showSpawnerFish = bubble.isSpawner && !bubble.isRock;
        foreach (ObstacleStyle style in styles)
        {
            if (style == null) continue;

            bool lockSpawner = bubble.isRock && style.Obstacle == BubbleObstacle.Spawner;
            bool wasEnabled = GUI.enabled;
            if (lockSpawner) GUI.enabled = false;

            EditorGUILayout.BeginHorizontal();
            Rect chip = GUILayoutUtility.GetRect(ChipWidth, ChipHeight,
                GUILayout.Width(ChipWidth), GUILayout.Height(ChipHeight));
            bool on = DesignConfig.GetFlag(bubble, style.Obstacle);
            bool next = DrawObstacleChip(chip, style, on);
            GUILayout.Label(style.Label, SirenixGUIStyles.LeftAlignedGreyLabel);
            GUILayout.Label(style.Hotkey == KeyCode.None ? "-" : style.Hotkey.ToString(),
                SirenixGUIStyles.RightAlignedWhiteMiniLabel, GUILayout.Width(38f));
            EditorGUILayout.EndHorizontal();
            GUI.enabled = wasEnabled;

            if (style.Obstacle == BubbleObstacle.Spawner) showSpawnerFish = next && !bubble.isRock;
            if (lockSpawner || next == on) continue;

            SoftBubbleData target = bubble;
            BubbleObstacle obstacle = style.Obstacle;
            pending = () => SetObstacle(data, target, obstacle, next);
        }

        if (bubble.isBubbleHp || bubble.isKey || bubble.isOrderBubble)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 78f;
            if (bubble.isBubbleHp)
            {
                int hp = EditorGUILayout.IntField("HP", bubble.bubbleHp);
                if (hp != bubble.bubbleHp)
                {
                    SoftBubbleData target = bubble;
                    pending = () => Mutate(data, "Đổi HP bóng", () => target.bubbleHp = Mathf.Max(0, hp));
                }
            }

            if (bubble.isKey)
            {
                int keyId = EditorGUILayout.IntField("Key id", bubble.keyId);
                if (keyId != bubble.keyId)
                {
                    SoftBubbleData target = bubble;
                    pending = () => Mutate(data, "Đổi ổ khoá của chìa", () => target.keyId = keyId);
                }
            }

            if (bubble.isOrderBubble)
            {
                // Cách còn lại để đặt fishOrderId là kéo cá vào ô giữa khung bóng; xem DrawOrderFishSlot.
                int fishOrderId = EditorGUILayout.IntField("Fish order id", bubble.fishOrderId);
                if (fishOrderId != bubble.fishOrderId)
                {
                    SoftBubbleData target = bubble;
                    pending = () => Mutate(data, "Đổi cá của đơn", () => target.fishOrderId = Mathf.Max(0, fishOrderId));
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }

        if (bubble.isMultiLayer && !bubble.isRock) DrawLayerCountRow(data, bubble);
        if (showSpawnerFish && !bubble.isRock) DrawSpawnerFish(data, design, bubble);
    }

    /// <summary>
    /// Thêm / bớt lớp của bóng nhiều lớp, kèm số cá từng lớp để thấy ngay luật "lớp sau không nhiều hơn lớp trước".
    /// Chọn lớp nào để sửa cá thì dùng thanh ◀ ▶ giữa dải tiêu đề của chính khung bóng đó.
    /// </summary>
    private void DrawLayerCountRow(LevelDesignData data, SoftBubbleData bubble)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Lớp: {bubble.LayerCount}", SirenixGUIStyles.LeftAlignedGreyLabel);

        TempContent.text = "+";
        TempContent.tooltip = "Thêm một lớp vào cuối";
        if (GUILayout.Button(TempContent, SirenixGUIStyles.MiniButton, GUILayout.Width(24f)))
            pending = () => AddBubbleLayer(data, bubble);

        TempContent.text = "−";
        TempContent.tooltip = "Xoá lớp cuối; hết lớp thì tắt luôn isMultiLayer";
        if (GUILayout.Button(TempContent, SirenixGUIStyles.MiniButton, GUILayout.Width(24f)))
            pending = () => RemoveBubbleLayer(data, bubble);
        EditorGUILayout.EndHorizontal();

        var counts = new string[bubble.LayerCount];
        for (int i = 0; i < counts.Length; i++) counts[i] = $"L{i + 1}:{bubble.LayerFishCount(i)}";
        GUILayout.Label(string.Join("  ", counts), SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
    }

    /// <summary>
    /// Một checkbox vật cản, không có chữ: nhìn màu nền là biết ô nào, bật/tắt đọc ở dấu tích, nền đậm khi bật và
    /// nhạt khi tắt. Bấm chỗ nào trong ô cũng đổi được, không cần trúng ngay dấu tích.
    /// </summary>
    private static bool DrawObstacleChip(Rect rect, ObstacleStyle style, bool on)
    {
        // Không in chữ trong ô nữa nên màu nền là thứ duy nhất phân biệt vật cản: kéo alpha lúc tắt lên cho còn đọc
        // được màu, còn bật/tắt đã có dấu tích lo. Tên đầy đủ + phím tắt nằm ở tooltip và ở bảng chú thích bên phải.
        Color background = style.Color;
        background.a = Mathf.Clamp01(background.a * (on ? 1.8f : 0.55f));
        DrawRoundedRect(rect, background, SirenixGUIStyles.BorderColor);

        // Thụt trái đúng ChipColorPad: EditorStyles.toggle vẽ ô vuông sát mép trái rect nhận được, nên chừa được
        // viền màu bên trái, phần dư bên phải lo nốt viền còn lại. Cả rect vẫn là vùng bấm.
        TempContent.text = string.Empty;
        TempContent.tooltip = $"{style.Label} — phím {style.Hotkey}";
        return EditorGUI.ToggleLeft(
            new Rect(rect.x + ChipColorPad, rect.y, rect.width - ChipColorPad, rect.height), TempContent, on,
            EditorStyles.miniLabel);
    }

    private void DrawSelectedFish(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("Cá", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        FishData fish = GetSelectedFish(data);
        if (fish == null)
        {
            GUILayout.Label("Click một ô cá để chọn", SirenixGUIStyles.MultiLineCenteredLabel,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndVertical();
            return;
        }

        string where = selectedFishList == FishListSpawner ? "spawner"
            : selectedFishList > FishListMain ? $"lớp {selectedFishList + 1}"
            : "bóng";
        GUILayout.Label($"#{selectedFishIndex + 1} trên {where}  #{selectedBubbleIndex + 1}",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 52f;
        int fishId = EditorGUILayout.IntField("Fish id", fish.FishId);
        if (fishId != fish.FishId)
        {
            FishData target = fish;
            pending = () => Mutate(data, "Đổi Fish id", () => target.FishId = Mathf.Max(0, fishId));
        }

        bool hidden = EditorGUILayout.Toggle("Ẩn", fish.isHidden);
        if (hidden != fish.isHidden)
        {
            FishData target = fish;
            pending = () => Mutate(data, "Đổi cá ẩn", () => target.isHidden = hidden);
        }

        EditorGUIUtility.labelWidth = labelWidth;
        EditorGUILayout.EndVertical();
    }
}
