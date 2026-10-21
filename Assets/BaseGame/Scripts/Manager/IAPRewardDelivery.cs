using System.Linq;
using Core.GamePlay;
using CoreData;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Activities;
using UnityEngine;

/// <summary>
/// Central, product-id based IAP reward delivery.
///
/// The normal in-session purchase flow delivers items through the callback
/// (IAPProduct.OnSuccess) that the shop UI passes when the user taps "Buy".
/// That callback only exists for the purchase made in the current session and
/// on a live shop UI instance. It is therefore NOT available when a transaction
/// is confirmed outside that flow, for example:
///   - Android pending payments (Pix, boleto, slow/verified cards) that settle
///     seconds/minutes later,
///   - iOS "Ask to Buy" purchases approved by a parent later,
///   - purchases interrupted by a crash / app kill and re-delivered by the
///     store on the next launch,
///   - restored / previously-pending transactions fetched at startup.
///
/// In all those cases the store charges the user but the old code delivered
/// nothing (m_IAPProduct was null, so m_IAPProduct?.OnSuccess did nothing).
///
/// This helper grants a package's rewards from ShopGlobalConfig by its
/// packageId directly, without needing the shop UI, so the user always receives
/// what they paid for. Idempotency (never grant twice) is enforced by the
/// caller via the transaction id.
/// </summary>
public static class IAPRewardDelivery
{
    public static bool GrantByProductId(string packageId)
    {
        if (string.IsNullOrEmpty(packageId))
        {
            Debug.LogError("[IAPRewardDelivery] Empty packageId, cannot deliver.");
            return false;
        }

        ShopPackageDataConfig config = ShopGlobalConfig.Instance.GetShopPackageDataConfig(packageId);
        if (config == null)
        {
            Debug.LogError($"[IAPRewardDelivery] No ShopPackageDataConfig for packageId '{packageId}'.");
            return false;
        }

        // Same "Tap to Claim" popup as the in-session shop purchase: the group is
        // saved, then credited when the player taps. Without the activity layer
        // (e.g. cold start) there is nobody to tap, so credit the resources
        // right away through PlayerResourceManager instead.
        ClaimGameResourceGroup claimGroup = BuildClaimGroup(config);
        if (ActivityContainer.TryFind(ContainerKey.Activities, out _))
        {
            RewardManager.Instance.AddResource(claimGroup);
            RewardManager.Instance.ClaimResource(claimGroup).Forget();
        }
        else
        {
            RewardManager.Instance.AddReward(claimGroup.GameResourceList, claimGroup.Placement, claimGroup.ClaimType);
        }

        ShopData.Instance.AddPurchasedPack(config.packageName.ToString());
        GameGlobalEvent.OnPurchaseIapSuccess?.Invoke();
        UserData.Instance.IncreaseIAPCount();

        Debug.Log($"[IAPRewardDelivery] Delivered package '{packageId}' ({config.packageName}).");
        return true;
    }

    // Mirrors the group UIShopPack builds for an IAP purchase.
    public static ClaimGameResourceGroup BuildClaimGroup(ShopPackageDataConfig config)
    {
        string claimType = nameof(ResourcePos.iap);
        return new ClaimGameResourceGroup
        {
            GameResourceList = config.GameResources.Where(resource => resource.Amount > 0).ToList(),
            ClaimType = claimType,
            Placement = claimType,
        };
    }
}
