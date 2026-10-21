using System.Linq;
using CoreData;
using NUnit.Framework;

/// Giao gói IAP ngoài luồng shop (pending, Ask to Buy, app bị kill rồi mở lại) phải dùng cùng popup "Tap to Claim"
/// với luồng shop: nhóm thưởng dựng giống UIShopPack (bỏ món số lượng 0, claim type / placement "iap").
public class IapFallbackRewardTests
{
    private const string CoinPackId = "com.abi.fishy.jam.coin2";
    private const string BundleId = "com.abi.fishy.jam.ultra_bundle";

    [Test]
    public void BuildClaimGroup_CoinPack_HasPackCoins()
    {
        ShopPackageDataConfig config = ShopGlobalConfig.Instance.GetShopPackageDataConfig(CoinPackId);

        ClaimGameResourceGroup group = IAPRewardDelivery.BuildClaimGroup(config);

        int expected = config.GameResources.Where(r => r.Type == ResourceType.Money).Sum(r => r.Amount.Value);
        Assert.Greater(expected, 0);
        Assert.AreEqual(expected, group.GameResourceList.Where(r => r.Type == ResourceType.Money).Sum(r => r.Amount.Value));
    }

    [Test]
    public void BuildClaimGroup_Bundle_KeepsOnlyPositiveAmounts()
    {
        ShopPackageDataConfig config = ShopGlobalConfig.Instance.GetShopPackageDataConfig(BundleId);

        ClaimGameResourceGroup group = IAPRewardDelivery.BuildClaimGroup(config);

        Assert.AreEqual(config.GameResources.Count(r => r.Amount.Value > 0), group.GameResourceList.Count);
        Assert.IsTrue(group.GameResourceList.All(r => r.Amount.Value > 0));
    }

    [Test]
    public void BuildClaimGroup_UsesIapClaimTypeAndDefaultPopup()
    {
        ShopPackageDataConfig config = ShopGlobalConfig.Instance.GetShopPackageDataConfig(CoinPackId);

        ClaimGameResourceGroup group = IAPRewardDelivery.BuildClaimGroup(config);

        Assert.AreEqual(nameof(ResourcePos.iap), group.ClaimType);
        Assert.AreEqual(nameof(ResourcePos.iap), group.Placement);
        Assert.AreEqual(ClaimGameResourceType.Default, group.ClaimGameResourceType);
    }
}
