using NUnit.Framework;

/// Parse JSON của key Remote Config ingame_banner_config và quy tắc hiện banner trong màn chơi.
/// Chỉ gọi FromJson / CanShow (hàm thuần), không đụng Firebase.
public class BannerAdsRemoteConfigTests
{
    [Test]
    public void Banner_ParsesIsActive()
    {
        BannerRemoteConfigData data = new BannerRemoteConfigData();
        data.FromJson("{\"IsActive\":1}");
        Assert.AreEqual(1, data.IsActive);
    }

    // Mặc định là TẮT: thiếu key trên Firebase (chuỗi rỗng), JSON lỗi hay sai tên trường đều không được hiện banner.
    [TestCase("")]
    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("not json")]
    public void Banner_BadInput_DefaultsToOff(string json)
    {
        BannerRemoteConfigData data = new BannerRemoteConfigData { IsActive = 1 };
        Assert.DoesNotThrow(() => data.FromJson(json));
        Assert.AreEqual(0, data.IsActive);
    }

    [TestCase(1, false, true)]
    [TestCase(1, true, false)]
    [TestCase(0, false, false)]
    public void Banner_CanShow(int isActive, bool isRemoveAds, bool expected)
    {
        BannerRemoteConfigData data = new BannerRemoteConfigData { IsActive = isActive };
        Assert.AreEqual(expected, data.CanShow(isRemoveAds));
    }
}
