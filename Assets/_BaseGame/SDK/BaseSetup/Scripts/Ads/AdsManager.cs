using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;
using Firebase.RemoteConfig;
using SDK.Analytics;
using Sirenix.OdinInspector;
using UnityEditor;
#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0162 // Unreachable code detected

namespace SDK
{


    public class AdsManager : MonoBehaviour
    {
        #region Fields

        public bool IsCheatAds;
        public static AdsManager Instance { get; private set; }

        [Header("Config")]
        public SDKSetup m_SDKSetup;
        [SerializeField] private double m_AdsLoadingCooldown = 0f;
        [SerializeField] private double m_MaxLoadingCooldown = 5f;
        [SerializeField] private double m_InterstitialCappingAdsCooldown = 0;
        [SerializeField] private double m_MaxInterstitialCappingTime = 30;
        [SerializeField] private int m_LevelPassToShowInterstitial = 2;
        [SerializeField] private int m_RewardInterruptCountTime = 0;
        [SerializeField] private int m_MaxRewardInterruptCount = 5;
        [SerializeField] private bool m_IsActiveInterruptReward = false;
        [SerializeField] private bool IsUpdateRemoteConfigSuccess = false;
        [SerializeField] private bool IsInitedAdsType;
        [SerializeField] private bool IsRemoveAds;
        [SerializeField] private bool IsActiveMRECAds;
        [SerializeField] public bool IsFirstOpen;
        [SerializeField] public bool IsLinkRewardWithRemoveAds;

        [Header("AdsMediation Controllers")]
        public AdsType resumeAdsType;

        public AdsMediationType m_MainAdsMediationType = AdsMediationType.MAX;
        public List<AdsConfig> m_AdsConfigs = new List<AdsConfig>();
        public List<AdsMediationController> m_AdsMediationControllers = new List<AdsMediationController>();

        private const string key_local_remove_ads = "key_local_remove_ads";

        #endregion

        #region System

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            EventManager.StartListening("UpdateRemoteConfigs", UpdateRemoteConfigs);
            m_IsActiveInterruptReward = true;
            LoadRemoveAds();
            IsFirstOpen = PlayerPrefs.GetInt("first_open", 0) == 0;
            SDKDebugLogger.Log("Is First Open " + IsFirstOpen);
            PlayerPrefs.SetInt("first_open", 1);
        }

        private void Start()
        {
            Init();
        }

        private void UpdateRemoteConfigs()
        {
            // {
            //     ConfigValue configValue =
            //         FirebaseManager.Instance.GetConfigValue(Keys.key_remote_interstitial_capping_time);
            //     m_MaxInterstitialCappingTime = configValue.DoubleValue;
            //     SDKDebugLogger.Log("=============== Max Interstitial Capping Time " + m_MaxInterstitialCappingTime);
            // }
            // {
            //     ConfigValue configValue =
            //         FirebaseManager.Instance.GetConfigValue(Keys.key_remote_inter_reward_interspersed);
            //     m_IsActiveInterruptReward = configValue.BooleanValue;
            //     SDKDebugLogger.Log("=============== Active " + m_IsActiveInterruptReward);
            // }
            // {
            //     ConfigValue configValue =
            //         FirebaseManager.Instance.GetConfigValue(Keys.key_remote_inter_reward_interspersed_time);
            //     m_MaxRewardInterruptCount = (int)configValue.DoubleValue;
            //     SDKDebugLogger.Log("=============== MAX Reward InteruptCount" + m_MaxRewardInterruptCount);
            // }
            // {
            //     m_LevelPassToShowInterstitial =
            //         (int)FirebaseManager.Instance.GetConfigDouble(Keys.key_remote_interstitial_level);
            //     SDKDebugLogger.Log("=============== Level Pass Show Interstitial " + m_LevelPassToShowInterstitial);
            // }
            // {
            //     IsActiveMRECAds = ABIFirebaseManager.Instance.GetConfigBool(Keys.key_remote_mrec_active);
            //     SDKDebugLogger.Log("=============== Active MREC Ads " + IsActiveMRECAds);
            // }

            UpdateAOARemoteConfig();
            UpdateRemoteConfigResumeAds();
            IsUpdateRemoteConfigSuccess = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (m_InterstitialCappingAdsCooldown > 0)
            {
                m_InterstitialCappingAdsCooldown -= dt;
            }

            if (m_AdsLoadingCooldown > 0)
            {
                m_AdsLoadingCooldown -= dt;
                if (m_AdsLoadingCooldown <= 0)
                {
                    // if (!IsRewardVideoLoaded())
                    // {
                    //     RequestRewardVideo();
                    // }

                    // if (!IsInterstitialAdLoaded())
                    // {
                    //     RequestInterstitial();
                    // }
                }
            }

            UpdateBanner();
            UpdateCollapsibleBanner(dt);
        }

        private void Init()
        {
            StartCoroutine(coWaitForFirebaseInitialization());

        }

        private IEnumerator coWaitForFirebaseInitialization()
        {
            while (!FirebaseRemoteConfigManager.IsReady)
            {
                yield return new WaitForEndOfFrame();
            }

            InitConfig();
            InitAdsMediation();
        }

        private void InitConfig()
        {
            foreach (AdsConfig adsConfig in m_AdsConfigs)
            {
                AdsMediationType adsMediationType = m_SDKSetup.GetAdsMediationType(adsConfig.adsType);
                adsConfig.Init(GetAdsMediationController(adsMediationType), OnAdRevenuePaidEvent);
            }
        }

        private void InitAdsMediation()
        {
            SDKDebugLogger.Log("Init Ads Mediation");
            {
                AdsMediationController adsMediationController = GetSelectedMediation(AdsType.INTERSTITIAL);
                if (adsMediationController != null && !adsMediationController.IsInited)
                {
                    GetSelectedMediation(AdsType.INTERSTITIAL).Init();
                }
            }

            {
                AdsMediationController adsMediationController = GetSelectedMediation(AdsType.REWARDED);
                if (adsMediationController != null && !adsMediationController.IsInited)
                {
                    GetSelectedMediation(AdsType.REWARDED).Init();
                }
            }

            {
                AdsMediationController adsMediationController = GetSelectedMediation(AdsType.BANNER);
                if (adsMediationController != null && !adsMediationController.IsInited)
                {
                    GetSelectedMediation(AdsType.BANNER).Init();
                }
            }

            {
                AdsMediationController adsMediationController = GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER);
                if (adsMediationController != null && !adsMediationController.IsInited)
                {
                    GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER).Init();
                }
            }

            // {
            //     AdsMediationController adsMediationController = GetSelectedMediation(AdsType.MREC);
            //     if (adsMediationController != null && !adsMediationController.IsInited)
            //     {
            //         GetSelectedMediation(AdsType.MREC).Init();
            //     }
            // }

            {
                AdsMediationController adsMediationController = GetSelectedMediation(AdsType.APP_OPEN);
                if (adsMediationController != null && !adsMediationController.IsInited)
                {
                    GetSelectedMediation(AdsType.APP_OPEN).Init();
                }
            }
        }

        public void InitAdsType(AdsMediationType adsMediationType)
        {
            SDKDebugLogger.Log("Init Ads Type");
            //Setup Interstitial
            SetupInterstitial(adsMediationType);

            //Setup Reward Video
            SetupRewardVideo(adsMediationType);

            //Setup Banner
            SetupBannerAds(adsMediationType);

            //Setup Collapsible Banner
            SetupCollapsibleBannerAds(adsMediationType);

            //Setup RMecAds
            // SetupMRecAds(adsMediationType);

            //Setup AppOpenAds
            SetupAppOpenAds(adsMediationType);

            IsInitedAdsType = true;
        }

        private void LoadRemoveAds()
        {
            IsRemoveAds = PlayerPrefs.GetInt(key_local_remove_ads, 0) == 1;
        }

        public void SetRemoveAds(bool isRemove)
        {
            IsRemoveAds = isRemove;
            PlayerPrefs.SetInt(key_local_remove_ads, isRemove ? 1 : 0);
            DestroyBanner();
            DestroyCollapsibleBanner();
        }

        private AdsConfig GetAdsConfig(AdsType adsType)
        {
            return m_AdsConfigs.Find(x => x.adsType == adsType);
        }

        private AdsMediationController GetSelectedMediation(AdsType adsType)
        {
            return adsType switch
            {
                AdsType.BANNER => BannerAdsConfig.GetAdsMediation(),
                AdsType.COLLAPSIBLE_BANNER => CollapsibleBannerAdsConfig.GetAdsMediation(),
                AdsType.INTERSTITIAL => InterstitialAdsConfig.GetAdsMediation(),
                AdsType.REWARDED => RewardVideoAdsConfig.GetAdsMediation(),
                AdsType.MREC => MRecAdsConfig.GetAdsMediation(),
                AdsType.APP_OPEN => AppOpenAdsConfig.GetAdsMediation(),
                _ => null
            };
        }

        private AdsMediationController GetAdsMediationController(AdsMediationType adsMediationType)
        {
            return adsMediationType switch
            {
                AdsMediationType.MAX => m_AdsMediationControllers[0],
                AdsMediationType.ADMOB => m_AdsMediationControllers[1],
                AdsMediationType.IRONSOURCE => m_AdsMediationControllers[2],
                _ => null
            };
        }

        #endregion

        #region EditorUpdate

        public void UpdateAdsMediationConfig()
        {
            if (m_SDKSetup == null) return;
            UpdateAdsMediationConfig(m_SDKSetup);
        }

        public void UpdateAdsMediationConfig(SDKSetup sdkSetup)
        {
            m_SDKSetup = sdkSetup;
            m_MainAdsMediationType = m_SDKSetup.adsMediationType;
            foreach (AdsConfig adsConfig in m_AdsConfigs)
            {
                AdsMediationType adsMediationType = m_SDKSetup.GetAdsMediationType(adsConfig.adsType);
                adsConfig.adsMediationType = adsMediationType;
            }

            IsLinkRewardWithRemoveAds = m_SDKSetup.IsLinkToRemoveAds;
            UpdateMaxMediation();
            UpdateAdmobMediation();
            UpdateIronSourceMediation();
        }

        private void UpdateMaxMediation()
        {
#if UNITY_AD_MAX
            const AdsMediationType adsMediationType = AdsMediationType.MAX;
            MaxMediationController maxMediationController =
                GetAdsMediationController(adsMediationType) as MaxMediationController;
            if (maxMediationController == null) return;
            if (m_SDKSetup.adsMediationType == adsMediationType)
            {
                maxMediationController.m_MaxAdConfig.SDKKey = m_SDKSetup.maxAdsSetup.SDKKey;
            }

            maxMediationController.m_MaxAdConfig.InterstitialAdUnitID =
                m_SDKSetup.interstitialAdsMediationType == adsMediationType
                    ? m_SDKSetup.maxAdsSetup.InterstitialAdUnitID
                    : "";

            maxMediationController.m_MaxAdConfig.RewardedAdUnitID =
                m_SDKSetup.rewardedAdsMediationType == adsMediationType ? m_SDKSetup.maxAdsSetup.RewardedAdUnitID : "";

            maxMediationController.m_MaxAdConfig.BannerAdUnitID = m_SDKSetup.bannerAdsMediationType == adsMediationType
                ? m_SDKSetup.maxAdsSetup.BannerAdUnitID
                : "";
#if UNITY_AD_MAX
            maxMediationController.m_BannerPosition = m_SDKSetup.maxBannerAdsPosition;
#endif

            maxMediationController.m_MaxAdConfig.CollapsibleBannerAdUnitID =
                m_SDKSetup.collapsibleBannerAdsMediationType == adsMediationType
                    ? m_SDKSetup.maxAdsSetup.CollapsibleBannerAdUnitID
                    : "";

            maxMediationController.m_MaxAdConfig.MrecAdUnitID = m_SDKSetup.mrecAdsMediationType == adsMediationType
                ? m_SDKSetup.maxAdsSetup.MrecAdUnitID
                : "";

            maxMediationController.m_MaxAdConfig.AppOpenAdUnitID =
                m_SDKSetup.appOpenAdsMediationType == adsMediationType ? m_SDKSetup.maxAdsSetup.AppOpenAdUnitID : "";

#if UNITY_EDITOR
            EditorUtility.SetDirty(maxMediationController);
            SDKDebugLogger.Log("Update Max Mediation Done");
#endif
#endif
        }

        private void UpdateAdmobMediation()
        {
#if UNITY_AD_ADMOB
            const AdsMediationType adsMediationType = AdsMediationType.ADMOB;
            AdmobMediationController admobMediationController =
                GetAdsMediationController(adsMediationType) as AdmobMediationController;
            if (admobMediationController == null) return;
            if (m_SDKSetup.interstitialAdsMediationType == adsMediationType)
            {
                m_MainAdsMediationType = adsMediationType;
                admobMediationController.m_AdmobAdSetup.InterstitialAdUnitIDList =
                    m_SDKSetup.admobAdsSetup.InterstitialAdUnitIDList;
            }
            else
            {
                admobMediationController.m_AdmobAdSetup.InterstitialAdUnitIDList = new List<string>();
            }

            admobMediationController.m_AdmobAdSetup.RewardedAdUnitIDList =
                m_SDKSetup.rewardedAdsMediationType == adsMediationType
                    ? m_SDKSetup.admobAdsSetup.RewardedAdUnitIDList
                    : new List<string>();

            {
                admobMediationController.m_AdmobAdSetup.BannerAdUnitIDList =
                    m_SDKSetup.bannerAdsMediationType == adsMediationType
                        ? m_SDKSetup.admobAdsSetup.BannerAdUnitIDList
                        : new List<string>();
                admobMediationController.IsBannerShowingOnStart = m_SDKSetup.isBannerShowingOnStart;
                admobMediationController.m_BannerPosition = m_SDKSetup.admobBannerAdsPosition;
            }

            {
                admobMediationController.m_AdmobAdSetup.CollapsibleBannerAdUnitIDList =
                    m_SDKSetup.collapsibleBannerAdsMediationType == adsMediationType
                        ? m_SDKSetup.admobAdsSetup.CollapsibleBannerAdUnitIDList
                        : new List<string>();
                admobMediationController.IsCollapsibleBannerShowingOnStart =
                    m_SDKSetup.isShowingOnStartCollapsibleBanner;
                IsAutoCloseCollapsibleBanner = m_SDKSetup.isAutoCloseCollapsibleBanner;
                m_AutoCloseTimeCollapsibleBanner = m_SDKSetup.autoCloseTime;

                IsAutoRefreshCollapsibleBanner = m_SDKSetup.isAutoRefreshCollapsibleBanner;
                IsAutoRefreshExtendCollapsibleBanner = m_SDKSetup.isAutoRefreshExtendCollapsibleBanner;
                m_AutoRefreshTimeCollapsibleBanner = m_SDKSetup.autoRefreshTime;

                admobMediationController.m_CollapsibleBannerPosition = m_SDKSetup.adsPositionCollapsibleBanner;
            }
            {
                admobMediationController.m_AdmobAdSetup.MrecAdUnitIDList =
                    m_SDKSetup.mrecAdsMediationType == adsMediationType
                        ? m_SDKSetup.admobAdsSetup.MrecAdUnitIDList
                        : new List<string>();
                admobMediationController.m_MRECPosition = m_SDKSetup.mrecAdsPosition;
            }
            admobMediationController.m_AdmobAdSetup.AppOpenAdUnitIDList =
                m_SDKSetup.appOpenAdsMediationType == adsMediationType
                    ? m_SDKSetup.admobAdsSetup.AppOpenAdUnitIDList
                    : new List<string>();
#if UNITY_EDITOR
            EditorUtility.SetDirty(admobMediationController);
            SDKDebugLogger.Log("Update Admob Mediation Done");
#endif
#endif
        }

        private void UpdateIronSourceMediation()
        {
#if UNITY_AD_IRONSOURCE
            const AdsMediationType adsMediationType = AdsMediationType.IRONSOURCE;
            IronSourceMediationController ironSourceMediationController =
 GetAdsMediationController(adsMediationType) as IronSourceMediationController;
            if (ironSourceMediationController == null) return;
            if (m_SDKSetup.adsMediationType == adsMediationType)
            {
                ironSourceMediationController.AppKey = m_SDKSetup.ironSourceAdSetup.appKey;
            }
            
            ironSourceMediationController.interstitialAdUnitID =
 m_SDKSetup.interstitialAdsMediationType == adsMediationType ? m_SDKSetup.ironSourceAdSetup.interstitialID : "";
            ironSourceMediationController.rewardedAdUnitID =
 m_SDKSetup.rewardedAdsMediationType == adsMediationType ? m_SDKSetup.ironSourceAdSetup.rewardedID : "";
            ironSourceMediationController.bannerAdUnitID =
 m_SDKSetup.bannerAdsMediationType == adsMediationType ? m_SDKSetup.ironSourceAdSetup.bannerID : "";
            isAutoRefreshBannerByCode = m_SDKSetup.isAutoRefreshBannerByCode;
#if UNITY_EDITOR
            EditorUtility.SetDirty(ironSourceMediationController);
            SDKDebugLogger.Log("Update IronSource Mediation Done");
#endif
#endif
        }

        #endregion

        #region Interstitial

        private AdsConfig InterstitialAdsConfig => GetAdsConfig(AdsType.INTERSTITIAL);

        private Action m_InterstitialAdCloseCallback;
        private Action m_InterstitialAdLoadSuccessCallback;
        private Action m_InterstitialAdLoadFailCallback;
        private Action m_InterstitialAdShowSuccessCallback;
        private Action m_InterstitialAdShowFailCallback;

        private string m_InterstitialPlacement;

        private void SetupInterstitial(AdsMediationType adsMediationType)
        {
            if (adsMediationType != m_SDKSetup.interstitialAdsMediationType) return;
            if (IsRemoveAds) return;
            SDKDebugLogger.Log("Setup Interstitial");
            InterstitialAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.INTERSTITIAL);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.INTERSTITIAL)) return;
            foreach (AdsMediationController t in InterstitialAdsConfig.adsMediations)
            {
                t.InitInterstitialAd(
                    OnInterstitialAdClosed,
                    OnInterstitialAdSuccessToLoad,
                    OnInterstitialAdFailedToLoad,
                    OnInterstitialAdShowSuccess,
                    OnInterstitialAdShowFail
                );
            }

            SDKDebugLogger.Log("Setup Interstitial Done");
        }

        private void RequestInterstitial()
        {
//             if (GetSelectedMediation(AdsType.INTERSTITIAL).IsInterstitialLoaded()) return;
// #if !UNITY_EDITOR
//             GetSelectedMediation(AdsType.INTERSTITIAL).RequestInterstitialAd();
// #endif
        }

        public void ShowInterstitial(string interstitialPlacement, Action showSuccessCallback = null, Action closedCallback = null,
            Action showFailCallback = null,
            bool isTracking = true, bool isSkipCapping = false)
        {

            if (IsCheatAds)
            {
                showSuccessCallback?.Invoke();
                return;
            }

            if (!isSkipCapping)
            {
                if (m_InterstitialCappingAdsCooldown > 0)
                {
                    showSuccessCallback?.Invoke();
                    closedCallback?.Invoke();
                    return;
                }
            }

            m_InterstitialPlacement = interstitialPlacement;
            m_InterstitialAdShowSuccessCallback = showSuccessCallback;
            m_InterstitialAdShowFailCallback = showFailCallback;
            m_InterstitialAdCloseCallback = closedCallback;

            if (isTracking)
            {
                // AnalyticsManager.TrackAdsInterstitial_ClickOnButton();
            }

            if (!IsRemoveAds)
            {
                if (IsInterstitialAdLoaded())
                {
                    ShowSelectedInterstitialAd();
                }
                else
                {
                    SDKDebugLogger.Log("Interstitial not loaded");
                    OnInterstitialAdShowFail();
                }
            }
            else
            {
                m_InterstitialAdCloseCallback?.Invoke();
                showSuccessCallback?.Invoke();
            }
        }

        public void ShowSelectedInterstitialAdDebug()
        {
            ShowSelectedInterstitialAd();
        }

        private void ShowSelectedInterstitialAd()
        {
            IsShowingAds = true;

            GetSelectedMediation(AdsType.INTERSTITIAL).ShowInterstitialAd(OnInterstitialAdShowSuccess, OnInterstitialAdShowFail, m_InterstitialPlacement);
        }

        public bool IsInterstitialAdLoaded()
        {
            if(GetSelectedMediation(AdsType.INTERSTITIAL)!=null)
                return GetSelectedMediation(AdsType.INTERSTITIAL).IsInterstitialLoaded();
            return false;
        }

        private void ResetAdsLoadingCooldown()
        {
            m_AdsLoadingCooldown = m_MaxLoadingCooldown;
        }

        private void ResetAdsInterstitialCappingTime()
        {
            m_InterstitialCappingAdsCooldown = m_MaxInterstitialCappingTime;
        }

        private void OnInterstitialAdSuccessToLoad()
        {
            InterstitialAdsConfig.RefreshLoadAds();
            m_InterstitialAdLoadSuccessCallback?.Invoke();
            // AnalyticsManager.TrackAdsInterstitial_LoadedSuccess();
        }

        private void OnInterstitialAdFailedToLoad()
        {

            ResetAdsLoadingCooldown();
            m_InterstitialAdLoadFailCallback?.Invoke();
        }

        private void OnInterstitialAdShowSuccess()
        {
            SDKDebugLogger.Log("Interstitial Show Success");
            MarkShowingAds(true);
            m_InterstitialAdShowSuccessCallback?.Invoke();
            // AnalyticsManager.TrackAdsInterstitial_ShowSuccess();
        }

        private void OnInterstitialAdShowFail()
        {
            SDKDebugLogger.Log("Interstitial Show Fail");
            MarkShowingAds(false);
            InterstitialAdsConfig.MarkReloadFail();
            m_InterstitialAdShowFailCallback?.Invoke();
            // AnalyticsManager.TrackAdsInterstitial_ShowFail();
        }

        private void OnInterstitialAdClosed()
        {
            SDKDebugLogger.Log("Interstitial Closed");
            ResetAdsInterstitialCappingTime();
            m_InterstitialAdCloseCallback?.Invoke();
            MarkShowingAds(false);
        }

        public bool IsReadyToShowInterstitial()
        {
            return IsInterstitialAdLoaded() && m_InterstitialCappingAdsCooldown <= 0;
        }

        public bool IsPassLevelToShowInterstitial(int level)
        {
            SDKDebugLogger.Log("currentLevel: " + level + " Level need passed " + m_LevelPassToShowInterstitial);
            return level >= m_LevelPassToShowInterstitial;
        }

        #endregion

        #region Banner Ads

        private AdsConfig BannerAdsConfig => GetAdsConfig(AdsType.BANNER);
        public float BannerCountTime { get; private set; }
        private const float banner_reset_time = 15f;
        private bool isAutoRefreshBannerByCode;
        private bool m_IsBannerShowing;

        private void UpdateBanner()
        {
            if (IsRemoveAds) return;
            if (!BannerAdsConfig.isActive) return;
            if (!m_IsBannerShowing) return;
            if (isAutoRefreshBannerByCode)
            {
                BannerCountTime += Time.deltaTime;
                if (BannerCountTime >= banner_reset_time)
                {
                    BannerCountTime = 0;
                    DestroyBanner();
                    RequestBanner();
                }
            }
        }

        private void SetupBannerAds(AdsMediationType adsMediationType)
        {
            if (adsMediationType != m_SDKSetup.bannerAdsMediationType) return;
            if (IsCheatAds || IsRemoveAds) return;
            SDKDebugLogger.Log("Setup Banner");
            BannerAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.BANNER);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.BANNER)) return;
            foreach (AdsMediationController t in BannerAdsConfig.adsMediations)
            {
                t.InitBannerAds(
                    OnBannerLoadedSuccess,
                    OnBannerLoadedFail,
                    OnBannerCollapsed,
                    OnBannerExpanded,
                    OnBannerDisplayed,
                    OnBannerDisplayedFail,
                    OnBannerClicked);
            }

            HideBannerAds();
            SDKDebugLogger.Log("Setup Banner Done");
        }

        public bool IsBannerShowing()
        {
            return m_IsBannerShowing;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void RequestBanner()
        {
            if (!BannerAdsConfig.isActive) return;
            GetSelectedMediation(AdsType.BANNER).RequestBannerAds();
        }

        public void ShowBannerAds()
        {
            SDKDebugLogger.Log(("Call Show Banner Ads"));
            if (IsCheatAds || IsRemoveAds) return;
            GetSelectedMediation(AdsType.BANNER)?.ShowBannerAds();

            BannerCountTime = 0;
        }

        public void HideBannerAds()
        {
            SDKDebugLogger.Log(("Call Hide Banner Ads"));
            GetSelectedMediation(AdsType.BANNER)?.HideBannerAds();

        }

        public void DestroyBanner()
        {
            GetSelectedMediation(AdsType.BANNER)?.DestroyBannerAds();
            m_IsBannerShowing = false;
        }

        public bool IsBannerLoaded()
        {
            AdsMediationController mediation = GetSelectedMediation(AdsType.BANNER);
            return mediation != null && mediation.IsBannerLoaded();
        }

        private void OnBannerLoadedSuccess()
        {
            SDKDebugLogger.Log("Banner Loaded");
            BannerCountTime = 0;
        }

        private void OnBannerLoadedFail()
        {
            SDKDebugLogger.Log("Banner Load Fail");
            BannerCountTime = 0;
        }

        private void OnBannerDisplayed()
        {
            SDKDebugLogger.Log("Banner Displayed");
            m_IsBannerShowing = true;

        }

        private void OnBannerClicked()
        {
            SDKDebugLogger.Log("Banner Clicked");
            m_IsShowingAds = true;
        }

        private void OnBannerDisplayedFail()
        {
            SDKDebugLogger.Log("Banner Displayed Fail");
            m_IsBannerShowing = false;
        }

        private void OnBannerExpanded()
        {
            SDKDebugLogger.Log("Banner Expanded");
        }

        private void OnBannerCollapsed()
        {
            SDKDebugLogger.Log("Banner Collapsed");
        }

        #endregion

        #region Collapsible Banner

        private AdsConfig CollapsibleBannerAdsConfig => GetAdsConfig(AdsType.COLLAPSIBLE_BANNER);
        private bool IsExpandedCollapsibleBanner;
        private bool IsShowingCollapsibleBanner;

        [BoxGroup("Collapsible Banner")] public bool IsAutoRefreshCollapsibleBanner;
        [BoxGroup("Collapsible Banner")] public bool IsAutoRefreshExtendCollapsibleBanner;
        [BoxGroup("Collapsible Banner")] public float m_AutoRefreshTimeCollapsibleBanner;
        private float m_RefreshTimeCounterCollapsibleBanner;

        [BoxGroup("Collapsible Banner")] public bool IsAutoCloseCollapsibleBanner;
        [BoxGroup("Collapsible Banner")] public float m_AutoCloseTimeCollapsibleBanner = 20;
        private float m_CloseTimeCounterCollapsibleBanner;

        private UnityAction m_CollapsibleBannerCloseCallback;

        private void SetupCollapsibleBannerAds(AdsMediationType adsMediationType)
        {
            StartCoroutine(coDelayInitCollapsibleBannerAds(adsMediationType));
        }

        private IEnumerator coDelayInitCollapsibleBannerAds(AdsMediationType adsMediationType)
        {
            yield return new WaitForSeconds(5);
            SetupCollapsibleBannerAdMediation(adsMediationType);
        }

        private void SetupCollapsibleBannerAdMediation(AdsMediationType adsMediationType)
        {
            if (IsCheatAds || IsRemoveAds) return;
            if (adsMediationType != m_SDKSetup.collapsibleBannerAdsMediationType) return;
            SDKDebugLogger.Log("Setup Banner");
            CollapsibleBannerAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.COLLAPSIBLE_BANNER);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.COLLAPSIBLE_BANNER)) return;
            foreach (AdsMediationController t in CollapsibleBannerAdsConfig.adsMediations)
            {
                t.InitCollapsibleBannerAds(
                    OnCollapsibleBannerLoadedSucess, OnCollapsibleBannerLoadedFail, OnCollapsibleBannerCollapsed,
                    OnCollapsibleBannerExpanded, OnCollapsibleBannerDestroyed, OnCollapsibleBannerHide);
            }

            SDKDebugLogger.Log("Setup Banner Done");
        }

        public bool IsCollapsibleBannerExpended()
        {
            return IsExpandedCollapsibleBanner;
        }

        public bool IsCollapsibleBannerShowing()
        {
            return IsShowingCollapsibleBanner;
        }

        private void UpdateCollapsibleBanner(float dt)
        {
            if (IsRemoveAds) return;
            if (IsAutoCloseCollapsibleBanner)
            {
                if (m_CloseTimeCounterCollapsibleBanner > 0)
                {
                    m_CloseTimeCounterCollapsibleBanner -= dt;
                    if (m_CloseTimeCounterCollapsibleBanner <= 0)
                    {
                        HideCollapsibleBannerAds();
                        m_CollapsibleBannerCloseCallback?.Invoke();
                    }
                }
            }

            if (IsAutoRefreshCollapsibleBanner)
            {
                if (m_RefreshTimeCounterCollapsibleBanner > 0)
                {
                    m_RefreshTimeCounterCollapsibleBanner -= dt;
                    if (m_RefreshTimeCounterCollapsibleBanner <= 0)
                    {
                        if (IsAutoRefreshExtendCollapsibleBanner)
                        {
                            ShowCollapsibleBannerAds();
                        }
                        else
                        {
                            RefreshCollapsibleBanner();
                        }

                        m_RefreshTimeCounterCollapsibleBanner = 0;
                    }
                }
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void RequestCollapsibleBanner()
        {
            if (!CollapsibleBannerAdsConfig.isActive || IsRemoveAds) return;
            GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER)?.RequestCollapsibleBannerAds(false);
        }

        public void RefreshCollapsibleBanner()
        {
            if (!CollapsibleBannerAdsConfig.isActive || IsRemoveAds) return;
            GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER)?.RefreshCollapsibleBannerAds();
        }

        public void ShowCollapsibleBannerAds(bool isAutoClose = false, UnityAction closeCallback = null)
        {
            SDKDebugLogger.Log(("Call Show Collapsible Banner Ads"));
            if (IsCheatAds || IsRemoveAds) return;
            if (GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER) == null) return;
            IsAutoCloseCollapsibleBanner = isAutoClose;
            m_CollapsibleBannerCloseCallback = closeCallback;
            m_RefreshTimeCounterCollapsibleBanner = 0;
            GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER).ShowCollapsibleBannerAds();
        }

        public void HideCollapsibleBannerAds()
        {
            GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER)?.HideCollapsibleBannerAds();
        }

        public void DestroyCollapsibleBanner()
        {
            GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER)?.DestroyCollapsibleBannerAds();
            IsShowingCollapsibleBanner = false;
        }

        public bool IsCollapsibleBannerLoaded()
        {
            AdsMediationController mediation = GetSelectedMediation(AdsType.COLLAPSIBLE_BANNER);
            return mediation != null && mediation.IsCollapsibleBannerLoaded();
        }

        private void OnCollapsibleBannerLoadedSucess()
        {
            SDKDebugLogger.Log("Collapsible Banner Loaded");
            m_RefreshTimeCounterCollapsibleBanner = m_AutoRefreshTimeCollapsibleBanner;
        }

        private void OnCollapsibleBannerLoadedFail()
        {
            SDKDebugLogger.Log("Collapsible Banner Load Fail");
        }

        private void OnCollapsibleBannerExpanded()
        {
            SDKDebugLogger.Log("Collapsible Banner Expanded");
            IsExpandedCollapsibleBanner = true;
            IsShowingCollapsibleBanner = true;
            m_RefreshTimeCounterCollapsibleBanner = 0;
        }

        private void OnCollapsibleBannerCollapsed()
        {
            SDKDebugLogger.Log("Collapsible Banner Collapsed");
            IsExpandedCollapsibleBanner = false;
            m_CloseTimeCounterCollapsibleBanner = m_AutoCloseTimeCollapsibleBanner;
            m_RefreshTimeCounterCollapsibleBanner = m_AutoRefreshTimeCollapsibleBanner;
        }

        private void OnCollapsibleBannerDestroyed()
        {
            SDKDebugLogger.Log("Collapsible Banner Destroyed");
            IsShowingCollapsibleBanner = false;
        }

        private void OnCollapsibleBannerHide()
        {
            SDKDebugLogger.Log("Collapsible Banner Hide");
            IsShowingCollapsibleBanner = false;
        }

        public bool IsCollapsibleBannerShowingTimeOut()
        {
            return m_CloseTimeCounterCollapsibleBanner <= 0;
        }

        #endregion

        #region Reward Ads

        private AdsConfig RewardVideoAdsConfig => GetAdsConfig(AdsType.REWARDED);

        private Action<bool> m_RewardedVideoCloseCallback;
        private Action m_RewardedVideoLoadSuccessCallback;
        private Action m_RewardedVideoLoadFailedCallback;
        private Action m_RewardedVideoEarnSuccessCallback;
        private Action m_RewardedVideoShowStartCallback;
        private Action m_RewardedVideoShowFailCallback;

        private string m_RewardedPlacement;

        // Reward Video Setup
        private void SetupRewardVideo(AdsMediationType adsMediationType)
        {
            if (IsRemoveAds && IsLinkRewardWithRemoveAds) return;
            if (adsMediationType != m_SDKSetup.rewardedAdsMediationType) return;
            SDKDebugLogger.Log("Setup Reward Video");
            RewardVideoAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.REWARDED);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.REWARDED)) return;
            foreach (AdsMediationController t in RewardVideoAdsConfig.adsMediations)
            {
                t.InitRewardVideoAd(
                    OnRewardVideoClosed,
                    OnRewardVideoLoadSuccess,
                    OnRewardVideoLoadFail,
                    OnRewardVideoStart
                );
            }

            SDKDebugLogger.Log("Setup Reward Video Done");
        }

        public void RequestRewardVideo()
        {
            // if (IsRemoveAds && IsLinkRewardWithRemoveAds) return;
            // if (GetSelectedMediation(AdsType.REWARDED).IsRewardVideoLoaded()) return;
            // GetSelectedMediation(AdsType.REWARDED).RequestRewardVideoAd();
        }

        public void ShowRewardVideo(string rewardedPlacement, Action successCallback,
            Action<bool> closedCallback = null, Action failedCallback = null)
        {
            if (IsCheatAds)
            {
                successCallback?.Invoke();
                return;
            }

            m_RewardedPlacement = rewardedPlacement;
            m_RewardedVideoEarnSuccessCallback = successCallback;
            m_RewardedVideoShowFailCallback = failedCallback;
            m_RewardedVideoCloseCallback = closedCallback;
            // AnalyticsManager.TrackAdsReward_ClickOnButton();
            if (IsRemoveAds && IsLinkRewardWithRemoveAds)
            {
                OnRewardVideoEarnSuccess();
            }
            else
            {
                if (m_IsActiveInterruptReward && IsReadyToShowRewardInterrupt() && IsInterstitialAdLoaded())
                {
                    MarkShowingAds(true);
                    ShowInterstitial(m_InterstitialPlacement, null, () =>
                    {
                        successCallback();
                        ResetRewardInterruptCount();
                    }, failedCallback, false, true);
                }
                else
                {
                    MarkShowingAds(true);
                    GetSelectedMediation(AdsType.REWARDED)
                        .ShowRewardVideoAd(OnRewardVideoEarnSuccess, OnRewardVideoShowFail, rewardedPlacement);
                }
            }
        }

        public bool IsRewardVideoLoaded()
        {
            return GetSelectedMediation(AdsType.REWARDED) != null;
        }

        private void OnRewardVideoEarnSuccess()
        {
            m_RewardedVideoEarnSuccessCallback?.Invoke();
            m_RewardInterruptCountTime++;
            // AnalyticsManager.TrackAdsReward_ShowCompleted(m_RewardedPlacement);
        }

        private void OnRewardVideoStart()
        {
            m_RewardedVideoShowStartCallback?.Invoke();
            MarkShowingAds(true);
            // AnalyticsManager.TrackAdsReward_StartShow();
        }

        private void OnRewardVideoShowFail()
        {
            m_RewardedVideoShowFailCallback?.Invoke();
            // AnalyticsManager.TrackAdsReward_ShowFail();
        }

        private void OnRewardVideoClosed(bool isWatchedSuccess)
        {
            ResetAdsInterstitialCappingTime();
            m_RewardedVideoCloseCallback?.Invoke(isWatchedSuccess);
            MarkShowingAds(false);
        }

        private void OnRewardVideoLoadSuccess()
        {
            RewardVideoAdsConfig.RefreshLoadAds();
            m_RewardedVideoLoadSuccessCallback?.Invoke();
            // AnalyticsManager.TrackAdsReward_LoadSuccess();
        }

        private void OnRewardVideoLoadFail()
        {
            ResetAdsLoadingCooldown();
            RewardVideoAdsConfig.MarkReloadFail();
            m_RewardedVideoLoadFailedCallback?.Invoke();
        }

        public bool IsReadyToShowRewardVideo()
        {
            return IsRewardVideoLoaded();
        }

        public bool IsReadyToShowRewardInterrupt()
        {
            return m_RewardInterruptCountTime >= m_MaxRewardInterruptCount;
        }

        public void ResetRewardInterruptCount()
        {
            m_RewardInterruptCountTime = 0;
        }

        #endregion Reward Ads

        #region MRec Ads

        private AdsConfig MRecAdsConfig => GetAdsConfig(AdsType.MREC);
        private UnityAction m_MRecAdLoadedCallback;
        private UnityAction m_MRecAdLoadFailCallback;
        private UnityAction m_MRecAdClickedCallback;
        private UnityAction m_MRecAdExpandedCallback;
        private UnityAction m_MRecAdCollapsedCallback;
        private bool m_IsMRecShowing;

        private void SetupMRecAds(AdsMediationType adsMediationType)
        {
            if (IsRemoveAds) return;
            if (adsMediationType != m_SDKSetup.mrecAdsMediationType) return;
            SDKDebugLogger.Log("Setup MREC");
            MRecAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.MREC);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.MREC)) return;
            foreach (AdsMediationController t in MRecAdsConfig.adsMediations)
            {
                t.InitRMecAds(OnMRecAdLoadedEvent, OnMRecAdLoadFailedEvent, OnMRecAdClickedEvent, OnMRecAdExpandedEvent,
                    OnMRecAdCollapsedEvent);
            }

            SDKDebugLogger.Log("Setup MREC Done");
        }

        public bool IsMRecShowing()
        {
            return m_IsMRecShowing;
        }

        public bool IsMRecLoaded()
        {
            return GetSelectedMediation(AdsType.MREC) != null && GetSelectedMediation(AdsType.MREC).IsMRecLoaded();
        }

        private void OnMRecAdLoadedEvent()
        {
            m_MRecAdLoadedCallback?.Invoke();
        }

        private void OnMRecAdLoadFailedEvent()
        {
            m_MRecAdLoadFailCallback?.Invoke();
        }

        private void OnMRecAdClickedEvent()
        {
            m_MRecAdClickedCallback?.Invoke();
        }

        private void OnMRecAdExpandedEvent()
        {
            m_MRecAdExpandedCallback?.Invoke();
            m_IsMRecShowing = true;
        }

        private void OnMRecAdCollapsedEvent()
        {
            m_MRecAdCollapsedCallback?.Invoke();
            m_IsMRecShowing = false;
        }

        public void ShowMRecAds()
        {
            SDKDebugLogger.Log("Call Show MREC Ads 1");
            if (IsCheatAds || IsRemoveAds) return;
            if (!IsActiveMRECAds) return;
            if (!m_SDKSetup.IsActiveAdsType(AdsType.MREC)) return;
            SDKDebugLogger.Log("Call Show MREC Ads 2");
            GetSelectedMediation(AdsType.MREC)?.ShowMRecAds();
            HideBannerAds();
        }

        public void HideMRecAds()
        {
            SDKDebugLogger.Log("Call Hide MREC Ads");
            GetSelectedMediation(AdsType.MREC)?.HideMRecAds();
        }

        #endregion

        #region App Open Ads

        private AdsConfig AppOpenAdsConfig => GetAdsConfig(AdsType.APP_OPEN);

        private bool isActiveAppOpenAds = true;
        private bool isActiveShowAdsFirstTime = true;
        private bool isDoneShowAdsFirstTime = false;
        private double adsResumeCappingTime = 0;
        private double pauseTimeNeedToShowAds = 5;
        private DateTime m_CloseAdsTime = DateTime.Now;
        private DateTime m_StartPauseTime = DateTime.Now;
        private bool m_IsShowingAds;

        private bool IsShowingAds
        {
            get => m_IsShowingAds;
            set
            {
                m_IsShowingAds = value;
                SDKDebugLogger.Log("Set Showing Ads = " + value);
            }
        }

        private void SetupAppOpenAds(AdsMediationType adsMediationType)
        {
            if (IsCheatAds || IsRemoveAds) return;
            if (adsMediationType != m_SDKSetup.appOpenAdsMediationType) return;
            SDKDebugLogger.Log("Setup App Open Ads");
            AppOpenAdsConfig.isActive = m_SDKSetup.IsActiveAdsType(AdsType.APP_OPEN);
            if (!m_SDKSetup.IsActiveAdsType(AdsType.APP_OPEN)) return;
            foreach (AdsMediationController t in AppOpenAdsConfig.adsMediations)
            {
                t.InitAppOpenAds(OnAppOpenAdLoadedEvent, OnAppOpenAdLoadFailedEvent, OnAppOpenAdClosedEvent,
                    OnAppOpenAdDisplayedEvent, OnAppOpenAdFailedToDisplayEvent);
            }

            StartCoroutine(coCheckingShowAppOpenAds());
            SDKDebugLogger.Log("Setup App Open Ads Done");
        }

        private void ShowAppOpenAds()
        {
            if (IsCheatAds || IsRemoveAds) return;
            if (IsAppOpenAdsReady())
            {
                SDKDebugLogger.Log("Start Show App Open Ads");
                MarkShowingAds(true);
                GetSelectedMediation(AdsType.APP_OPEN).ShowAppOpenAds();
            }
        }

        private void DelayShowAppOpenAds()
        {
            StartCoroutine(coDelayShowAppOpenAds());
        }

        private IEnumerator coDelayShowAppOpenAds()
        {
            yield return new WaitForSeconds(0.3f);
            ShowAppOpenAds();
        }

        private void ForceShowAppOpenAds()
        {
            if (IsCheatAds || IsRemoveAds) return;
            if (IsAppOpenAdsLoaded())
            {
                MarkShowingAds(true);
                SDKDebugLogger.Log("Start Force Show App Open Ads");
                GetSelectedMediation(AdsType.APP_OPEN).ShowAppOpenAds();
            }
        }

        private void RequestAppOpenAds()
        {
            if (IsRemoveAds) return;
            GetSelectedMediation(AdsType.APP_OPEN).RequestAppOpenAds();
        }

        private bool IsAppOpenAdsReady()
        {
            if (GetSelectedMediation(AdsType.APP_OPEN) == null)
            {
                SDKDebugLogger.Log("App Open Mediation Null");
                return false;
            }

            return IsActiveAppOpenAds() && IsAppOpenAdsLoaded();
        }

        private bool IsActiveAppOpenAds()
        {
            if (!IsActiveResumeAdsRemoteConfig) return false;
            if (IsShowingAds) return false;
            float totalTimeBetweenShow = (float)(DateTime.Now - m_CloseAdsTime).TotalSeconds;
            SDKDebugLogger.Log("Total Time Between Show = " + totalTimeBetweenShow + " Need = " + adsResumeCappingTime);
            return !(totalTimeBetweenShow < adsResumeCappingTime);
        }

        private bool IsAppOpenAdsLoaded()
        {
            return GetSelectedMediation(AdsType.APP_OPEN) != null &&
                   GetSelectedMediation(AdsType.APP_OPEN).IsAppOpenAdsLoaded();
        }

        private void UpdateAOARemoteConfig()
        {
            // {
            //     ConfigValue configValue = ABIFirebaseManager.Instance.GetConfigValue(ABI.Keys.key_remote_aoa_active);
            //     isActiveAppOpenAds = configValue.BooleanValue;
            //     SDKDebugLogger.Log("App Open Ads Active = " + isActiveAppOpenAds);
            // }

            // {
            //     ConfigValue configValue =
            //         ABIFirebaseManager.Instance.GetConfigValue(ABI.Keys.key_remote_aoa_show_first_time_active);
            //     isActiveShowAdsFirstTime = configValue.BooleanValue;
            //     SDKDebugLogger.Log("AOA active show first time = " + isActiveShowAdsFirstTime);
            // }
        }

        private IEnumerator coCheckingShowAppOpenAds()
        {
#if UNITY_EDITOR
            yield break;
#endif
            float startCheckingTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < 10f)
            {

                if (isDoneShowAdsFirstTime) break;
                if (FirebaseRemoteConfigManager.IsReady)
                {
                    SDKDebugLogger.Log("Is Active App Open Ads = " + isActiveAppOpenAds + " Is First Open = " + IsFirstOpen +
                              " Is Active Show First Time = " + isActiveShowAdsFirstTime + " Is AOA Loaded = " +
                              IsAppOpenAdsLoaded());
                    if (!isActiveAppOpenAds || IsRemoveAds) break;
                    if (IsFirstOpen)
                    {
                        if (isActiveShowAdsFirstTime)
                        {
                            if (IsAppOpenAdsLoaded())
                            {
                                ShowAdsFirstTime();
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (IsAppOpenAdsLoaded())
                        {
                            ShowAdsFirstTime();
                            break;
                        }
                    }
                }

                yield return new WaitForSeconds(0.2f);
            }

            SDKDebugLogger.Log("AOA Done Checking --- Start Time = " + startCheckingTime + " End Time = " +
                      Time.realtimeSinceStartup);
        }

        private void ShowAdsFirstTime()
        {
            SDKDebugLogger.Log("-------------------Show Ads First Time-------------------");
            isDoneShowAdsFirstTime = true;
            ForceShowAppOpenAds();

        }

        private void MarkShowingAds(bool isShowing)
        {
            if (isShowing)
            {
                IsShowingAds = true;
            }
            else
            {
                EventManager.AddEventNextFrame(() => { StartCoroutine(coWaitingMarkShowingAdsDone()); });
            }
        }

        private IEnumerator coWaitingMarkShowingAdsDone()
        {
            yield return new WaitForSeconds(2f);
            IsShowingAds = false;
        }

        private void OnAppOpenAdLoadedEvent()
        {
            SDKDebugLogger.Log("AdsManager AOA Loaded");
        }

        private void OnAppOpenAdLoadFailedEvent()
        {
            SDKDebugLogger.Log("AdsManager AOA Load Fail");
        }

        private void OnAppOpenAdClosedEvent()
        {
            SDKDebugLogger.Log("AdsManager Closed app open ad");
            MarkShowingAds(false);
            m_CloseAdsTime = DateTime.Now;
            RequestAppOpenAds();
        }

        private void OnAppOpenAdDisplayedEvent()
        {
            SDKDebugLogger.Log("AdsManager Displayed app open ad");
            MarkShowingAds(true);
            ResetAdsInterstitialCappingTime();
        }

        private void OnAppOpenAdFailedToDisplayEvent()
        {
            SDKDebugLogger.Log("AdsManager Failed to display app open ad");
            MarkShowingAds(false);
        }

        #endregion

        #region Resume Ads

        public bool IsActiveResumeAdsIngame = false;
        private bool IsActiveResumeAdsRemoteConfig = false;

        private async void ShowResumeAds()
        {
            if (!IsActiveResumeAdsIngame) return;
            if (!IsActiveResumeAdsRemoteConfig) return;
            SDKDebugLogger.Log("Show Resume Ads");
            switch (resumeAdsType)
            {
                case AdsType.INTERSTITIAL:
                {
                    await ShowLoadingPanel();
                    if (IsReadyToShowInterstitial())
                    {
                        ShowInterstitial(
                            m_InterstitialPlacement,
                            CloseLoadingPanel,
                            CloseLoadingPanel,
                            CloseLoadingPanel);
                        return;
                    }
                    else
                    {
                        CloseLoadingPanel();
                        return;
                    }

                    break;
                }
                case AdsType.APP_OPEN:
                {
                    if (AppOpenAdsConfig.isActive)
                    {
                        DelayShowAppOpenAds();
                        return;
                    }

                    break;
                }
            }

            IsShowingAds = false;
        }

        private UniTask ShowLoadingPanel()
        {
            SDKDebugLogger.Log("Show Loading Panel");
            return UniTask.CompletedTask; // Replace with actual loading panel logic
        }

        private void CloseLoadingPanel()
        {
            SDKDebugLogger.Log("Close Loading Panel");
        }

        private void UpdateRemoteConfigResumeAds()
        {
            // {
            //     ConfigValue configValue =
            //         ABIFirebaseManager.Instance.GetConfigValue(ABI.Keys.key_remote_ads_resume_ads_active);
            //     IsActiveResumeAdsRemoteConfig = configValue.BooleanValue;
            //     SDKDebugLogger.Log("=============== Resume Ads Active = " + IsActiveResumeAdsRemoteConfig);
            // }
            // {
            //     bool value = ABIFirebaseManager.Instance.GetConfigBool(Keys.key_remote_resume_ads_type);
            //     resumeAdsType = value ? AdsType.APP_OPEN : AdsType.INTERSTITIAL;
            //     SDKDebugLogger.Log("=============== Resume Ads Type " + resumeAdsType);
            // }
            // {
            //     ConfigValue configValue =
            //         ABIFirebaseManager.Instance.GetConfigValue(ABI.Keys.key_remote_ads_resume_capping_time);
            //     adsResumeCappingTime = configValue.DoubleValue;
            //     SDKDebugLogger.Log("=============== Ads Resume Capping Time = " + adsResumeCappingTime);
            // }

            // {
            //     ConfigValue configValue =
            //         ABIFirebaseManager.Instance.GetConfigValue(ABI.Keys.key_remote_ads_resume_pause_time);
            //     pauseTimeNeedToShowAds = configValue.DoubleValue;
            //     SDKDebugLogger.Log("=============== Ads Resume Pause Time To Show Ads = " + pauseTimeNeedToShowAds);
            // }
        }

        #endregion

        private void OnAdRevenuePaidEvent(ImpressionData impressionData)
        {
            SDKDebugLogger.Log("Paid Ad Revenue - Ads Type = " + impressionData.ad_type);
            AnalyticsManager.TrackAdImpression(impressionData);
#if UNITY_APPSFLYER
            // AppsFlyerManager.TrackAppsflyerAdRevenue(impressionData);
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            SDKDebugLogger.Log("OnApplicationPause " + paused +
                      "Resume Ads Type = " + resumeAdsType +
                      " Is Showing Ads = " + IsShowingAds);
            switch (paused)
            {
                case true:
                    m_StartPauseTime = DateTime.Now;
                    break;
                case false when (DateTime.Now - m_StartPauseTime).TotalSeconds > pauseTimeNeedToShowAds:
                {
                    if (Time.realtimeSinceStartup > 30 && !IsShowingAds)
                    {
                        ShowResumeAds();
                    }
                    else
                    {
                        IsShowingAds = false;
                    }

                    break;
                }
            }
        }

        [System.Serializable]
        public class UUID
        {
            public string uuid;

            public static string Generate()
            {
                UUID newUuid = new UUID { uuid = System.Guid.NewGuid().ToString() };
                return newUuid.uuid;
            }
        }
    }
}
