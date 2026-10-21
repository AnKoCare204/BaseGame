using System.Collections.Generic;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using UnityEngine;

/// <summary>
/// Người chơi vượt quá kho level thật (LevelGlobalConfig.MaxPlayableLevel) thì chơi lại một level cũ:
/// lấy độ khó mục tiêu của số level đó từ <see cref="LevelDifficultRuleGlobalConfig"/>, rồi random một level nguồn
/// CÙNG độ khó trong khoảng [LoopSourceFromLevel .. MaxLevel - LoopSourceEndOffset], tránh trùng các level vừa quay gần đây.
/// Kết quả được ghim vào UserData theo số level nên retry / thua / mở lại game vẫn ra đúng level đó; qua level mới mới quay lại.
/// Số level hiển thị cho người chơi vẫn là số thật (101, 102...), chỉ nội dung màn là level cũ.
/// </summary>
public static class LevelLoopResolver
{
    /// <summary>
    /// Level này đã vượt kho level thật. Nút Play ngoài Home dùng hàm này để chặn lại và hiện ModalComingSoon
    /// thay vì cho chơi tiếp bằng nội dung level cũ; phần loop bên dưới chỉ còn là lưới an toàn cho các
    /// đường vào khác (cheat nhảy level, tool editor).
    /// </summary>
    public static bool IsBeyondMaxLevel(int playerLevel)
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        return config != null && !config.IsLevelPlayable(playerLevel) && playerLevel >= 1;
    }

    /// <summary>Số level người chơi đang ở -> level trong kho dùng để nạp LevelData. Chưa vượt max thì trả lại chính nó.</summary>
    public static int ResolveSourceLevel(int playerLevel)
    {
        playerLevel = Mathf.Max(1, playerLevel);
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        if (config == null) return playerLevel;

        int maxLevel = config.MaxPlayableLevel;
        if (maxLevel <= 0 || playerLevel <= maxLevel) return playerLevel;

        UserData userData = TryGetUserData();
        if (userData != null && userData.LoopSourceForLevel == playerLevel &&
            userData.LoopSourceLevel >= 1 && userData.LoopSourceLevel <= maxLevel)
        {
            return userData.LoopSourceLevel;
        }

        int sourceLevel = PickSourceLevel(config, playerLevel, userData);
        userData?.SetLoopSourceLevel(playerLevel, sourceLevel, config.LoopAvoidRecentCount);
        return sourceLevel;
    }

    private static int PickSourceLevel(LevelGlobalConfig config, int playerLevel, UserData userData)
    {
        int maxLevel = config.MaxPlayableLevel;
        int fromLevel = Mathf.Clamp(config.LoopSourceFromLevel, 1, maxLevel);
        int toLevel = Mathf.Clamp(maxLevel - config.LoopSourceEndOffset, fromLevel, maxLevel);

        LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;
        LevelDifficulty wantedDifficulty = rule != null ? rule.GetDifficulty(playerLevel) : LevelDifficulty.Normal;

        var candidates = new List<int>();
        for (int level = fromLevel; level <= toLevel; level++)
        {
            if (config.TryGetCachedDifficulty(level, out LevelDifficulty difficulty) && difficulty == wantedDifficulty)
            {
                candidates.Add(level);
            }
        }

        // Kho level không có màn nào đúng độ khó trong khoảng (khoảng quá hẹp, hoặc chưa thiết kế đủ màn SuperHard)
        // -> thà chơi sai độ khó còn hơn không nạp được level.
        if (candidates.Count == 0)
        {
            for (int level = fromLevel; level <= toLevel; level++)
            {
                if (config.TryGetCachedDifficulty(level, out _)) candidates.Add(level);
            }

            Debug.LogWarning($"[LevelLoopResolver] Level {playerLevel}: không có level {wantedDifficulty} nào trong khoảng " +
                             $"{fromLevel}-{toLevel}, lấy tạm level bất kỳ trong khoảng (nới LoopSourceFromLevel " +
                             "hoặc sửa LevelDifficultRuleGlobalConfig).");
        }

        if (candidates.Count == 0) return Mathf.Clamp(playerLevel, 1, maxLevel);

        List<int> pool = ExcludeRecent(candidates, userData);
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    /// <summary>Bỏ các level vừa quay gần đây. Loại hết thì chỉ cần khác level ngay trước đó là đủ.</summary>
    private static List<int> ExcludeRecent(List<int> candidates, UserData userData)
    {
        List<int> recent = userData?.RecentLoopSourceLevels;
        if (recent == null || recent.Count == 0 || candidates.Count <= 1) return candidates;

        var fresh = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!recent.Contains(candidates[i])) fresh.Add(candidates[i]);
        }

        if (fresh.Count > 0) return fresh;

        int lastUsed = userData.LoopSourceLevel;
        var notLastUsed = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != lastUsed) notLastUsed.Add(candidates[i]);
        }

        return notLastUsed.Count > 0 ? notLastUsed : candidates;
    }

    /// <summary>Editor tool (ValidateAll, LevelDesignData) chạy ngoài Play mode thì chưa có save data.</summary>
    private static UserData TryGetUserData()
    {
        if (!Application.isPlaying) return null;
        return UserData.Instance;
    }
}
