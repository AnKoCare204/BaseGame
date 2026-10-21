using CoreData;
using NUnit.Framework;

/// Số lần chơi lại của lượt sắp nạp: bộ đếm lưu kèm level nó thuộc về, lệch level thì đếm lại từ 0 (nhảy level bằng cheat,
/// migration loop). Save cũ chưa có level (0) thì coi là của level hiện tại.
public class LevelAddrVariantRetryTests
{
    [TestCase(-1, 25, 25, 0)]
    [TestCase(0, 25, 25, 1)]
    [TestCase(4, 25, 25, 5)]
    [TestCase(-7, 25, 25, 0)]
    public void NextRetry_SameLevel_CountsUp(int storedCount, int storedLevel, int level, int expected)
    {
        Assert.AreEqual(expected, LevelRetryRule.NextRetry(storedCount, storedLevel, level));
    }

    [TestCase(4, 24, 25)]
    [TestCase(9, 26, 25)]
    [TestCase(-1, 24, 25)]
    public void NextRetry_OtherLevel_StartsAtZero(int storedCount, int storedLevel, int level)
    {
        Assert.AreEqual(0, LevelRetryRule.NextRetry(storedCount, storedLevel, level));
    }

    [Test]
    public void NextRetry_OldSaveWithoutLevel_KeepsCounting()
    {
        Assert.AreEqual(4, LevelRetryRule.NextRetry(3, 0, 25));
        Assert.AreEqual(0, LevelRetryRule.NextRetry(-1, 0, 25));
    }
}
