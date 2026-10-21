using System;

namespace CoreData
{
    /// <summary>
    /// Số lần chơi lại của lượt sắp nạp (lượt đầu của level = 0). UserData lưu bộ đếm kèm level nó thuộc về: lệch level (nhảy level
    /// bằng cheat, ApplyLevelLoopMigration...) thì đếm lại từ 0, để biến thể level không bật nhầm (LevelVariantRule). Save của bản app
    /// cũ chưa có level (0) thì coi là của level hiện tại. Hàm thuần, test ở LevelAddrVariantRetryTests.
    /// </summary>
    public static class LevelRetryRule
    {
        public static int NextRetry(int storedCount, int storedLevel, int level)
        {
            if (storedLevel != 0 && storedLevel != level) return 0;
            return Math.Max(-1, storedCount) + 1;
        }
    }
}
