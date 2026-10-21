using Core.Tutorial;
using NUnit.Framework;

/// Bảng HUD theo level (spec mục 10).
public class FtueHudRulesTests
{
    private static readonly string[] None = new string[0];
    private static readonly string[] L1Done = { TutorialIds.L1Match };
    private static readonly string[] L2Done = { TutorialIds.L1Match, TutorialIds.L2Holding };

    [Test]
    public void Progress_L1_HiddenUntilMatchStepDone()
    {
        Assert.IsFalse(FtueHudRules.IsVisible(HudPart.Progress, 1, None));
        Assert.IsTrue(FtueHudRules.IsVisible(HudPart.Progress, 1, L1Done));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(40)]
    public void Progress_AfterL1_AlwaysVisible(int level) => Assert.IsTrue(FtueHudRules.IsVisible(HudPart.Progress, level, None));

    [Test]
    public void Holding_L1_AlwaysHidden() => Assert.IsFalse(FtueHudRules.IsVisible(HudPart.Holding, 1, L2Done));

    [Test]
    public void Holding_L2_HiddenUntilHoldingStepDone()
    {
        Assert.IsFalse(FtueHudRules.IsVisible(HudPart.Holding, 2, L1Done));
        Assert.IsTrue(FtueHudRules.IsVisible(HudPart.Holding, 2, L2Done));
    }

    [TestCase(3)]
    [TestCase(4)]
    [TestCase(40)]
    public void Holding_FromL3_Visible(int level) => Assert.IsTrue(FtueHudRules.IsVisible(HudPart.Holding, level, None));

    [TestCase(1)]
    [TestCase(2)]
    public void BoosterBar_L1L2_Hidden(int level) => Assert.IsFalse(FtueHudRules.IsVisible(HudPart.BoosterBar, level, L2Done));

    // Add Slot mở lúc vào L3 như mọi booster khác: thanh booster phải hiện sẵn, kể cả khi chưa nhận.
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(40)]
    public void BoosterBar_FromL3_Visible(int level) => Assert.IsTrue(FtueHudRules.IsVisible(HudPart.BoosterBar, level, None));

    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    public void LockedTanks_FollowLevelRule(int level, bool expected) =>
        Assert.AreEqual(expected, FtueHudRules.IsVisible(HudPart.LockedTanks, level, None));

    [Test]
    public void NullDoneSteps_TreatedAsEmpty() => Assert.IsFalse(FtueHudRules.IsVisible(HudPart.Progress, 1, null));
}
