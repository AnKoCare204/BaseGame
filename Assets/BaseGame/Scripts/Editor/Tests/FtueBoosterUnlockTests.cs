using CoreData;
using NUnit.Framework;

/// Add Slot mở khóa giữa L3 thay vì đầu level (spec mục 12); booster thường giữ luật cũ.
public class FtueBoosterUnlockTests
{
    private static BoosterInfoData AddSlot => new BoosterInfoData { ResourceType = ResourceType.AddSlot, LevelUnlock = 3, UnlockMidLevel = true };
    private static BoosterInfoData Shuffle => new BoosterInfoData { ResourceType = ResourceType.Shuffle, LevelUnlock = 9 };
    private static BoosterInfoData UnlockTank => new BoosterInfoData { ResourceType = ResourceType.UnlockTank, LevelUnlock = -1 };

    [TestCase(2, false, false)]
    [TestCase(3, false, false)]   
    [TestCase(4, false, true)]    
    [TestCase(9, false, true)]
    [TestCase(4, true, false)]
    public void MidLevel_FlowAtStartOnlyAfterItsLevel(int level, bool claimed, bool expected) =>
        Assert.AreEqual(expected, BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(AddSlot, level, claimed));

    [TestCase(8, false, false)]
    [TestCase(9, false, true)]
    [TestCase(9, true, false)]
    [TestCase(10, false, false)]
    public void Normal_FlowAtStartExactlyAtItsLevel(int level, bool claimed, bool expected) =>
        Assert.AreEqual(expected, BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(Shuffle, level, claimed));

    [TestCase(1)]
    [TestCase(50)]
    public void NegativeUnlockLevel_NeverNeedsFlow(int level) =>
        Assert.IsFalse(BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(UnlockTank, level, false));

    [TestCase(2, false, false)]
    [TestCase(3, false, false)]
    [TestCase(3, true, true)]
    [TestCase(10, false, false)]
    [TestCase(10, true, true)]
    public void MidLevel_SelectorUnlockedOnlyWhenClaimed(int level, bool claimed, bool expected) =>
        Assert.AreEqual(expected, BoosterUnlockRules.IsSelectorUnlocked(AddSlot, level, claimed));

    [TestCase(8, false, false)]
    [TestCase(9, false, true)]
    [TestCase(9, true, true)]
    public void Normal_SelectorUnlockedFromItsLevel(int level, bool claimed, bool expected) =>
        Assert.AreEqual(expected, BoosterUnlockRules.IsSelectorUnlocked(Shuffle, level, claimed));

    [Test]
    public void NullData_False()
    {
        Assert.IsFalse(BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(null, 3, false));
        Assert.IsFalse(BoosterUnlockRules.IsSelectorUnlocked(null, 3, true));
    }
}
