using System;
using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Khâu 4 của "Function random level": rắc vật cản lên các bóng đang có. Mỗi vật cản một nút, kèm ô "Số bóng" và
/// ô giá trị nếu vật cản đó có giá trị (HP, số cá trên spawner). Danh sách vật cản lấy thẳng từ
/// DesignConfig.ObstacleStyles nên thêm / bớt vật cản trong config là UI đi theo, không phải sửa ở đây.
///
/// Riêng ổ khoá và chìa khoá gộp thành một nút: chúng chỉ có nghĩa khi đi theo cặp (chìa phải cầm đúng id của ổ
/// khoá), bốc rời từng loại là ra level có khoá không ai mở được.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private readonly Dictionary<BubbleObstacle, int> randomObstacleCount = new Dictionary<BubbleObstacle, int>();
    private readonly Dictionary<BubbleObstacle, int> randomObstacleValue = new Dictionary<BubbleObstacle, int>();
    private readonly Dictionary<BubbleObstacle, string> obstacleButtonLabels = new Dictionary<BubbleObstacle, string>();

    private int ObstacleCount(BubbleObstacle obstacle) =>
        randomObstacleCount.TryGetValue(obstacle, out int count) ? count : 1;

    private int ObstacleValue(BubbleObstacle obstacle) =>
        randomObstacleValue.TryGetValue(obstacle, out int value) ? value : 1;

    /// <summary>Nhãn ô giá trị của vật cản, null = vật cản này không có giá trị gì để nhập.</summary>
    private static string ObstacleValueLabel(BubbleObstacle obstacle)
    {
        return obstacle switch
        {
            BubbleObstacle.BubbleHp => "HP mỗi bóng",
            BubbleObstacle.Spawner => "Cá trên spawner",
            _ => null
        };
    }

    private string ObstacleButtonLabel(ObstacleStyle style)
    {
        if (obstacleButtonLabels.TryGetValue(style.Obstacle, out string text)) return text;
        text = "Random " + (string.IsNullOrEmpty(style.ShortLabel) ? style.Label : style.ShortLabel);
        obstacleButtonLabels[style.Obstacle] = text;
        return text;
    }

    private void DrawRandomObstacleSection(LevelDesignData data, DesignConfig design)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("4. Obstacle", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));

        int bubbleCount = data.levelData.SoftBubbleDataArray?.Length ?? 0;
        GUILayout.Label(
            bubbleCount == 0 ? "Chưa có bóng nào để rắc vật cản" : $"Bốc ngẫu nhiên trong {bubbleCount} bóng",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        List<ObstacleStyle> styles = design.ObstacleStyles;
        if (styles != null)
        {
            foreach (ObstacleStyle style in styles)
            {
                if (style == null) continue;

                // Chìa không có nút riêng: nó nằm trong nút cặp vẽ ở dòng ổ khoá.
                if (style.Obstacle == BubbleObstacle.Key) continue;

                if (style.Obstacle == BubbleObstacle.Lock) DrawLockPairRow(data, design, style, bubbleCount);
                else DrawObstacleRandomRow(data, style, bubbleCount);
            }
        }

        // Cá ẩn là cờ của từng con cá chứ không phải vật cản của bóng, nên không có trong ObstacleStyles.
        DrawRandomHiddenRow(data);

        EditorGUILayout.Space(2f);
        GUI.enabled = bubbleCount > 0;
        TempContent.text = "Xoá hết vật cản";
        TempContent.tooltip = "Tắt mọi cờ vật cản trên mọi bóng. Cá đã bị bóng đá rải đi thì không gom về lại được.";
        if (GUILayout.Button(TempContent, GUILayout.Height(22f))) pending = () => ClearAllObstacles(data);
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    private void DrawObstacleRandomRow(LevelDesignData data, ObstacleStyle style, int bubbleCount)
    {
        BubbleObstacle obstacle = style.Obstacle;
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);

        EditorGUILayout.BeginHorizontal();
        DrawObstacleColorChip(style);
        GUILayout.Label(style.Label, SirenixGUIStyles.LeftAlignedGreyLabel);
        EditorGUILayout.EndHorizontal();

        randomObstacleCount[obstacle] = Mathf.Clamp(EditorGUILayout.IntField("Số bóng", ObstacleCount(obstacle)), 0,
            Mathf.Max(0, bubbleCount));

        string valueLabel = ObstacleValueLabel(obstacle);
        if (valueLabel != null)
        {
            int min = obstacle == BubbleObstacle.BubbleHp ? 1 : 0;
            randomObstacleValue[obstacle] = Mathf.Max(min, EditorGUILayout.IntField(valueLabel, ObstacleValue(obstacle)));
        }

        string note = ObstacleNote(obstacle);
        if (note != null) GUILayout.Label(note, SirenixGUIStyles.MultiLineCenteredLabel);

        GUI.enabled = bubbleCount > 0 && ObstacleCount(obstacle) > 0;
        if (GUILayout.Button(ObstacleButtonLabel(style), GUILayout.Height(22f)))
        {
            ObstacleStyle target = style;
            pending = () => RandomObstacle(data, target);
        }

        GUI.enabled = true;
        EditorGUILayout.EndVertical();
    }

    /// <summary>Dòng gộp ổ khoá + chìa: một ô "Số cặp" và một nút, mỗi lần bấm ăn 2 bóng cho mỗi cặp.</summary>
    private void DrawLockPairRow(LevelDesignData data, DesignConfig design, ObstacleStyle lockStyle, int bubbleCount)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);

        EditorGUILayout.BeginHorizontal();
        DrawObstacleColorChip(lockStyle);
        ObstacleStyle keyStyle = design.GetStyle(BubbleObstacle.Key);
        if (keyStyle != null) DrawObstacleColorChip(keyStyle);
        GUILayout.Label("Ổ khoá + Chìa (cặp)", SirenixGUIStyles.LeftAlignedGreyLabel);
        EditorGUILayout.EndHorizontal();

        int maxPairs = Mathf.Max(0, bubbleCount / 2);
        int pairs = Mathf.Clamp(EditorGUILayout.IntField("Số cặp", ObstacleCount(BubbleObstacle.Lock)), 0, maxPairs);
        randomObstacleCount[BubbleObstacle.Lock] = pairs;

        GUILayout.Label("Mỗi cặp ăn 2 bóng: bóng đứng trước làm chìa và cầm sẵn keyId của bóng đứng sau (ổ khoá).",
            SirenixGUIStyles.MultiLineCenteredLabel);

        GUI.enabled = bubbleCount >= 2 && pairs > 0;
        if (GUILayout.Button("Random Khoá + Chìa", GUILayout.Height(22f)))
            pending = () => RandomLockKeyPairs(data);
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    /// <summary>Chấm màu đúng màu nền khung bóng của vật cản, cho khỏi phải đọc chữ mới biết nút nào ra nút nào.</summary>
    private static void DrawObstacleColorChip(ObstacleStyle style)
    {
        Rect chip = GUILayoutUtility.GetRect(ChipWidth, ChipHeight, GUILayout.Width(ChipWidth),
            GUILayout.Height(ChipHeight));
        Color color = style.Color;
        color.a = Mathf.Clamp01(color.a * 1.8f);
        DrawRoundedRect(chip, color, SirenixGUIStyles.BorderColor);
    }

    /// <summary>Câu nhắc cho các vật cản có luật riêng; vật cản chỉ bật/tắt thì không cần nói gì.</summary>
    private static string ObstacleNote(BubbleObstacle obstacle)
    {
        return obstacle switch
        {
            BubbleObstacle.Rock => "Bóng đá không chứa cá: cá bên trong bị rải sang các bóng khác.",
            BubbleObstacle.Spawner => "Cá trên spawner bốc trong các loài đang có đơn.",
            _ => null
        };
    }

    /// <summary>
    /// Bật một vật cản cho ngẫu nhiên N bóng chưa có nó. Chọn xong hết rồi mới sửa, và giữ tham chiếu SoftBubbleData
    /// chứ không giữ index: Đá còn phải rải cá đi chỗ khác, mà khâu đó chèn thêm bóng mới nên index xô hết.
    /// </summary>
    private void RandomObstacle(LevelDesignData data, ObstacleStyle style)
    {
        BubbleObstacle obstacle = style.Obstacle;
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
        if (bubbles == null || bubbles.Length == 0)
        {
            SetStatus(false, "Chưa có bóng nào - bấm Random Bubble trước");
            return;
        }

        int want = ObstacleCount(obstacle);
        if (want <= 0)
        {
            SetStatus(false, $"\"{style.Label}\": số bóng phải lớn hơn 0");
            return;
        }

        var candidates = new List<SoftBubbleData>();
        foreach (SoftBubbleData bubble in bubbles)
        {
            if (CanRandomizeObstacle(bubble, obstacle)) candidates.Add(bubble);
        }

        if (candidates.Count == 0)
        {
            SetStatus(false, $"Không còn bóng nào nhận thêm \"{style.Label}\"");
            return;
        }

        int applied = Mathf.Min(want, candidates.Count);

        // Fisher-Yates nửa chừng: chỉ cần applied phần tử đầu là đã bốc ngẫu nhiên đúng luật.
        for (int i = 0; i < applied; i++)
        {
            int j = UnityEngine.Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int value = ObstacleValue(obstacle);
        Mutate(data, $"Random {style.Label}", () =>
        {
            for (int i = 0; i < applied; i++)
            {
                SoftBubbleData bubble = candidates[i];
                DesignConfig.SetFlag(bubble, obstacle, true);
                switch (obstacle)
                {
                    case BubbleObstacle.BubbleHp:
                        bubble.bubbleHp = Mathf.Max(1, value);
                        break;

                    case BubbleObstacle.Spawner:
                        bubble.fishOnSpawner = RandomSpawnerFish(data, value);
                        break;

                    case BubbleObstacle.Rock:
                        EvacuateFishFromRock(data, bubble);
                        break;
                }
            }
        });

        // Đá dựng lại cả mảng bóng nên ô cá đang chọn có thể trỏ vào chỗ khác.
        if (obstacle == BubbleObstacle.Rock) ResetSelection();

        SetStatus(true, applied < want
            ? $"Đã đặt \"{style.Label}\" cho {applied} bóng - chỉ còn bấy nhiêu bóng hợp lệ"
            : $"Đã đặt \"{style.Label}\" cho {applied} bóng");
    }

    /// <summary>
    /// Khoá và chìa luôn ra theo cặp: mỗi cặp bốc 2 bóng trống, bóng đứng trước làm chìa (keyId = softBubbleId của
    /// bóng đứng sau), bóng đứng sau làm ổ khoá. Chìa nằm trước nên bao giờ cũng lấy được trước ổ khoá của nó, và
    /// không đời nào ra tình trạng khoá thừa / chìa thiếu như lúc bốc rời từng loại.
    /// </summary>
    private void RandomLockKeyPairs(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
        if (bubbles == null || bubbles.Length < 2)
        {
            SetStatus(false, "Cần ít nhất 2 bóng mới đặt được một cặp khoá - chìa");
            return;
        }

        int want = ObstacleCount(BubbleObstacle.Lock);
        if (want <= 0)
        {
            SetStatus(false, "Số cặp phải lớn hơn 0");
            return;
        }

        // Giữ index chứ không giữ tham chiếu: còn phải biết bóng nào đứng trước để giao chìa cho đúng.
        var candidates = new List<int>();
        for (int i = 0; i < bubbles.Length; i++)
        {
            SoftBubbleData bubble = bubbles[i];
            if (bubble == null || bubble.isLock || bubble.isKey) continue;
            candidates.Add(i);
        }

        int pairs = Mathf.Min(want, candidates.Count / 2);
        if (pairs <= 0)
        {
            SetStatus(false, "Không còn đủ 2 bóng trống để thêm một cặp khoá - chìa");
            return;
        }

        int take = pairs * 2;
        for (int i = 0; i < take; i++)
        {
            int j = UnityEngine.Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        Mutate(data, "Random cặp khoá - chìa", () =>
        {
            for (int i = 0; i < take; i += 2)
            {
                SoftBubbleData key = bubbles[Mathf.Min(candidates[i], candidates[i + 1])];
                SoftBubbleData padlock = bubbles[Mathf.Max(candidates[i], candidates[i + 1])];

                DesignConfig.SetFlag(padlock, BubbleObstacle.Lock, true);
                DesignConfig.SetFlag(key, BubbleObstacle.Key, true);
                key.keyId = padlock.softBubbleId;
            }
        });

        SetStatus(true, pairs < want
            ? $"Đã thêm {pairs} cặp khoá - chìa, hết bóng trống nên chưa đủ {want} cặp"
            : $"Đã thêm {pairs} cặp khoá - chìa");
    }

    private void ClearAllObstacles(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
        if (bubbles == null || bubbles.Length == 0)
        {
            SetStatus(false, "Chưa có bóng nào để xoá vật cản");
            return;
        }

        Mutate(data, "Xoá hết vật cản", () =>
        {
            foreach (SoftBubbleData bubble in bubbles)
            {
                if (bubble == null) continue;
                foreach (BubbleObstacle obstacle in DesignConfig.AllObstacles)
                {
                    DesignConfig.SetFlag(bubble, obstacle, false);
                }
            }
        });

        ResetSelection();
        SetStatus(true, $"Đã tắt hết vật cản trên {bubbles.Length} bóng");
    }

    private static bool CanRandomizeObstacle(SoftBubbleData bubble, BubbleObstacle obstacle)
    {
        if (bubble == null || DesignConfig.GetFlag(bubble, obstacle)) return false;

        // Bóng đá không chứa cá nên cũng không làm spawner được.
        return obstacle != BubbleObstacle.Spawner || !bubble.isRock;
    }

    /// <summary>
    /// Cá cho spawner bốc trong các loài đang có đơn (không có đơn thì bốc trong palette). Lưu ý cá trên spawner
    /// không tính vào phép cân "số cá = số đơn × TankGoal" mà Validate kiểm.
    /// </summary>
    private FishData[] RandomSpawnerFish(LevelDesignData data, int count)
    {
        if (count <= 0) return Array.Empty<FishData>();

        int[] pool = data.levelData.OrderQueue is { Length: > 0 } orders ? orders : fishIds;
        if (pool == null || pool.Length == 0) return Array.Empty<FishData>();

        var fish = new FishData[count];
        for (int i = 0; i < count; i++)
        {
            fish[i] = new FishData { FishId = pool[UnityEngine.Random.Range(0, pool.Length)] };
        }

        return fish;
    }
}
