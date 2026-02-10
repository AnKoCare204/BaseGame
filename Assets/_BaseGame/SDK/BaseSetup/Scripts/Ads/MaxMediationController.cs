using System;
using System.Collections;
using System.Collections.Generic;
using SDK.Analytics;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace SDK {
    public class MaxMediationController : AdsMediationController
    {
#if UNITY_AD_MAX
        private bool m_IsWatchSuccess = false;
        public MaxAdSetup m_MaxAdConfig;
        [FoldoutGroup("Rewarded")] [ShowInInspector] private Dictionary<string, bool> m_RewardedReadyMap = new Dictionary<string, bool>();
        [FoldoutGroup("Rewarded")] [ShowInInspector] private List<string> m_RewardedOrder = new List<string>();
        [FoldoutGroup("Rewarded")] [ShowInInspector] private int m_CurrentLoadingIndexReward = 0;
        [FoldoutGroup("Rewarded")] [ShowInInspector] private bool isWaitLoadRewardedUntilAvailable = false;
        [FoldoutGroup("Rewarded")] [ShowInInspector] private double revenueRewarded = 0;

        [FoldoutGroup("Interstitial")] [ShowInInspector] private Dictionary<string, bool> m_InterstitialReadyMap = new Dictionary<string, bool>();
        [FoldoutGroup("Interstitial")] [ShowInInspector] private List<string> m_InterstitialOrder = new List<string>();
        [FoldoutGroup("Interstitial")] [ShowInInspector] private int m_CurrentLoadingIndexInterstitial = 0;
        [FoldoutGroup("Interstitial")] [ShowInInspector] private bool isWaitLoadInterstitialUntilAvailable = false;
        [FoldoutGroup("Interstitial")] [ShowInInspector] private double revenueInterstitial = 0;

        public override void Init()
        {
            if (IsInited) return;
            base.Init();
            SDKDebugLogger.Log("unity-script: MyAppStart Start called");
            MaxSdk.SetExtraParameter("disable_all_logs", "true");

            // Disable MAX SDK retry mechanism
            MaxSdk.SetExtraParameter("disable_auto_retries", "true");

            // Disable back-to-back ad loading
            MaxSdk.SetExtraParameter("disable_back_to_back_loading", "true");

            // Initialize InMobi Publisher Signals Manager
            // ABIInMobiPublisherSignalsManager.Initialize();
            // ABIInMobiPublisherSignalsManager.SendAllPublisherSignals();

            MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfiguration =>
            {
                // AppLovin SDK is initialized, configure and start loading ads.
                SDKDebugLogger.Log("MAX SDK Initialized");
                AdsManager.Instance.InitAdsType(AdsMediationType.MAX);
                //MaxSdk.ShowMediationDebugger();
            };
            MaxSdk.SetHasUserConsent(true);
            MaxSdk.SetDoNotSell(false);
            MaxSdk.InitializeSdk();
            MaxSdk.SetVerboseLogging(false);
        }

        private void OnAdRevenuePaidEvent(AdsType adsType, string adUnitId, MaxSdkBase.AdInfo impressionData)
        {
            double revenue = impressionData.Revenue;
            ImpressionData impression = new ImpressionData
            {
                ad_mediation = AdsMediationType.MAX,
                ad_source = impressionData.NetworkName,
                ad_unit_name = impressionData.AdUnitIdentifier,
                ad_format = impressionData.AdFormat,
                ad_revenue = revenue,
                ad_currency = "USD",
                ad_type = impressionData.AdFormat
            };
            AdRevenuePaidCallback?.Invoke(impression);
        
            // AnalyticsManager.LogAdsRevenue(impression, adsType);
            
            // Track ad impression for InMobi signals
            // ABIInMobiPublisherSignalsManager.TrackAdImpression(adsType, revenue);
        }
        #region Interstitial
        public override void InitInterstitialAd(Action adClosedCallback, Action adLoadSuccessCallback, Action adLoadFailedCallback, Action adShowSuccessCallback, Action adShowFailCallback)
        {
            base.InitInterstitialAd(adClosedCallback, adLoadSuccessCallback, adLoadFailedCallback, adShowSuccessCallback, adShowFailCallback);
            SDKDebugLogger.Log("Init MAX Interstitial");
            // Attach callbacks
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += InterstitialFailedToDisplayEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialDismissedEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (adUnitID, adInfo) => { OnAdRevenuePaidEvent(AdsType.INTERSTITIAL, adUnitID, adInfo); };
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialAdShowSucceededEvent;

            PreloadAllInterstitialAds();
        }


        private void BuildInterstitialOrder()
        {
            m_InterstitialOrder.Clear();
            m_InterstitialReadyMap.Clear();
            if (m_MaxAdConfig.interstitialAdsUnit != null && m_MaxAdConfig.interstitialAdsUnit.Count > 0)
            {
                for (int i = 0; i < m_MaxAdConfig.interstitialAdsUnit.Count && i < 5; i++)
                {
                    string id = m_MaxAdConfig.interstitialAdsUnit[i];
                    if (!string.IsNullOrEmpty(id))
                    {
                        m_InterstitialOrder.Add(id);
                        m_InterstitialReadyMap.Add(id, false);
                    }
                }
            }

            if (m_InterstitialOrder.Count == 0 && !string.IsNullOrEmpty(m_MaxAdConfig.InterstitialAdUnitID))
            {
                m_InterstitialOrder.Add(m_MaxAdConfig.InterstitialAdUnitID);
                m_InterstitialReadyMap.Add(m_MaxAdConfig.InterstitialAdUnitID, false);
            }
        }

        private void PreloadAllInterstitialAds()
        {
            BuildInterstitialOrder();
            
            LoadInterstitialFirst();
        }
        
        private void LoadInterstitialFirst()
        {
            if (isWaitLoadInterstitialUntilAvailable) return;
            isWaitLoadInterstitialUntilAvailable = true;

            SDKDebugLogger.Log($"LoadInterstitialFirst");

            m_CurrentLoadingIndexInterstitial = 0;

            string adUnitId = m_InterstitialOrder[m_CurrentLoadingIndexInterstitial];
            
            if (!m_InterstitialReadyMap[adUnitId])
            {
                
                SDKDebugLogger.Log($"Start loading interstitial ad at index {0}: {adUnitId}");
                MaxSdk.LoadInterstitial(adUnitId);
            }
            else if (m_InterstitialReadyMap[adUnitId])
            {
                SDKDebugLogger.Log($"Interstitial ad at index {0} already ready: {adUnitId}");
            }
        }

        private void LoadInterstitialAtIndex(int index)
        {
            SDKDebugLogger.Log($"LoadInterstitialAtIndex: {index}");
            string adUnitId = m_InterstitialOrder[index];
            bool isReady = MaxSdk.IsInterstitialReady(adUnitId);
            m_InterstitialReadyMap[adUnitId] = isReady;
            
            if (!m_InterstitialReadyMap[adUnitId])
            {
                m_CurrentLoadingIndexInterstitial = index;
                SDKDebugLogger.Log($"Start loading interstitial ad at index {index}: {adUnitId}");
                MaxSdk.LoadInterstitial(adUnitId);
            }
            else if (m_InterstitialReadyMap[adUnitId])
            {
                isWaitLoadInterstitialUntilAvailable = false;
                SDKDebugLogger.Log($"Interstitial ad at index {index} already ready: {adUnitId}");
            }
        }

        private void ShowInterstitialReady()
        {
            SDKDebugLogger.Log("ShowInterstitialReady");
            for (int i = 0; i < m_InterstitialOrder.Count; i++)
            {
                string adUnitId = m_InterstitialOrder[i];
                if (m_InterstitialReadyMap[adUnitId])
                {
                    SDKDebugLogger.Log("Show MAX Interstitial with AdUnit: " + adUnitId);
                    m_InterstitialReadyMap[adUnitId] = false;
                    MaxSdk.ShowInterstitial(adUnitId, m_InterstitialPlacement);
                    return;
                }
            }

            Debug.Log("No interstitial ad ready to show");
            m_InterstitialAdShowFailCallback?.Invoke();
            Debug.Log("Reload Interstitial after show fail");
            LoadInterstitialFirst();
        }


        public override void RequestInterstitialAd() // not used
        {
            base.RequestInterstitialAd();
            SDKDebugLogger.Log("Request MAX Interstitial");
            if (m_InterstitialOrder == null || m_InterstitialOrder.Count == 0)
            {
                BuildInterstitialOrder();
            }

            LoadInterstitialFirst();
        }
        public override void ShowInterstitialAd(Action successCallback, Action failedCallback, string m_InterstitialPlacement = "interstitial")
        {
            base.ShowInterstitialAd(successCallback, failedCallback, m_InterstitialPlacement);
            SDKDebugLogger.Log("Show MAX Interstitial");
            ShowInterstitialReady();
        }
        public override bool IsInterstitialLoaded()
        {
            if (m_InterstitialOrder == null || m_InterstitialOrder.Count == 0)
            {
                BuildInterstitialOrder();
            }
            foreach (string adUnitId in m_InterstitialOrder)
            {
                if (MaxSdk.IsInterstitialReady(adUnitId))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("Load MAX Interstitial Success: " + adUnitId);
            m_InterstitialReadyMap[adUnitId] = true;
            revenueInterstitial = adInfo.Revenue;
            SDKDebugLogger.Log("Revenue Interstitial: " + revenueInterstitial);
            
            EventManager.AddEventNextFrame(m_InterstitialAdLoadSuccessCallback);

            isWaitLoadInterstitialUntilAvailable = false;
        }

        private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            SDKDebugLogger.Log("Load MAX Interstitial Fai: " + adUnitId);
            m_InterstitialReadyMap[adUnitId] = false;
            EventManager.AddEventNextFrame(m_InterstitialAdLoadFailCallback);
            
            int nextIndex = m_CurrentLoadingIndexInterstitial + 1;
            if (nextIndex < m_InterstitialOrder.Count)
            {
                SDKDebugLogger.Log($"Loading next interstitial ad at index {nextIndex}");
                LoadInterstitialAtIndex(nextIndex);
            }
            else
            {
                isWaitLoadInterstitialUntilAvailable = false;
                LoadInterstitialFirst();
                SDKDebugLogger.Log("All interstitial ads failed to load");
            }
        }

        private void InterstitialFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) {
            SDKDebugLogger.Log("unity-script: I got InterstitialAdShowFailedEvent, code :  " + errorInfo.Code + ", description : " + errorInfo.Message);
            EventManager.AddEventNextFrame(m_InterstitialAdShowFailCallback);
        }

        private void OnInterstitialDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("Interstitial dismissed");
            EventManager.AddEventNextFrame(m_InterstitialAdCloseCallback);
            LoadInterstitialFirst();
        }

        private void OnInterstitialAdShowSucceededEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("unity-script: I got InterstitialAdShowSuccess");

            // Send InMobi display signals (dir_r, dir_nn, dir_type)
            // ABIInMobiPublisherSignalsManager.OnAdDisplayed(adUnitId, adInfo);
            
            // Send updated publisher signals
            // ABIInMobiPublisherSignalsManager.SendAllPublisherSignals();

            EventManager.AddEventNextFrame(m_InterstitialAdShowSuccessCallback);
        }
        #endregion

        #region Rewards Video
        
        public override void InitRewardVideoAd(Action<bool> videoClosed, Action videoLoadSuccess, Action videoLoadFailed, Action videoStart) {
            base.InitRewardVideoAd(videoClosed, videoLoadSuccess, videoLoadFailed, videoStart);

            SDKDebugLogger.Log("Init MAX RewardedVideoAd");
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += Rewarded_OnAdStartedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += Rewarded_OnAdShowFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += Rewarded_OnAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += Rewarded_OnAdRewardedEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += Rewarded_OnAdClosedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += Rewarded_OnAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += Rewarded_OnAdLoadedFailEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (adUnitID, adInfo) => { OnAdRevenuePaidEvent(AdsType.REWARDED, adUnitID, adInfo);};
            PreloadAllRewardedAds();
        }
        
        private void BuildRewardedOrder()
        {
            m_RewardedOrder.Clear();
            m_RewardedReadyMap.Clear();
            if (m_MaxAdConfig.rewardAdsUnit != null && m_MaxAdConfig.rewardAdsUnit.Count > 0)
            {
                for (int i = 0; i < m_MaxAdConfig.rewardAdsUnit.Count && i < 5; i++)
                {
                    string id = m_MaxAdConfig.rewardAdsUnit[i];
                    if (!string.IsNullOrEmpty(id))
                    {
                        m_RewardedOrder.Add(id);
                        m_RewardedReadyMap.Add(id, false);
                        MaxSdk.SetRewardedAdExtraParameter(id, "disable_auto_retries", "true");
                    }
                }
            }

            if (m_RewardedOrder.Count == 0 && !string.IsNullOrEmpty(m_MaxAdConfig.RewardedAdUnitID))
            {
                m_RewardedOrder.Add(m_MaxAdConfig.RewardedAdUnitID);
                m_RewardedReadyMap.Add(m_MaxAdConfig.RewardedAdUnitID, false);
            }
        }

        private void PreloadAllRewardedAds()
        {
            BuildRewardedOrder();
            
            LoadRewardedFirst();
        }
        
        private void LoadRewardedFirst()
        {
            if (isWaitLoadRewardedUntilAvailable) return;
            isWaitLoadRewardedUntilAvailable = true;

            SDKDebugLogger.Log($"LoadRewardedFirst");

            m_CurrentLoadingIndexReward = 0;

            string adUnitId = m_RewardedOrder[m_CurrentLoadingIndexReward];
            
            if (!m_RewardedReadyMap[adUnitId])
            {
                
                SDKDebugLogger.Log($"Start loading rewarded ad at index {0}: {adUnitId}");
                MaxSdk.LoadRewardedAd(adUnitId);
            }
            else if (m_RewardedReadyMap[adUnitId])
            {
                SDKDebugLogger.Log($"Rewarded ad at index {0} already ready: {adUnitId}");
            }
        }

        private void LoadRewardedAtIndex(int index)
        {
            SDKDebugLogger.Log($"LoadRewardedAtIndex: {index}");
            string adUnitId = m_RewardedOrder[index];
            bool isReady = MaxSdk.IsRewardedAdReady(adUnitId);
            m_RewardedReadyMap[adUnitId] = isReady;
            
            if (!m_RewardedReadyMap[adUnitId])
            {
                m_CurrentLoadingIndexReward = index;
                SDKDebugLogger.Log($"Start loading rewarded ad at index {index}: {adUnitId}");
                MaxSdk.LoadRewardedAd(adUnitId);
            }
            else if (m_RewardedReadyMap[adUnitId])
            {
                isWaitLoadRewardedUntilAvailable = false;
                SDKDebugLogger.Log($"Rewarded ad at index {index} already ready: {adUnitId}");
            }
        }


        public override void RequestRewardVideoAd() // not used
        {
            base.RequestRewardVideoAd();
            SDKDebugLogger.Log("Request MAX RewardedVideoAd (sequential loading)");
            if (m_RewardedOrder == null || m_RewardedOrder.Count == 0)
            {
                BuildRewardedOrder();
            }

            LoadRewardedFirst();
        }
        
        public override void ShowRewardVideoAd(Action successCallback, Action failedCallback, string m_RewardedVideoPlacement = "rewarded_video")
        {
            if (RewardReadyToShow() && IsInterstitialLoaded() && revenueInterstitial > revenueRewarded) {
                SDKDebugLogger.Log("[Check Ads] Show Interstitial because revenue interstitial is greater than revenue rewarded");
                ShowInterstitialAd(successCallback, failedCallback, "rewardreplace");
                return;
            }
            base.ShowRewardVideoAd(successCallback, failedCallback, m_RewardedVideoPlacement);
            m_IsWatchSuccess = false;
            ShowRewardReady();
        }
        public override bool IsRewardVideoLoaded() {
            if (m_RewardedOrder == null || m_RewardedOrder.Count == 0)
            {
                BuildRewardedOrder();
            }
            foreach (string adUnitId in m_RewardedOrder)
            {
                if (MaxSdk.IsRewardedAdReady(adUnitId))
                {
                    return true;
                }
            }
            return false;
        }

        /************* RewardedVideo Delegates *************/
        private void Rewarded_OnAdLoadedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("RewardedVideoAd MAX Loaded Success: " + adUnitID);
            m_RewardedReadyMap[adUnitID] = true;
            revenueRewarded = adInfo.Revenue;
            SDKDebugLogger.Log("Revenue Rewarded: " + revenueRewarded);
            
            EventManager.AddEventNextFrame(m_RewardedVideoLoadSuccessCallback);
            // m_RewardedVideoLoadSuccessCallback?.Invoke();

            isWaitLoadRewardedUntilAvailable = false;
        }
        private void Rewarded_OnAdLoadedFailEvent(string adUnitID, MaxSdkBase.ErrorInfo adError) {
            SDKDebugLogger.Log("RewardedVideoAd MAX Loaded Fail: " + adUnitID);
            m_RewardedReadyMap[adUnitID] = false;
            EventManager.AddEventNextFrame(m_RewardedVideoLoadFailedCallback);
            // m_RewardedVideoLoadFailedCallback?.Invoke();
            
            int nextIndex = m_CurrentLoadingIndexReward + 1;
            if (nextIndex < m_RewardedOrder.Count)
            {
                SDKDebugLogger.Log($"Loading next rewarded ad at index {nextIndex}");
                LoadRewardedAtIndex(nextIndex);
            }
            else
            {
                isWaitLoadRewardedUntilAvailable = false;
                LoadRewardedFirst();
                SDKDebugLogger.Log("All rewarded ads failed to load");
            }
        }

        private void Rewarded_OnAdRewardedEvent(string adUnitID, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
#if !UNITY_EDITOR
        SDKDebugLogger.Log("unity-script: I got RewardedVideoAdRewardedEvent");
#endif
            m_IsWatchSuccess = true;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                {
                    if (m_RewardedVideoEarnSuccessCallback != null) {
                        SDKDebugLogger.Log("Watch video Success Callback!");
                        EventManager.AddEventNextFrame(m_RewardedVideoEarnSuccessCallback);
                        m_RewardedVideoEarnSuccessCallback = null;
                    }

                    break;
                }
                case RuntimePlatform.IPhonePlayer:
                {
                    if (m_RewardedVideoEarnSuccessCallback != null) {
                        SDKDebugLogger.Log("Watch video Success Callback!");
                        EventManager.AddEventNextFrame(m_RewardedVideoEarnSuccessCallback);
                        m_RewardedVideoEarnSuccessCallback = null;
                    }
                    break;
                }
            }
        }

        private void Rewarded_OnAdClosedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo) {
            SDKDebugLogger.Log("unity-script: I got RewardedVideoAdClosedEvent");
            if (m_RewardedVideoEarnSuccessCallback != null && m_IsWatchSuccess)
            {
                EventManager.AddEventNextFrame(m_RewardedVideoEarnSuccessCallback);
                m_RewardedVideoEarnSuccessCallback = null;
            }
            EventManager.AddEventNextFrame(() => 
            {
                m_RewardedVideoCloseCallback?.Invoke(m_IsWatchSuccess);
            });
            // m_RewardedVideoCloseCallback?.Invoke(m_IsWatchSuccess);

            LoadRewardedFirst();
        }

        private void Rewarded_OnAdStartedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("unity-script: I got RewardedVideoAdStartedEvent");
            
            // Send InMobi display signals (dir_r, dir_nn, dir_type)
            // ABIInMobiPublisherSignalsManager.OnAdDisplayed(adUnitID, adInfo);
            
            // Send updated publisher signals
            // ABIInMobiPublisherSignalsManager.SendAllPublisherSignals();
            
            EventManager.AddEventNextFrame(m_RewardedVideoShowStartCallback);
            
        }

        private void RewardedVideoAdEndedEvent() {
            SDKDebugLogger.Log("unity-script: I got RewardedVideoAdEndedEvent");
        }

        private void Rewarded_OnAdShowFailedEvent(string adUnitID, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) {
            SDKDebugLogger.Log("unity-script: I got RewardedVideoAdShowFailedEvent, code :  " + errorInfo.Code + ", description : " + errorInfo.Message);
            EventManager.AddEventNextFrame(m_RewardedVideoShowFailCallback);
        }

        private void Rewarded_OnAdClickedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo) {
            SDKDebugLogger.Log("unity-script: I got RewardedVideoAdClickedEvent");
            
            // Track ad click for CTR calculation
            // ABIInMobiPublisherSignalsManager.TrackAdClick(AdsType.REWARDED);
        }
        #endregion

        private bool RewardReadyToShow()
        {
            for (int i = 0; i < m_RewardedOrder.Count; i++)
            {
                string adUnitId = m_RewardedOrder[i];
                if (m_RewardedReadyMap[adUnitId])
                {
                    return true;
                }
            }
            return false;
        }

        private void ShowRewardReady()
        {
            SDKDebugLogger.Log("ShowRewardReady");
            for (int i = 0; i < m_RewardedOrder.Count; i++)
            {
                string adUnitId = m_RewardedOrder[i];
                if (m_RewardedReadyMap[adUnitId])
                {
                    SDKDebugLogger.Log("[Check Ads] Show MAX Rewarded with AdUnit: " + adUnitId);
                    m_RewardedReadyMap[adUnitId] = false;
                    MaxSdk.ShowRewardedAd(adUnitId, m_RewardedVideoPlacement);
                    return;
                }
            }

            // if (GameDefaultConfig.EnableInterstitialFallback)
            // {
            //     FallbackToInterstitial();
            // }
            // else
            // {
                EventManager.AddEventNextFrame(m_RewardedVideoShowFailCallback);
                LoadRewardedFirst();
            // }
        }

        private void FallbackToInterstitial()
        {
            SDKDebugLogger.Log("[Check Ads] FallbackToInterstitial");
            // InGameAdsController.EventShowAdsInter?.Invoke(
            //     "backfill",
            //     () => { m_RewardedVideoEarnSuccessCallback?.Invoke(); },
            //     () => { m_RewardedVideoCloseCallback?.Invoke(false); },
            //     () => { m_RewardedVideoShowFailCallback?.Invoke(); },
            //     true,
            //     true
            // );

            LoadRewardedFirst();
        }

        #region Banner

        public MaxSdkBase.BannerPosition m_BannerPosition;
        private bool m_IsBannerLoaded;
        private float maxBannerHeightPercentage = 0.15f;
        public override void InitBannerAds(
            Action bannerLoadedSuccessCallback,
            Action bannerAdLoadedFailCallback,
            Action bannerAdsCollapsedCallback,
            Action bannerAdsExpandedCallback,
            Action bannerAdsDisplayed,
            Action bannerAdsDisplayedFailedCallback,
            Action bannerAdsClickedCallback)
        {
            base.InitBannerAds(
                bannerLoadedSuccessCallback,
                bannerAdLoadedFailCallback,
                bannerAdsCollapsedCallback,
                bannerAdsExpandedCallback,
                bannerAdsDisplayed,
                bannerAdsDisplayedFailedCallback,
                bannerAdsClickedCallback);
            SDKDebugLogger.Log("Banner MAX Init ID = " + m_MaxAdConfig.BannerAdUnitID);
            MaxSdk.CreateBanner(m_MaxAdConfig.BannerAdUnitID, m_BannerPosition);
            //MaxSdk.SetBannerBackgroundColor(m_MaxAdConfig.BannerAdUnitID, Color.black);
            
            // Enforce banner-only format để ngăn MREC được hiển thị
            // Set extra parameter để chỉ cho phép banner format
            MaxSdk.SetBannerExtraParameter(m_MaxAdConfig.BannerAdUnitID, "adaptive_banner", "true");

            MaxSdkCallbacks.Banner.OnAdLoadedEvent += BannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += BannerAdLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += BannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (adUnitID, adInfo) => { OnAdRevenuePaidEvent(AdsType.BANNER, adUnitID, adInfo); };
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent += OnBannerAdCollapsedEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent += OnBannerAdExpandedEvent;
        }

        public override void ShowBannerAds() {
            if (!m_IsBannerLoaded) return;

            if (!CheckBannerHeight())
            {
                return;
            }

            base.ShowBannerAds();
            SDKDebugLogger.Log("MAX Mediation Banner Call Show");
            MaxSdk.ShowBanner(m_MaxAdConfig.BannerAdUnitID);
        }
        public override void HideBannerAds()
        {
            base.HideBannerAds();
            SDKDebugLogger.Log("MAX Mediation Banner Call Hide");
            MaxSdk.HideBanner(m_MaxAdConfig.BannerAdUnitID);
        }

        public override bool IsBannerLoaded()
        {
            return m_IsBannerLoaded;
        }

        private void BannerAdLoadedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo) {
            SDKDebugLogger.Log($"MAX Mediation Banner Loaded Success - AdFormat: {adInfo.AdFormat}, Network: {adInfo.NetworkName}");
            
            // Kiểm tra AdFormat để phát hiện MREC
            if (!string.IsNullOrEmpty(adInfo.AdFormat) && 
                (adInfo.AdFormat.Equals("MREC", StringComparison.OrdinalIgnoreCase) || 
                 adInfo.AdFormat.Equals("MRec", StringComparison.OrdinalIgnoreCase)))
            {
                SDKDebugLogger.Log($"Phát hiện MREC trong banner placement! AdFormat: {adInfo.AdFormat}. Chặn và reload banner.");
                HideBannerAds();
                m_IsBannerLoaded = false;
                EventManager.AddEventNextFrame(m_BannerAdLoadedFailCallback);
                // Reload banner để lấy banner thật
                MaxSdk.LoadBanner(adUnitID);
                return;
            }
            
            // Kiểm tra kích thước thực tế của banner để phát hiện MREC (300x250)
            if (!CheckBannerSize(adUnitID))
            {
                return;
            }
            
            if (!CheckBannerHeight())
            {
                return;
            }
            
            EventManager.AddEventNextFrame(m_BannerAdLoadedSuccessCallback);
            // m_BannerAdLoadedSuccessCallback?.Invoke();
            m_IsBannerLoaded = true;
            
            // Update session depth for InMobi signals when banner is shown
            // Note: Banner ads are shown continuously, so we track when ShowBannerAds is called
        }
        private void BannerAdLoadFailedEvent(string adUnitID, MaxSdkBase.ErrorInfo errorInfo)
        {
            SDKDebugLogger.Log("MAX Mediation Banner Loaded Fail");
            EventManager.AddEventNextFrame(m_BannerAdLoadedFailCallback);
            // m_BannerAdLoadedFailCallback?.Invoke();
            m_IsBannerLoaded = false;
        }
        private void BannerAdClickedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation Banner Clicked");
            
            // Track ad click for CTR calculation
            // ABIInMobiPublisherSignalsManager.TrackAdClick(AdsType.BANNER);
            
            EventManager.AddEventNextFrame(m_BannerAdsClickedCallback);
            // m_BannerAdsClickedCallback?.Invoke();
        }
        private void OnBannerAdCollapsedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation Banner Collapsed");
            EventManager.AddEventNextFrame(m_BannerAdsCollapsedCallback);
            // m_BannerAdsCollapsedCallback?.Invoke();
        }
        private void OnBannerAdExpandedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation Banner Expanded");
            EventManager.AddEventNextFrame(m_BannerAdsExpandedCallback);
            // m_BannerAdsExpandedCallback?.Invoke();
        }

        private bool CheckBannerSize(string adUnitID)
        {
            try
            {
                Rect bannerLayout = MaxSdk.GetBannerLayout(adUnitID);
                float bannerWidth = bannerLayout.width;
                float bannerHeight = bannerLayout.height;
                
                SDKDebugLogger.Log($"Banner Layout - Width: {bannerWidth}px, Height: {bannerHeight}px");
                
                // MREC thường có kích thước 300x250 pixels
                // Banner thông thường có chiều cao 50-90px
                // Kiểm tra nếu banner có chiều cao > 150px thì có thể là MREC
                const float mrecMinHeight = 150f; // MREC thường là 250px, banner thường < 100px
                const float mrecWidthMin = 250f; // MREC thường có width ~300px
                const float mrecWidthMax = 350f;
                
                // Kiểm tra chiều cao và chiều rộng để phát hiện MREC
                if (bannerHeight > mrecMinHeight)
                {
                    // Nếu chiều cao > 150px và chiều rộng trong khoảng MREC (250-350px)
                    if (bannerWidth >= mrecWidthMin && bannerWidth <= mrecWidthMax)
                    {
                        SDKDebugLogger.Log($"Phát hiện MREC dựa trên kích thước! Width: {bannerWidth}px, Height: {bannerHeight}px. Chặn banner.");
                        HideBannerAds();
                        m_IsBannerLoaded = false;
                        EventManager.AddEventNextFrame(m_BannerAdLoadedFailCallback);
                        // Reload banner để lấy banner thật
                        MaxSdk.LoadBanner(adUnitID);
                        return false;
                    }
                    // Nếu chỉ chiều cao > 150px cũng có thể là MREC (an toàn hơn)
                    else if (bannerHeight > 200f)
                    {
                        SDKDebugLogger.Log($"Phát hiện MREC dựa trên chiều cao! Height: {bannerHeight}px (>200px). Chặn banner.");
                        HideBannerAds();
                        m_IsBannerLoaded = false;
                        EventManager.AddEventNextFrame(m_BannerAdLoadedFailCallback);
                        // Reload banner để lấy banner thật
                        MaxSdk.LoadBanner(adUnitID);
                        return false;
                    }
                }
                
                SDKDebugLogger.Log($"Banner kích thước hợp lệ - Width: {bannerWidth}px, Height: {bannerHeight}px");
                return true;
            }
            catch (Exception e)
            {
                SDKDebugLogger.Log($"Lỗi khi kiểm tra kích thước banner: {e.Message}");
                // Nếu không thể lấy layout, tiếp tục với kiểm tra height
                return true;
            }
        }
        
        private bool CheckBannerHeight()
        {
            float bannerHeight = MaxSdkUtils.GetAdaptiveBannerHeight();
            float screenHeight = Screen.height;
            float safeAreaHeight = Screen.safeArea.height;

            float maxBannerHeightInPixels = screenHeight - safeAreaHeight;
            
            float maxAllowedHeight;
            if (maxBannerHeightInPixels > 0 && maxBannerHeightInPixels > screenHeight * maxBannerHeightPercentage)
            {
                // Dùng ngưỡng pixel cố định
                maxAllowedHeight = maxBannerHeightInPixels;
            }
            else
            {
                // Dùng % màn hình
                maxAllowedHeight = screenHeight * maxBannerHeightPercentage;
            }
            
            SDKDebugLogger.Log($"Banner Height: {bannerHeight}px, Screen Height: {screenHeight}px, Safe Area Height: {safeAreaHeight}px, Max Allowed: {maxAllowedHeight}px ({(maxAllowedHeight/screenHeight)*100:F1}%)");

            if (bannerHeight > maxAllowedHeight)
            {
                SDKDebugLogger.Log($"Banner quá cao ({bannerHeight}px > {maxAllowedHeight}px)! Chặn banner để không che nội dung game.");
                HideBannerAds();
                m_IsBannerLoaded = false;
                EventManager.AddEventNextFrame(m_BannerAdLoadedFailCallback);
                return false;
            }
            
            SDKDebugLogger.Log($"Banner hợp lệ ({bannerHeight}px <= {maxAllowedHeight}px). Cho phép hiển thị.");
            return true;
        }
        #endregion

        #region MREC
        private bool m_IsMRecLoaded = false;
        public override void InitRMecAds(Action adLoadedCallback, Action adLoadFailedCallback, Action adClickedCallback, Action adExpandedCallback, Action adCollapsedCallback)
        {
            base.InitRMecAds(adLoadedCallback, adLoadFailedCallback, adClickedCallback, adExpandedCallback, adCollapsedCallback);
            SDKDebugLogger.Log("MAX Start Init MREC");
            MaxSdkCallbacks.MRec.OnAdLoadedEvent      += OnMRecAdLoadedEvent;
            MaxSdkCallbacks.MRec.OnAdLoadFailedEvent  += OnMRecAdLoadFailedEvent;
            MaxSdkCallbacks.MRec.OnAdClickedEvent     += OnMRecAdClickedEvent;
            MaxSdkCallbacks.MRec.OnAdExpandedEvent    += OnMRecAdExpandedEvent;
            MaxSdkCallbacks.MRec.OnAdCollapsedEvent   += OnMRecAdCollapsedEvent;
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += (adUnitID, adInfo) => { OnAdRevenuePaidEvent(AdsType.MREC, adUnitID, adInfo);};
            MaxSdk.CreateMRec(m_MaxAdConfig.MrecAdUnitID, MaxSdkBase.AdViewPosition.BottomCenter);
        }
        public override bool IsMRecLoaded()
        {
            return m_IsMRecLoaded;
        }
        private void OnMRecAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation MREC Loaded Success");
            m_IsMRecLoaded = true;
            m_MRecAdLoadedCallback?.Invoke();
        }
        private void OnMRecAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo error)
        {
            SDKDebugLogger.Log("MAX Mediation MREC Loaded Fail");
            m_IsMRecLoaded = false;
            m_MRecAdLoadFailCallback?.Invoke();
        }
        private void OnMRecAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            m_MRecAdClickedCallback?.Invoke();
        }
        private void OnMRecAdExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            m_MRecAdExpandedCallback?.Invoke();
        }
        private void OnMRecAdCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            m_MRecAdCollapsedCallback?.Invoke();
        }
        public override void ShowMRecAds()
        {
            base.ShowMRecAds();
            MaxSdk.ShowMRec(m_MaxAdConfig.MrecAdUnitID);
        }
        public override void HideMRecAds()
        {
            base.HideMRecAds();
            MaxSdk.HideMRec(m_MaxAdConfig.MrecAdUnitID);
        }
        #endregion
        
        #region App Open Ads

        public override void InitAppOpenAds(Action adLoadedCallback, Action adLoadFailedCallback, 
            Action adClosedCallback, Action adDisplayedCallback, Action adFailedToDisplayCallback)
        {
            base.InitAppOpenAds(adLoadedCallback, adLoadFailedCallback, 
                adClosedCallback, adDisplayedCallback, adFailedToDisplayCallback);
            
            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAppOpenAdLoadedEvent;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAppOpenAdLoadFailedEvent;
            MaxSdkCallbacks.AppOpen.OnAdClickedEvent += OnAppOpenAdClickedEvent;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += (adUnitID, adInfo) => { OnAdRevenuePaidEvent(AdsType.APP_OPEN, adUnitID, adInfo);};
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenAdHiddenEvent;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAppOpenAdDisplayedEvent;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAppOpenAdDisplayFailedEvent;
            RequestAppOpenAds();
        }
        public override void ShowAppOpenAds()
        {
            base.ShowAppOpenAds();
            MaxSdk.ShowAppOpenAd(m_MaxAdConfig.AppOpenAdUnitID);
        }
        public override void RequestAppOpenAds()
        {
            MaxSdk.LoadAppOpenAd(m_MaxAdConfig.AppOpenAdUnitID);
        }
        public override bool IsAppOpenAdsLoaded()
        {
            return MaxSdk.IsAppOpenAdReady(m_MaxAdConfig.AppOpenAdUnitID);
        }
        private void OnAppOpenAdLoadedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation App Open Ads Loaded Success");
            m_AppOpenAdLoadedCallback?.Invoke();
        }
        private void OnAppOpenAdLoadFailedEvent(string adUnitID, MaxSdkBase.ErrorInfo errorInfo)
        {
            SDKDebugLogger.Log("MAX Mediation App Open Ads Loaded Fail");
            m_AppOpenAdLoadFailedCallback?.Invoke();
        }
        private void OnAppOpenAdClickedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
        }
        private void OnAppOpenAdDisplayedEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation App Open Ads Displayed");
            m_AppOpenAdDisplayedCallback?.Invoke();
        }
        private void OnAppOpenAdDisplayFailedEvent(string adUnitID, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation App Open Ads Displayed Fail");
            m_AppOpenAdFailedToDisplayCallback?.Invoke();
        }
        private void OnAppOpenAdHiddenEvent(string adUnitID, MaxSdkBase.AdInfo adInfo)
        {
            SDKDebugLogger.Log("MAX Mediation App Open Ads Hidden");
            m_AppOpenAdClosedCallback?.Invoke();
        }
        #endregion
#endif
        public override AdsMediationType GetAdsMediationType()
        {
            return AdsMediationType.MAX;
        }
    }
    #if !UNITY_AD_MAX
    public enum BannerPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        Centered,
        CenterLeft,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
    #endif
}
