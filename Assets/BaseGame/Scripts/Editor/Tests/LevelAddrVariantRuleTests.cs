using System.Collections.Generic;
using CoreData;
using NUnit.Framework;

/// Từ lần chơi lại thứ x của level n chơi biến thể x; nhiều biến thể thì bậc thang (x lớn nhất ≤ r). Không áp cho level loop,
/// level 1-2 (FTUE) và lượt đầu.
public class LevelAddrVariantRuleTests
{
    [TestCase(25, 25, 1, true)]
    [TestCase(25, 25, 40, true)]
    [TestCase(3, 3, 1, true)]
    [TestCase(25, 25, 0, false)]
    [TestCase(25, 25, -1, false)]
    [TestCase(160, 73, 5, false)]
    [TestCase(2, 2, 5, false)]
    [TestCase(1, 1, 5, false)]
    [TestCase(5, 0, 3, false)]
    public void IsEligible(int level, int sourceLevel, int retryCount, bool expected)
    {
        Assert.AreEqual(expected, LevelVariantRule.IsEligible(new LevelRequest(level, sourceLevel, retryCount)));
    }

    [Test]
    public void Request_DefaultRetryCount_IsZero()
    {
        Assert.AreEqual(0, new LevelRequest(12, 12).RetryCount);
        Assert.AreEqual(4, new LevelRequest(12, 12, 4).RetryCount);
    }

    [Test]
    public void Candidates_FollowStairs()
    {
        List<int> variants = new List<int> { 3, 5 };
        CollectionAssert.IsEmpty(LevelVariantRule.Candidates(2, variants));
        CollectionAssert.AreEqual(new[] { 3 }, LevelVariantRule.Candidates(3, variants));
        CollectionAssert.AreEqual(new[] { 3 }, LevelVariantRule.Candidates(4, variants));
        CollectionAssert.AreEqual(new[] { 5, 3 }, LevelVariantRule.Candidates(5, variants));
        CollectionAssert.AreEqual(new[] { 5, 3 }, LevelVariantRule.Candidates(40, variants));
    }

    [Test]
    public void Candidates_NoVariants_IsEmpty()
    {
        CollectionAssert.IsEmpty(LevelVariantRule.Candidates(5, null));
        CollectionAssert.IsEmpty(LevelVariantRule.Candidates(5, new List<int>()));
    }

    [Test]
    public void Candidates_IgnoresDuplicatesAndNonPositive()
    {
        CollectionAssert.AreEqual(new[] { 2 }, LevelVariantRule.Candidates(5, new List<int> { 0, -1, 2, 2 }));
    }
}
