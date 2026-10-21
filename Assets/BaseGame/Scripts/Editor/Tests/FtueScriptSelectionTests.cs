using Core.Tutorial;
using NUnit.Framework;

/// Kịch bản nào chạy với save nào (spec mục 6-8; L3 không còn tut, Add Slot mở lúc vào level như mọi booster).
public class FtueScriptSelectionTests
{
    private static TutorialRunContext Ctx(int level, params string[] done) => new TutorialRunContext(level, done);

    [Test]
    public void L1_RunsForNewPlayer() => Assert.IsTrue(new TutMatchTankScript().ShouldRun(Ctx(1)));

    [Test]
    public void L1_SkipsWhenDone() => Assert.IsFalse(new TutMatchTankScript().ShouldRun(Ctx(1, TutorialIds.L1Match)));

    [Test]
    public void L1_OnlyOnLevel1() => Assert.IsFalse(new TutMatchTankScript().ShouldRun(Ctx(2)));

    [Test]
    public void L2_RunsWhenL1Done() => Assert.IsTrue(new TutHoldingScript().ShouldRun(Ctx(2, TutorialIds.L1Match)));

    [Test]
    public void L2_RunsForOldPlayerWhoSkippedNewL1() => Assert.IsTrue(new TutHoldingScript().ShouldRun(Ctx(2)));

    [Test]
    public void L2_SkipsWhenDone() => Assert.IsFalse(new TutHoldingScript().ShouldRun(Ctx(2, TutorialIds.L2Holding)));

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    public void L2_OnlyOnLevel2(int level) => Assert.IsFalse(new TutHoldingScript().ShouldRun(Ctx(level)));

    [Test]
    public void L1_Metadata()
    {
        var s = new TutMatchTankScript();
        Assert.AreEqual(TutorialIds.L1Match, s.Id);
        Assert.AreEqual(1, s.Level);
        Assert.IsTrue(s.BlocksAtStart);
    }
}
