/// <summary>
/// Luật mở khóa booster theo level (spec FTUE đợt 1 mục 12). Booster thường: luồng nhận chạy đúng ở level LevelUnlock, nút mở từ
/// level đó. Booster UnlockMidLevel (hiện không booster nào dùng): kịch bản mở giữa level LevelUnlock; level sau mà chưa nhận thì luồng nhận
/// chạy lúc vào level như cũ; nút chỉ mở khi đã nhận. Hàm thuần, test ở FtueBoosterUnlockTests.
/// </summary>
public static class BoosterUnlockRules
{
    public static bool NeedsUnlockFlowAtLevelStart(BoosterInfoData data, int level, bool claimed)
    {
        if (data == null || claimed || data.LevelUnlock < 1) return false;
        return data.UnlockMidLevel ? level > data.LevelUnlock : level == data.LevelUnlock;
    }

    public static bool IsSelectorUnlocked(BoosterInfoData data, int level, bool claimed)
    {
        if (data == null || level < data.LevelUnlock) return false;
        return !data.UnlockMidLevel || claimed;
    }
}
