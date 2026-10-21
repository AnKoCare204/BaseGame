using NUnit.Framework;

/// Parse JSON của hai key Remote Config cho level. Chỉ gọi FromJson (hàm thuần), không đụng Firebase.
public class LevelAddrRemoteConfigTests
{
    [Test]
    public void LevelMax_ParsesBothVariants()
    {
        LevelRemoteConfigData data = new LevelRemoteConfigData();
        data.FromJson("{\"LevelMaxCheat\":180,\"LevelMaxRelease\":160}");
        Assert.AreEqual(180, data.LevelMaxCheat);
        Assert.AreEqual(160, data.LevelMaxRelease);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("{}")]
    public void LevelMax_EmptyInput_KeepsZero(string json)
    {
        LevelRemoteConfigData data = new LevelRemoteConfigData { LevelMaxCheat = 99, LevelMaxRelease = 99 };
        data.FromJson(json);
        Assert.AreEqual(0, data.LevelMaxCheat);
        Assert.AreEqual(0, data.LevelMaxRelease);
    }

    [Test]
    public void LevelMax_BrokenJson_ResetsInsteadOfThrowing()
    {
        LevelRemoteConfigData data = new LevelRemoteConfigData { LevelMaxCheat = 99 };
        Assert.DoesNotThrow(() => data.FromJson("{\"LevelMaxCheat\":"));
        Assert.AreEqual(0, data.LevelMaxCheat);
    }

    [Test]
    public void TestLevel_ParsesAllFields()
    {
        TestLevelRemoteConfigData data = new TestLevelRemoteConfigData();
        data.FromJson("{\"IsActiveCheat\":1,\"IsActiveRelease\":0,\"TestVersionCheat\":\"VersionA\",\"TestVersionRelease\":\"VersionB\"}");
        Assert.AreEqual(1, data.IsActiveCheat);
        Assert.AreEqual(0, data.IsActiveRelease);
        Assert.AreEqual("VersionA", data.TestVersionCheat);
        Assert.AreEqual("VersionB", data.TestVersionRelease);
    }

    [Test]
    public void TestLevel_MissingFields_DefaultToOff()
    {
        TestLevelRemoteConfigData data = new TestLevelRemoteConfigData();
        data.FromJson("{\"IsActiveCheat\":1}");
        Assert.AreEqual(1, data.IsActiveCheat);
        Assert.AreEqual("", data.TestVersionCheat);
        Assert.AreEqual("", data.TestVersionRelease);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("not json")]
    public void TestLevel_BadInput_ResetsToOff(string json)
    {
        TestLevelRemoteConfigData data = new TestLevelRemoteConfigData
        {
            IsActiveCheat = 1, IsActiveRelease = 1, TestVersionCheat = "VersionA", TestVersionRelease = "VersionA"
        };
        Assert.DoesNotThrow(() => data.FromJson(json));
        Assert.AreEqual(0, data.IsActiveCheat);
        Assert.AreEqual(0, data.IsActiveRelease);
        Assert.AreEqual("", data.TestVersionCheat);
        Assert.AreEqual("", data.TestVersionRelease);
    }

    [Test]
    public void TestLevel_ActiveWithoutVersion_IsNotEnabled()
    {
        TestLevelRemoteConfigData data = new TestLevelRemoteConfigData();
        data.FromJson("{\"IsActiveCheat\":1,\"IsActiveRelease\":1,\"TestVersionCheat\":\"\",\"TestVersionRelease\":\"  \"}");
        Assert.IsFalse(data.IsEnabled);
        Assert.AreEqual("", data.TestVersion);
    }
}
