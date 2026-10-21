using CoreData;
using NUnit.Framework;

/// Level tối đa chơi được: remote quyết định khi có, nhưng không bao giờ khoá người chơi khỏi level họ đã tới (trong phạm vi bake).
public class LevelAddrMaxRuleTests
{
    [TestCase(0, 150, 10, 150, TestName = "Chua co remote thi dung so bake")]
    [TestCase(-5, 150, 10, 150, TestName = "Remote am coi nhu chua co")]
    [TestCase(200, 150, 10, 200, TestName = "Remote lon hon bake")]
    [TestCase(100, 150, 50, 100, TestName = "Remote nho hon bake, nguoi choi chua toi")]
    [TestCase(100, 150, 120, 120, TestName = "Nguoi choi da vuot remote")]
    [TestCase(100, 150, 300, 150, TestName = "Nguoi choi vuot ca bake thi chan o bake")]
    public void Resolve(int remoteMax, int bakedMax, int currentLevel, int expected)
    {
        Assert.AreEqual(expected, LevelMaxRule.Resolve(remoteMax, bakedMax, currentLevel));
    }

    [Test]
    public void DecidePlay_WithinMax_Plays()
    {
        Assert.AreEqual(LevelPlayDecision.Play, LevelMaxRule.DecidePlay(1, 150, 150, false));
        Assert.AreEqual(LevelPlayDecision.Play, LevelMaxRule.DecidePlay(150, 150, 150, true));
        Assert.AreEqual(LevelPlayDecision.Play, LevelMaxRule.DecidePlay(180, 200, 150, true));
    }

    [Test]
    public void DecidePlay_BeyondMax_RemoteKnown_ComingSoon()
    {
        Assert.AreEqual(LevelPlayDecision.ComingSoon, LevelMaxRule.DecidePlay(151, 150, 150, true));
        Assert.AreEqual(LevelPlayDecision.ComingSoon, LevelMaxRule.DecidePlay(201, 200, 150, true));
    }

    [Test]
    public void DecidePlay_BeyondBakedMax_RemoteNotReadYet_AsksToRetry()
    {
        // Người chơi có thể đang ở level chỉ có trên CDN; chưa đọc được Remote Config thì chưa kết luận là hết level.
        Assert.AreEqual(LevelPlayDecision.UnstableNetwork, LevelMaxRule.DecidePlay(151, 150, 150, false));
    }

    [Test]
    public void DecidePlay_InvalidLevel_ComingSoon()
    {
        Assert.AreEqual(LevelPlayDecision.ComingSoon, LevelMaxRule.DecidePlay(0, 150, 150, true));
        Assert.AreEqual(LevelPlayDecision.ComingSoon, LevelMaxRule.DecidePlay(-3, 150, 150, false));
    }
}
