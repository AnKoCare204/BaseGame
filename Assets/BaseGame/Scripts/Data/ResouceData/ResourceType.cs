namespace CoreData
{
    /// <summary>
    /// Tiền tệ và booster dùng chung một enum (khung AntFood). Booster theo Docs/GDD/08_Boosters.tsv.
    /// Undo (50) đã bỏ: không làm booster này nữa.
    /// </summary>
    public enum ResourceType
    {
        None = 0,
        Money = 1,
        Life = 2,

        NoAds = 20,

        AddSlot = 51,
        AutoFill = 52,
        Shuffle = 53,
        PackedUp = 54,
        Swap = 55,
        UnlockTank = 56,

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

    public enum ResourcePos
    {
        unknown = -1,
        home,
        ingame,

        ads = 50,
        iap,
        coin,
        free,
        ads_break,

        refill_heart = 100,

        play_level = 200,
        win_level,
        lose_level,
        quit_level,
        replay_level,
        treasure_reward,
        open_slot,

        revive = 300,
        booster_hint,
        booster_magnet,
        booster_shuffle,
        booster_extraslot,
        booster_swap,
        booster_unlocktank,

        buy_booster,
        play_on,
        shop,
        popup_noads
    }

    public static class ResourceTypeExtensions
    {
        public static bool IsBooster(this ResourceType type)
        {
            return type >= ResourceType.AddSlot && type <= ResourceType.UnlockTank;
        }

        /// <summary>ResourcePos chi tiêu tương ứng với booster (booster_hint... booster_unlocktank).</summary>
        public static ResourcePos ToSpendPos(this ResourceType type)
        {
            return type.IsBooster() ? (ResourcePos)((int)ResourcePos.booster_hint + (type - ResourceType.AddSlot)) : ResourcePos.unknown;
        }
    }
}
