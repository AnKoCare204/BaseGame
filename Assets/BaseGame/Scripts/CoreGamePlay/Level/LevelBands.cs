using Core.GamePlay;
using UnityEngine;

/// <summary>
/// Band theo level của Docs/GDD/04_Config.tsv (LevelGenBandsModel) cho tool sinh level, cộng các luật cố định lấy từ
/// GamePlayGlobalConfig (bể mở sẵn, ô chờ, số bóng hiện, cỡ bóng). Thuần C# ngoài việc đọc config.
/// </summary>
public static class LevelBands
{
    public const int OrderBonusHard = 2;
    public const int OrderBonusSuperHard = 4;
    public const int FishPerBubbleMin = 1;
    public const int MaxFishTypes = 14;

    /// <summary>
    /// Bề ngang vùng chơi (đơn vị world) mà LevelSolver dùng để ước lượng hàng trên cùng chứa được bao nhiêu bóng khi xét bóng băng.
    /// GamePlayRectManager tính rect thật lúc chạy theo camera (~12.8 ở tỉ lệ 9:16). Nhân TopRowFill vì bóng tròn không xếp kín hàng.
    /// </summary>
    public const float TopRowWidth = 12.8f;

    public const float TopRowFill = 0.9f;

    private static GamePlayGlobalConfig Config => GamePlayGlobalConfig.Instance;

    public static int TankGoal => GamePlayGlobalConfig.TankGoal;

    /// <summary>Số bể mở sẵn lúc bắt đầu (mặc định 2; 2 bể còn lại khóa, mở bằng booster).</summary>
    public static int DefaultTankSlots => Config != null ? Mathf.Max(1, Config.DefaultTankSlots) : 2;

    public static int MaxTankSlots => Config != null ? Mathf.Max(DefaultTankSlots, Config.MaxTankSlots) : 4;

    /// <summary>Số ô chờ hiện sẵn (mặc định 5; booster ExtraSlot lên 6).</summary>
    public static int DefaultHoldingSlots => Config != null ? Mathf.Max(1, Config.DefaultHoldingSlots) : 5;

    /// <summary>Số bóng hiện trên bàn (mặc định 15): bóng đầu sinh ngay, nổ một thì sinh thêm một.</summary>
    public static int VisibleBubbles => Config != null ? Mathf.Max(1, Config.VisibleBubbleCount) : 15;

    public static float BubbleRadiusMin => Config != null ? Mathf.Min(Config.BubbleRadiusMin, Config.BubbleRadiusMax) : 0.9f;
    public static float BubbleRadiusMax => Config != null ? Mathf.Max(Config.BubbleRadiusMin, Config.BubbleRadiusMax) : 1.5f;

    // Tag độ khó theo số level KHÔNG còn ở đây nữa: luật nằm trong LevelDifficultRuleGlobalConfig.GetDifficulty(level),
    // chỉnh band ngoài Inspector. Để lại một bản chép cứng ở đây là sớm muộn hai bên lệch nhau.

    public static int FishTypes(int level)
    {
        if (level <= 2) return 2;
        if (level <= 6) return 3;
        if (level <= 14) return 4;
        if (level <= 29) return 5;
        if (level <= 59) return 6;
        if (level <= 99) return 7;
        if (level <= 149) return 8;
        return 9;
    }

    public static int OrderBase(int level)
    {
        if (level <= 1) return 4;
        if (level == 2) return 5;
        if (level <= 4) return 6;
        if (level <= 9) return 7 + (level - 5) / 2;
        if (level <= 19) return 10 + (level - 10) / 2;
        if (level <= 29) return 14 + (level - 20) / 2;
        if (level <= 49) return 18 + (level - 30) / 4;
        if (level <= 99) return 22 + (level - 50) / 6;
        if (level <= 149) return 30 + (level - 100) / 8;
        return 36 + (level - 150) / 10;
    }

    public static int OrderCount(int level, LevelDifficulty difficulty)
    {
        int count = OrderBase(level);
        if (difficulty == LevelDifficulty.Hard) count += OrderBonusHard;
        else if (difficulty == LevelDifficulty.SuperHard) count += OrderBonusSuperHard;
        return count;
    }

    public static int FishPerBubbleMax(int level)
    {
        if (level <= 4) return 2;
        if (level <= 29) return 3;
        return 4;
    }

    /// <summary>
    /// Xác suất một bóng lộ ra chỉ chứa loài chưa cần (người chơi phải đẩy cá vào ô chờ để mở đường). Tăng dần theo level,
    /// Hard/SuperHard cao hơn; đây là nút vặn độ khó chính của LevelGenerator (chỉnh cùng TargetWinRate).
    /// </summary>
    public static float BlockChance(int level, LevelDifficulty difficulty)
    {
        if (level <= 3) return 0f;
        float baseChance = Mathf.Lerp(0.15f, 0.45f, Mathf.Clamp01((level - 4) / 60f));
        return difficulty switch
        {
            LevelDifficulty.Hard => Mathf.Min(0.75f, baseChance + 0.2f),
            LevelDifficulty.SuperHard => Mathf.Min(0.85f, baseChance + 0.35f),
            _ => baseChance
        };
    }

    /// <summary>
    /// Tỉ lệ thắng mục tiêu của người thật, không booster (GDD BAL-02). Từ 2026-10-16 không còn là mục tiêu của bot nào
    /// (trừ L1-9: 99 / 92%, bot như người vẫn thắng gần hết ở đó); mục tiêu trên thước bot nằm ở
    /// LevelHumanBot.*TargetWinRate, quy từ các số này theo report winrate.
    /// </summary>
    public static float TargetWinRate(int level, LevelDifficulty difficulty)
    {
        if (level <= 3) return 0.99f;
        if (level <= 9) return 0.92f;
        return difficulty switch
        {
            LevelDifficulty.Hard => 0.60f,
            LevelDifficulty.SuperHard => 0.40f,
            _ => 0.85f
        };
    }
}
