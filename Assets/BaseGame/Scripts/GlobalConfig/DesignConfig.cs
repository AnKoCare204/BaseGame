using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

/// <summary>Các cờ vật cản "is{...}" của một bóng trong SoftBubbleData, gom lại thành enum để tool vẽ level duyệt được.</summary>
public enum BubbleObstacle
{
    // 1 = Hidden đã bỏ; các số còn lại giữ nguyên để asset DesignConfig và data cũ không bị xô lệch.
    Ice = 0,
    Spawner = 2,
    Lock = 3,
    Key = 4,
    BubbleHp = 5,
    Rock = 6,
    OrderBubble = 7,
    MultiLayer = 8,
    Bundle = 9,
    Conveyor = 10
}

/// <summary>Cách hiện một vật cản trong tool vẽ level: nhãn, màu nền khung bóng và phím tắt bật/tắt.</summary>
[Serializable]
public class ObstacleStyle
{
    public BubbleObstacle Obstacle;

    [Tooltip("Tên đầy đủ, hiện ở bảng chú thích bên phải")]
    public string Label = "";

    [Tooltip("Nhãn ngắn in trên nút bật/tắt trong khung bóng")]
    public string ShortLabel = "";

    [Tooltip("Màu nền khung bóng khi cờ này bật; để alpha thấp cho chữ còn đọc được")]
    public Color Color = Color.white;

    [Tooltip("Rê chuột lên khung bóng rồi bấm phím này để bật/tắt cờ")]
    public KeyCode Hotkey = KeyCode.None;
}

/// <summary>
/// Cấu hình riêng cho khâu design: kích thước lưới, bảng màu và phím tắt của tool vẽ level (LevelDesignData).
/// Không có gì ảnh hưởng gameplay - đổi thoải mái để hợp mắt người dựng level.
/// </summary>
[CreateAssetMenu(fileName = "DesignConfig", menuName = "GlobalConfigs/DesignConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class DesignConfig : GlobalConfig<DesignConfig>
{
    public static readonly BubbleObstacle[] AllObstacles =
    {
        BubbleObstacle.Ice, BubbleObstacle.Spawner, BubbleObstacle.Lock,
        BubbleObstacle.Key, BubbleObstacle.BubbleHp, BubbleObstacle.Rock, BubbleObstacle.OrderBubble,
        BubbleObstacle.MultiLayer, BubbleObstacle.Bundle, BubbleObstacle.Conveyor
    };

    public const int MaxBubblesPerRow = 5;

    [Title("Lưới bóng")]
    [field: SerializeField, Range(1, MaxBubblesPerRow), Tooltip("Số khung bóng trên một hàng trước khi xuống hàng mới (tối đa 5)")]
    public int BubblesPerRow { get; private set; } = MaxBubblesPerRow;

    [field: SerializeField, Min(1), Tooltip("Số ô cá trên một hàng bên trong khung bóng")]
    public int FishPerRow { get; private set; } = 3;

    [field: SerializeField, Range(24f, 200f), Tooltip("Cạnh ô cá trong khung bóng (px) — bề ngang và phần icon")]
    public float FishSlotSize { get; private set; } = 70f;

    [field: SerializeField, Range(0f, 80f), Tooltip("Thêm chiều cao ô cá trong khung bóng (px)")]
    public float FishSlotExtraHeight { get; private set; } = 18f;

    [field: SerializeField, Range(24f, 200f), Tooltip("Cạnh ô cá ở cột palette và cột OrderQueue (px)")]
    public float PaletteSlotSize { get; private set; } = 70f;

    [field: SerializeField, Min(1), Tooltip("Không dùng cho List item cá / OrderQueue (tool luôn 3 ô/hàng). Giữ cho cấu hình cũ.")]
    public int OrderPerRow { get; private set; } = 3;

    [Title("Màu nền")]
    [field: SerializeField, Tooltip("Nền khung bóng khi không có vật cản nào")]
    public Color BubbleColorDefault { get; private set; } = new Color(1f, 1f, 1f, 0.04f);

    [field: SerializeField, Tooltip("Nền ô cá trống")]
    public Color SlotEmptyColor { get; private set; } = new Color(0f, 0f, 0f, 0.25f);

    [field: SerializeField, Tooltip("Nền ô cá đã có cá")]
    public Color SlotFilledColor { get; private set; } = new Color(1f, 1f, 1f, 0.12f);

    [field: SerializeField, Tooltip("Viền ô / khung bóng đang được rê chuột lên")]
    public Color HoverColor { get; private set; } = new Color(1f, 0.78f, 0.2f, 1f);

    [Title("Phím tắt tool")]
    [field: SerializeField, Tooltip("Xoá bóng hoặc ô cá đang chọn. None = tắt")]
    public KeyCode DeleteHotkey { get; private set; } = KeyCode.Delete;

    [field: SerializeField, Tooltip("Chèn bóng trống vào vị trí bóng đang chọn. None = tắt")]
    public KeyCode InsertBubbleHotkey { get; private set; } = KeyCode.A;

    [Title("Icon vật cản")]
    [field: SerializeField, PreviewField, Tooltip("Icon hiện trên bóng isRock trong tool vẽ level")]
    public Texture2D RockIcon { get; private set; }

    [Title("Vật cản: màu + phím tắt")]
    [field: SerializeField]
    public List<ObstacleStyle> ObstacleStyles { get; private set; } = CreateDefaultStyles();

    /// <summary>Bù các vật cản còn thiếu trong danh sách (khi thêm cờ mới vào SoftBubbleData) mà không đụng phần đã chỉnh.</summary>
    [Button("Bù vật cản còn thiếu")]
    public void FillMissingStyles()
    {
        ObstacleStyles ??= new List<ObstacleStyle>();
        List<ObstacleStyle> defaults = CreateDefaultStyles();
        foreach (ObstacleStyle fallback in defaults)
        {
            if (ObstacleStyles.Exists(s => s != null && s.Obstacle == fallback.Obstacle)) continue;
            ObstacleStyles.Add(fallback);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [Button("Trả bảng màu về mặc định")]
    public void ResetStyles()
    {
        ObstacleStyles = CreateDefaultStyles();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public ObstacleStyle GetStyle(BubbleObstacle obstacle)
    {
        if (ObstacleStyles == null) return null;
        for (int i = 0; i < ObstacleStyles.Count; i++)
        {
            if (ObstacleStyles[i] != null && ObstacleStyles[i].Obstacle == obstacle) return ObstacleStyles[i];
        }

        return null;
    }

    /// <summary>Màu nền khung bóng: lấy theo vật cản bật đầu tiên trong danh sách, không có thì dùng BubbleColorDefault.</summary>
    public Color GetBubbleColor(SoftBubbleData bubble)
    {
        if (bubble == null || ObstacleStyles == null) return BubbleColorDefault;
        for (int i = 0; i < ObstacleStyles.Count; i++)
        {
            ObstacleStyle style = ObstacleStyles[i];
            if (style != null && GetFlag(bubble, style.Obstacle)) return style.Color;
        }

        return BubbleColorDefault;
    }

    public bool TryGetObstacleByHotkey(KeyCode key, out BubbleObstacle obstacle)
    {
        obstacle = BubbleObstacle.Ice;
        if (key == KeyCode.None || ObstacleStyles == null) return false;
        for (int i = 0; i < ObstacleStyles.Count; i++)
        {
            ObstacleStyle style = ObstacleStyles[i];
            if (style == null || style.Hotkey != key) continue;
            obstacle = style.Obstacle;
            return true;
        }

        return false;
    }

    public static bool GetFlag(SoftBubbleData bubble, BubbleObstacle obstacle)
    {
        if (bubble == null) return false;
        return obstacle switch
        {
            BubbleObstacle.Ice => bubble.isIce,
            BubbleObstacle.Spawner => bubble.isSpawner,
            BubbleObstacle.Lock => bubble.isLock,
            BubbleObstacle.Key => bubble.isKey,
            BubbleObstacle.BubbleHp => bubble.isBubbleHp,
            BubbleObstacle.Rock => bubble.isRock,
            BubbleObstacle.OrderBubble => bubble.isOrderBubble,
            BubbleObstacle.MultiLayer => bubble.isMultiLayer,
            BubbleObstacle.Bundle => bubble.isBundleBubble,
            BubbleObstacle.Conveyor => bubble.isConveyor,
            _ => false
        };
    }

    public static void SetFlag(SoftBubbleData bubble, BubbleObstacle obstacle, bool value)
    {
        if (bubble == null) return;
        switch (obstacle)
        {
            case BubbleObstacle.Ice:
                bubble.isIce = value;
                break;
            case BubbleObstacle.Spawner:
                bubble.isSpawner = value;
                if (!value) bubble.fishOnSpawner = Array.Empty<FishData>();
                break;
            case BubbleObstacle.Lock:
                bubble.isLock = value;
                break;
            case BubbleObstacle.Key:
                bubble.isKey = value;
                if (!value) bubble.keyId = 0;
                break;
            case BubbleObstacle.BubbleHp:
                bubble.isBubbleHp = value;
                if (!value) bubble.bubbleHp = 0;
                else if (bubble.bubbleHp <= 0) bubble.bubbleHp = 1;
                break;
            case BubbleObstacle.Rock:
                bubble.isRock = value;
                if (value) bubble.FishDataArray = Array.Empty<FishData>();   // bóng đá không nuôi cá
                break;
            case BubbleObstacle.OrderBubble:
                bubble.isOrderBubble = value;
                if (!value) bubble.fishOrderId = 0;   // tắt bóng đơn thì bỏ luôn cá của đơn
                break;
            case BubbleObstacle.MultiLayer:
                bubble.isMultiLayer = value;
                // Tắt thì bỏ hết lớp ẩn; bật thì dựng sẵn một lớp rỗng để thanh chuyển lớp có chỗ đi tới.
                if (!value) bubble.layers = Array.Empty<BubbleLayerData>();
                else if (bubble.layers == null || bubble.layers.Length == 0)
                    bubble.layers = new[] { new BubbleLayerData { index = 0 } };
                break;
            case BubbleObstacle.Bundle:
                bubble.isBundleBubble = value;   // chỉ một cờ, không có data kèm để dọn
                break;
            case BubbleObstacle.Conveyor:
                bubble.isConveyor = value;       // cá bên trong vẫn như bóng thường, chỉ khác cách trình bày
                break;
        }
    }

    private static List<ObstacleStyle> CreateDefaultStyles()
    {
        return new List<ObstacleStyle>
        {
            Style(BubbleObstacle.Ice, "Băng (isIce)", "Băng", new Color(0.29f, 0.62f, 0.95f, 0.45f), KeyCode.I),
            Style(BubbleObstacle.Spawner, "Sinh bóng (isSpawner)", "Sinh", new Color(0.35f, 0.80f, 0.52f, 0.45f), KeyCode.S),
            Style(BubbleObstacle.Lock, "Ổ khoá (isLock)", "Khoá", new Color(0.90f, 0.36f, 0.32f, 0.45f), KeyCode.L),
            Style(BubbleObstacle.Key, "Chìa khoá (isKey)", "Chìa", new Color(0.95f, 0.60f, 0.29f, 0.45f), KeyCode.K),
            Style(BubbleObstacle.BubbleHp, "Máu bóng (isBubbleHp)", "HP", new Color(0.66f, 0.51f, 0.93f, 0.45f), KeyCode.B),
            Style(BubbleObstacle.Rock, "Ngọc trai (isRock)", "Ngọc", new Color(0.93f, 0.85f, 0.92f, 0.5f), KeyCode.R),
            Style(BubbleObstacle.OrderBubble, "Order bubble", "Order", new Color(0.24f, 0.78f, 0.75f, 0.45f), KeyCode.O),
            Style(BubbleObstacle.MultiLayer, "Nhiều lớp (isMultiLayer)", "Lớp", new Color(0.85f, 0.45f, 0.72f, 0.45f), KeyCode.M),
            Style(BubbleObstacle.Bundle, "Bundle bubble", "Bundle", new Color(0.98f, 0.85f, 0.30f, 0.45f), KeyCode.G),
            Style(BubbleObstacle.Conveyor, "Băng chuyền (isConveyor)", "Băng", new Color(0.62f, 0.42f, 0.24f, 0.45f), KeyCode.C)
        };
    }

    private static ObstacleStyle Style(BubbleObstacle obstacle, string label, string shortLabel, Color color, KeyCode hotkey)
    {
        return new ObstacleStyle
        {
            Obstacle = obstacle,
            Label = label,
            ShortLabel = shortLabel,
            Color = color,
            Hotkey = hotkey
        };
    }
}
