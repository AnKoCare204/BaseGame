using NUnit.Framework;

/// Parse key Remote Config idle_hint_config và thời gian chờ theo level (spec mục 13). Chỉ gọi FromJson / GetDelay, không đụng Firebase.
public class FtueIdleHintConfigTests
{
    [Test]
    public void ParsesAllFields()
    {
        var data = new IdleHintRemoteConfigData();
        data.FromJson("{\"IsActive\":1,\"MaxLevel\":20,\"EarlyMaxLevel\":5,\"EarlyDelay\":4,\"LateDelay\":10}");
        Assert.AreEqual(20, data.MaxLevel);
        Assert.AreEqual(5, data.EarlyMaxLevel);
        Assert.AreEqual(4f, data.EarlyDelay);
        Assert.AreEqual(10f, data.LateDelay);
    }

    [Test]
    public void MissingField_KeepsItsDefault()
    {
        var data = new IdleHintRemoteConfigData();
        data.FromJson("{\"LateDelay\":12}");
        Assert.AreEqual(1, data.IsActive);
        Assert.AreEqual(15, data.MaxLevel);
        Assert.AreEqual(5f, data.EarlyDelay);
        Assert.AreEqual(12f, data.LateDelay);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("not json")]
    public void BadInput_UsesDefaults(string json)
    {
        var data = new IdleHintRemoteConfigData { IsActive = 0, MaxLevel = 99 };
        Assert.DoesNotThrow(() => data.FromJson(json));
        Assert.AreEqual(1, data.IsActive);
        Assert.AreEqual(15, data.MaxLevel);
        Assert.AreEqual(3, data.EarlyMaxLevel);
        Assert.AreEqual(5f, data.EarlyDelay);
        Assert.AreEqual(8f, data.LateDelay);
    }

    [TestCase(0, -1f)]
    [TestCase(1, 5f)]
    [TestCase(3, 5f)]
    [TestCase(4, 8f)]
    [TestCase(15, 8f)]
    [TestCase(16, -1f)]
    public void GetDelay_Defaults(int level, float expected) => Assert.AreEqual(expected, new IdleHintRemoteConfigData().GetDelay(level));

    [Test]
    public void GetDelay_Inactive_IsMinusOne()
    {
        var data = new IdleHintRemoteConfigData { IsActive = 0 };
        Assert.AreEqual(-1f, data.GetDelay(1));
    }

    [Test]
    public void GetDelay_NonPositiveDelay_IsMinusOne()
    {
        var data = new IdleHintRemoteConfigData { EarlyDelay = 0f, LateDelay = -2f };
        Assert.AreEqual(-1f, data.GetDelay(1));
        Assert.AreEqual(-1f, data.GetDelay(10));
    }
}
