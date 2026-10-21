using Core.Tutorial;
using NUnit.Framework;

/// Luật riêng theo level của FTUE đợt 1 (spec mục 11).
public class FtueLevelRulesTests
{
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(50, true)]
    [TestCase(int.MaxValue, true)]
    public void HoldingEnabled(int level, bool expected) => Assert.AreEqual(expected, FtueLevelRules.HoldingEnabled(level));

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(int.MaxValue, false)]
    public void FreeRescue(int level, bool expected) => Assert.AreEqual(expected, FtueLevelRules.FreeRescue(level));

    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(int.MaxValue, true)]
    public void ShowLockedTanks(int level, bool expected) => Assert.AreEqual(expected, FtueLevelRules.ShowLockedTanks(level));

    [TestCase(1, 3)]
    [TestCase(2, 3)]
    [TestCase(3, 3)]
    [TestCase(4, 2)]
    [TestCase(20, 2)]
    [TestCase(0, 2)]
    [TestCase(-1, 2)]
    public void OpenOrderSlots_ThreeOnTutorialLevels(int level, int expected) => Assert.AreEqual(expected, FtueLevelRules.OpenOrderSlots(level, 2));
}
