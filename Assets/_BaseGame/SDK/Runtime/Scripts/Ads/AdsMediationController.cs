using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SDK {
    public abstract class AdsMediationController : MonoBehaviour {
        [SerializeField]
        protected bool m_IsActive;
        public bool IsActive
        {
            get => m_IsActive;
            set => m_IsActive = value;
        }

        protected AdsMediationType m_AdsMediationType;
        protected Action<ImpressionData> m_AdRevenuePaidCallback;

        public Action<ImpressionData> AdRevenuePaidCallback
        {
            get => m_AdRevenuePaidCallback;
            set => m_AdRevenuePaidCallback = value;
        }

        public bool IsInited = false;
        public virtual void Init() {
            IsInited = true;
        }

        #region Banner Ads
        protected Action m_BannerAdLoadedSuccessCallback;
        protected Action m_BannerAdLoadedFailCallback;
        protected Action m_BannerAdsDisplayedCallback;
        protected Action m_BannerAdsDisplayedFailedCallback;
        protected Action m_BannerAdsClickedCallback;
        protected Action m_BannerAdsCollapsedCallback;
        protected Action m_BannerAdsExpandedCallback;
        
        public virtual void InitBannerAds(
            Action bannerLoadedSuccessCallback, Action bannerAdLoadedFailCallback, 
            Action bannerAdsCollapsedCallback, Action bannerAdsExpandedCallback,
            Action bannerAdsDisplayed = null, Action bannerAdsDisplayedFailedCallback = null,
            Action bannerAdsClickedCallback = null) {
            m_BannerAdLoadedSuccessCallback = bannerLoadedSuccessCallback;
            m_BannerAdLoadedFailCallback = bannerAdLoadedFailCallback;
            m_BannerAdsCollapsedCallback = bannerAdsCollapsedCallback;
            m_BannerAdsExpandedCallback = bannerAdsExpandedCallback;
            m_BannerAdsDisplayedCallback = bannerAdsDisplayed;
            m_BannerAdsDisplayedFailedCallback = bannerAdsDisplayedFailedCallback;
            m_BannerAdsClickedCallback = bannerAdsClickedCallback;
        }
        public virtual void RequestBannerAds() {
        }
        public virtual void ShowBannerAds() {
        }
        public virtual void HideBannerAds() {
        }
        public virtual void CreateBannerAds() {
        }
        public virtual void DestroyBannerAds() {
        }
        public virtual bool IsBannerLoaded() {
            return false;
        }
        #endregion

        #region  Collapsible Banner

        protected Action m_CollapsibleBannerAdLoadedSuccessCallback;
        protected Action m_CollapsibleBannerAdLoadedFailCallback;
        protected Action m_CollapsibleBannerAdsCollapsedCallback;
        protected Action m_CollapsibleBannerAdsExpandedCallback;
        protected Action m_CollapsibleBannerAdsDestroyedCallback;
        protected Action m_CollapsibleBannerAdsHideCallback;
        public virtual void InitCollapsibleBannerAds(Action bannerLoadedSuccessCallback, Action bannerAdLoadedFailCallback, Action bannerAdsCollapsedCallback, Action bannerAdsExpandedCallback, Action bannerAdsDestroyedCallback, Action bannerAdsHideCallback) {
            m_CollapsibleBannerAdLoadedSuccessCallback = bannerLoadedSuccessCallback;
            m_CollapsibleBannerAdLoadedFailCallback = bannerAdLoadedFailCallback;
            m_CollapsibleBannerAdsCollapsedCallback = bannerAdsCollapsedCallback;
            m_CollapsibleBannerAdsExpandedCallback = bannerAdsExpandedCallback;
            m_CollapsibleBannerAdsDestroyedCallback = bannerAdsDestroyedCallback;
            m_CollapsibleBannerAdsHideCallback = bannerAdsHideCallback;
        }
        public virtual void RequestCollapsibleBannerAds(bool isOpenOnStart) {
        }
        public virtual void RefreshCollapsibleBannerAds() {
        }
        public virtual void ShowCollapsibleBannerAds() {
        }
        public virtual void HideCollapsibleBannerAds() {
        }
        public virtual void DestroyCollapsibleBannerAds() {
        }
        public virtual bool IsCollapsibleBannerLoaded() {
            return false;
        }

        #endregion

        #region Interstitial Ads
        protected string m_InterstitialPlacement = "interstitial";
        protected Action m_InterstitialAdCloseCallback;
        protected Action m_InterstitialAdLoadSuccessCallback;
        protected Action m_InterstitialAdLoadFailCallback;
        protected Action m_InterstitialAdShowSuccessCallback;
        protected Action m_InterstitialAdShowFailCallback;
        public virtual void InitInterstitialAd(Action adClosedCallback, Action adLoadSuccessCallback, Action adLoadFailedCallback, Action adShowSuccessCallback, Action adShowFailCallback) {
            m_InterstitialAdCloseCallback = adClosedCallback;
            m_InterstitialAdLoadSuccessCallback = adLoadSuccessCallback;
            m_InterstitialAdLoadFailCallback = adLoadFailedCallback;
            m_InterstitialAdShowSuccessCallback = adShowSuccessCallback;
            m_InterstitialAdShowFailCallback = adShowFailCallback;
        }
        public virtual void ShowInterstitialAd(Action successCallback, Action failedCallback, string m_InterstitialPlacement = "interstitial") {
            this.m_InterstitialPlacement = m_InterstitialPlacement;
            m_InterstitialAdShowSuccessCallback = successCallback;
            m_InterstitialAdShowFailCallback = failedCallback;
        }
        public virtual void RequestInterstitialAd() {
        }
        public virtual bool IsInterstitialLoaded() {
            return false;
        }

        #endregion

        #region Reward Ads
        protected string m_RewardedVideoPlacement = "rewarded_video";
        protected Action<bool> m_RewardedVideoCloseCallback;
        protected Action m_RewardedVideoLoadSuccessCallback;
        protected Action m_RewardedVideoLoadFailedCallback;
        protected Action m_RewardedVideoEarnSuccessCallback;
        protected Action m_RewardedVideoShowStartCallback;
        protected Action m_RewardedVideoShowFailCallback;
        public virtual void InitRewardVideoAd(Action<bool> videoClosed, Action videoLoadSuccess, Action videoLoadFailed, Action videoStart) {
            m_RewardedVideoCloseCallback = videoClosed;
            m_RewardedVideoLoadSuccessCallback = videoLoadSuccess;
            m_RewardedVideoLoadFailedCallback = videoLoadFailed;
            m_RewardedVideoShowStartCallback = videoStart;
        }
        public virtual void RequestRewardVideoAd() {
        }
        public virtual void ShowRewardVideoAd(Action successCallback, Action failedCallback, string m_RewardedVideoPlacement = "rewarded_video") {
            this.m_RewardedVideoPlacement = m_RewardedVideoPlacement;
            m_RewardedVideoEarnSuccessCallback = successCallback;
            m_RewardedVideoShowFailCallback = failedCallback;
        }
        public virtual bool IsRewardVideoLoaded() {
            return false;
        }

        #endregion

        #region MRec Ads
        protected Action m_MRecAdLoadedCallback;
        protected Action m_MRecAdLoadFailCallback;
        protected Action m_MRecAdClickedCallback;
        protected Action m_MRecAdExpandedCallback;
        protected Action m_MRecAdCollapsedCallback;
        public virtual void InitRMecAds(Action adLoadedCallback, Action adLoadFailedCallback, Action adClickedCallback, Action adExpandedCallback, Action adCollapsedCallback) {
            m_MRecAdLoadedCallback = adLoadedCallback;
            m_MRecAdLoadFailCallback = adLoadFailedCallback;
            m_MRecAdClickedCallback = adClickedCallback;
            m_MRecAdExpandedCallback = adExpandedCallback;
            m_MRecAdCollapsedCallback = adCollapsedCallback;
        }
        public virtual void ShowMRecAds() {
            
        }
        public virtual void HideMRecAds() {
            
        }
        public virtual bool IsMRecLoaded() {
            return false;
        }
        #endregion

        #region App Open Ads
        protected Action m_AppOpenAdLoadedCallback;
        protected Action m_AppOpenAdLoadFailedCallback;
        protected Action m_AppOpenAdClosedCallback;
        protected Action m_AppOpenAdDisplayedCallback;
        protected Action m_AppOpenAdFailedToDisplayCallback;
        public virtual void InitAppOpenAds(Action adLoadedCallback, Action adLoadFailedCallback, 
            Action adClosedCallback, Action adDisplayedCallback, Action adFailedToDisplayCallback)
        {
            m_AppOpenAdLoadedCallback = adLoadedCallback;
            m_AppOpenAdLoadFailedCallback = adLoadFailedCallback;
            m_AppOpenAdClosedCallback = adClosedCallback;
            m_AppOpenAdDisplayedCallback = adDisplayedCallback;
            m_AppOpenAdFailedToDisplayCallback = adFailedToDisplayCallback;
        }

        public virtual void ShowAppOpenAds()
        {
        }
        public virtual void RequestAppOpenAds()
        {
        }
        public virtual bool IsAppOpenAdsLoaded()
        {
            return false;
        }
        #endregion
        public virtual bool IsActiveAdsType(AdsType adsType) {
            return m_IsActive;
        }
        public abstract AdsMediationType GetAdsMediationType();
    }

    [System.Serializable]
    public class AdUnitID
    {
        #if UNITY_ANDROID
        [LabelText("ID")]
        public string AndroidID;
        #elif UNITY_IOS
        [LabelText("ID")]
        public string IOSID;
        #endif
        public string ID
        {
            get
            {
#if UNITY_ANDROID
                return AndroidID;
#elif UNITY_IOS
            return IOSID;
#else
            return "";
#endif
            }
            set
            {
#if UNITY_ANDROID
                AndroidID = value;
#elif UNITY_IOS
                IOSID = value;
#else
#endif
            }
        }
    }
}
