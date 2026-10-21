using System.Linq;
using Core.GamePlay;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Data L1-L3 của FTUE (spec docs/superpowers/specs/2026-10-05-ftue-phase1-revision-design.md mục 6: 3 order mở, 3 / 4 / 5 loài). Menu ghi đè LevelData_1..3.json qua
/// LevelDesignData (cùng đường Save của Tools/FishSort/Level Design: Validate, import, đồng bộ LevelGlobalConfig).
/// FtueLevelDataTests đọc lại file đã ghi và so với bảng ở đây.
/// </summary>
public static class FtueLevelSeeds
{
    private const string DesignDataPath = "Assets/Resources/GlobalConfig/LevelDesignData.asset";

    public static readonly int[] L1Queue = { 7, 0, 10, 7, 0, 10 };
    public static readonly int[][] L1Bubbles =
    {
        new[] { 7, 7, 7 }, new[] { 0, 0, 0 }, new[] { 10, 10, 10 }, new[] { 7, 7, 7 }, new[] { 0, 0, 0 }, new[] { 10, 10, 10 }
    };

    /// <summary>Loài X của kịch bản L2: order thứ 4, không nằm trong 3 order mở lúc đầu, có ở bóng đầu.</summary>
    public const int L2HoldingSpecies = 11;
    public static readonly int[] L2Queue = { 0, 10, 7, 11, 0, 10, 7, 11, 0 };
    public static readonly int[][] L2Bubbles =
    {
        new[] { 7, 0, 11 }, new[] { 11, 0, 10 }, new[] { 0, 11, 11 }, new[] { 0, 0, 11 }, new[] { 0, 10, 10 },
        new[] { 7, 7, 0 }, new[] { 0, 0, 10 }, new[] { 7, 11, 10 }, new[] { 10, 7, 7 }
    };

    public static readonly int[] L3Queue = { 0, 10, 11, 7, 13, 0, 10, 11, 7, 13, 0, 10, 11, 7, 13 };
    public static readonly int[][] L3Bubbles =
    {
        new[] { 13, 11, 7, 0 }, new[] { 11, 0, 10, 0 }, new[] { 7, 0, 10, 10 }, new[] { 11, 0, 11, 10 }, new[] { 11, 13, 10, 13 },
        new[] { 7, 7, 13, 0 }, new[] { 0, 10, 11, 7 }, new[] { 10, 13, 0, 11 }, new[] { 10, 13, 13, 7 }, new[] { 11, 13, 7 },
        new[] { 7, 11, 0 }, new[] { 13, 10, 7 }
    };

    public static LevelData Build(int level)
    {
        (int[] queue, int[][] bubbles) = level switch
        {
            1 => (L1Queue, L1Bubbles),
            2 => (L2Queue, L2Bubbles),
            3 => (L3Queue, L3Bubbles),
            _ => throw new System.ArgumentOutOfRangeException(nameof(level))
        };
        SoftBubbleData[] data = bubbles.Select((fish, i) => new SoftBubbleData
        {
            softBubbleId = i + 1,
            FishDataArray = fish.Select(id => new FishData { FishId = id }).ToArray(),
            Radius = RadiusFor(fish.Length)
        }).ToArray();
        return LevelData.Create(level, LevelDifficulty.Normal, (int[])queue.Clone(), data);
    }

    /// <summary>Bán kính theo số cá, cùng thang data hiện có (1 → 2.0, 2 → 2.1, 3 → 2.3).</summary>
    private static float RadiusFor(int count) => count switch
    {
        1 => 2.0f,
        2 => 2.1f,
        3 => 2.3f,
        4 => 2.6f,
        _ => 2.9f
    };

    /// <summary>Thư mục level người chơi nhận (Addressables Levels_Main). LevelDesignData.levelFolder có thể đang trỏ bộ test (TestLevel/VersionA).</summary>
    private const string LevelFolder = "Assets/BaseGame/TextAssets/LevelDataConverted";

    [MenuItem("Tools/FishSort/FTUE/Write Levels 1-3")]
    public static void WriteAll()
    {
        var design = AssetDatabase.LoadAssetAtPath<LevelDesignData>(DesignDataPath);
        if (design == null)
        {
            Debug.LogError($"[FtueLevelSeeds] Không thấy {DesignDataPath}");
            return;
        }

        // Tool đang mở level / thư mục nào thì giữ nguyên: đổi tạm sang thư mục level thật, ghi xong bỏ bản trong bộ nhớ
        // (UnloadAsset) để lần nạp sau đọc lại đúng asset trên đĩa (SaveLevel không lưu LevelDesignData.asset).
        string previousFolder = design.levelFolder;
        design.levelFolder = LevelFolder;
        try
        {
            for (int level = 1; level <= 3; level++)
            {
                design.LoadLevel(level, out string loadMessage);
                LevelData seed = Build(level);
                design.levelData.EditorSetOrderQueue(seed.OrderQueue);
                design.levelData.EditorSetBubbles(seed.SoftBubbleDataArray);
                bool ok = design.SaveLevel(out string saveMessage);
                Debug.Log($"[FtueLevelSeeds] L{level}: {(ok ? "OK" : "LỖI")} - {loadMessage} → {saveMessage}");
            }
        }
        finally
        {
            design.levelFolder = previousFolder;
            Resources.UnloadAsset(design);
        }
    }
}
