namespace CoreData
{
    /// <summary>
    /// Tiền tệ và booster dùng chung một enum (khung AntFood/FishSort). Số được serialize trong prefab / GlobalConfig / save:
    /// không đổi số đã có, chỉ thêm số mới. Booster nằm trong dải 51..59 (IsBooster); game mới đổi tên / thêm booster trong dải này.
    /// </summary>
    public enum ResourceType
    {
        None = 0,
        Money = 1,
        Life = 2,

        NoAds = 20,

        Hint = 51,
        Undo = 52,
        Shuffle = 53,

        InfiniteLife = 102,
        BonusLife = 103,

        Star = 110,

        // Profile cosmetics: Value = index trong ProfileGlobalConfig reward lists
        // (badgeRewards / frameRewards / avatarRewards), không phải số lượng.
        Badge = 300,
        Frame = 301,
        Avatar = 302,
        NameEffect = 303,
    }

    /// <summary>Placement / claim type gửi kèm analytics (dùng ToString()). Số ghi tường minh để thêm bớt không xê dịch giá trị khác.</summary>
    public enum ResourcePos
    {
        unknown = -1,
        home = 0,
        ingame = 1,

        ads = 50,
        iap = 51,
        coin = 52,
        free = 53,
        ads_break = 54,

        refill_heart = 100,

        play_level = 200,
        win_level = 201,
        lose_level = 202,
        quit_level = 203,
        replay_level = 204,
        treasure_reward = 205,
        open_slot = 206,

        revive = 300,
        booster_hint = 301,
        booster_undo = 302,
        booster_shuffle = 303,

        buy_booster = 307,
        play_on = 308,
        shop = 309,
        popup_noads = 310,
    }

    public static class ResourceTypeExtensions
    {
        public const ResourceType FirstBooster = ResourceType.Hint;
        public const ResourceType LastBooster = ResourceType.Shuffle;

        public static bool IsBooster(this ResourceType type)
        {
            return type >= FirstBooster && type <= LastBooster;
        }

        /// <summary>ResourcePos chi tiêu tương ứng với booster (booster_hint, booster_undo, booster_shuffle).</summary>
        public static ResourcePos ToSpendPos(this ResourceType type)
        {
            return type.IsBooster() ? (ResourcePos)((int)ResourcePos.booster_hint + (type - FirstBooster)) : ResourcePos.unknown;
        }
    }
}
