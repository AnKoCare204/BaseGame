using CoreData;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class NoAdsPopupManager
{
    private static NoAdsPopupRemoteConfigData Config => NoAdsPopupRemoteConfigData.Instance;

    private static bool interstitialWatchedThisSession;

    public static void MarkInterstitialWatched()
    {
        Debug.Log("Interstitial watched");
        interstitialWatchedThisSession = true;
    }

    public static async UniTask TryShowOnHome()
    {
        Debug.Log(Config.ShowPerDay + " " + Config.GapSeconds);
        bool watched = interstitialWatchedThisSession;
        interstitialWatchedThisSession = false;
        if (!watched) return;
        if (ShopData.Instance.IsRemoveADs.Value) return;
        if (!Config.IsEnabled) return;
        if (!NoAdsPopupData.Instance.CanShow(Config.ShowPerDay, Config.GapSeconds)) return;
        NoAdsPopupData.Instance.RegisterShown();
        await UIManager.Instance.ShowModalNoAdsCompleted();
    }
}
