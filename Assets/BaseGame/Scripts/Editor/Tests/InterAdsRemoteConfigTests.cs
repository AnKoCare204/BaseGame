using NUnit.Framework;

/// Parse JSON của hai key Remote Config cho inter (inter_level_config, inter_lose_config) và điều kiện hiện inter khi thua.
/// Chỉ gọi FromJson / CanShow (hàm thuần), không đụng Firebase.
public class InterAdsRemoteConfigTests
{
    [Test]
    public void InterLevel_ParsesLevel()
    {
        InterLevelRemoteConfigData data = new InterLevelRemoteConfigData();
        data.FromJson("{\"LevelShowInter\":20}");
        Assert.AreEqual(20, data.LevelShowInter);
    }

    // Thiếu key trên Firebase (chuỗi rỗng), JSON lỗi hay sai tên trường đều phải về 15.
    [TestCase("")]
    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("not json")]
    public void InterLevel_BadInput_UsesDefault(string json)
    {
        InterLevelRemoteConfigData data = new InterLevelRemoteConfigData { LevelShowInter = 99 };
        Assert.DoesNotThrow(() => data.FromJson(json));
        Assert.AreEqual(15, data.LevelShowInter);
    }

    [Test]
    public void InterLose_ParsesAllFields()
    {
        InterLoseRemoteConfigData data = new InterLoseRemoteConfigData();
        data.FromJson("{\"IsActive\":0,\"Level\":30}");
        Assert.AreEqual(0, data.IsActive);
        Assert.AreEqual(30, data.Level);
    }

    [Test]
    public void InterLose_MissingField_KeepsItsDefault()
    {
        InterLoseRemoteConfigData data = new InterLoseRemoteConfigData { IsActive = 0 };
        data.FromJson("{\"Level\":30}");
        Assert.AreEqual(1, data.IsActive);
        Assert.AreEqual(30, data.Level);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("not json")]
    public void InterLose_BadInput_UsesDefaults(string json)
    {
        InterLoseRemoteConfigData data = new InterLoseRemoteConfigData { IsActive = 0, Level = 99 };
        Assert.DoesNotThrow(() => data.FromJson(json));
        Assert.AreEqual(1, data.IsActive);
        Assert.AreEqual(50, data.Level);
    }

    [TestCase(1, 50, 49, false)]
    [TestCase(1, 50, 50, true)]
    [TestCase(1, 50, 51, true)]
    [TestCase(0, 50, 100, false)]
    public void InterLose_CanShow(int isActive, int level, int currentLevel, bool expected)
    {
        InterLoseRemoteConfigData data = new InterLoseRemoteConfigData { IsActive = isActive, Level = level };
        Assert.AreEqual(expected, data.CanShow(currentLevel));
    }
}
