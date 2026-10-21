using System;
using Core;
using Core.Manager;
using CoreData;
using Cysharp.Threading.Tasks;
using Firebase.RemoteConfig;
// using Manager;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.Events;
#pragma warning disable CS0162 // Unreachable code detected

public class AdsController : Singleton<AdsController>
{
    public static Action EventShowBanner { get; set; }
    public static Action EventHideBanner { get; set; }
    
    [field: SerializeField] public float InterCapping {get; private set;}
    [field: SerializeField] public float InterCappingAfterReward {get; private set;}
    [field: SerializeField] public float InterCooldown {get; private set;}
    [field: SerializeField] public bool InterReady { get; private set; } = true;
    [field: SerializeField] public static bool InterJustShowed { get; set; } = false;
    private const string PrefKeyHasShownFirstInterEver = "ads_has_shown_first_inter_ever";
    private bool HasShownFirstInterEver
    {
        get => PlayerPrefs.GetInt(PrefKeyHasShownFirstInterEver, 0) == 1;
        set { PlayerPrefs.SetInt(PrefKeyHasShownFirstInterEver, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    private bool _firstInterAlreadyShownAtLaunch;
    private int _interSkippedThisReopen;

    protected override void Awake()
    {
        base.Awake();
        AddEvent();
        Initialize();
    }
    private void OnDestroy()
    {
        RemoveEvent();
    }

    public void Initialize()
    {
        // Chốt trạng thái đầu lần mở game: nếu inter đầu tiên đã show từ lần mở trước
        // thì lần mở này sẽ áp dụng skip-first-N.
        _firstInterAlreadyShownAtLaunch = HasShownFirstInterEver;
        _interSkippedThisReopen = 0;

        EventAdsManager.ShowInterstitialAds += ShowAdsInter;
        EventAdsManager.ShowRewardAds += ShowAdsReward;
        EventAdsManager.ShowBannerAds += ShowBanner;
    }

    public void GetRemoteConfig()
    {
        RefreshInterCapping();
        InterCappingAfterReward = (float)FirebaseManager.Instance.GetConfigValue(Keys.key_inter_capping_after_reward).DoubleValue;
    }

    private void RefreshInterCapping()
    {
        InterCapping = InterAdsCappingRemoteConfigData.Instance.GetInterCapping(GameManager.Instance.Level.Value);
    }

    private void ClearEvent()
    {
        EventShowBanner = null;
        EventHideBanner = null;
    }
    
    public void AddEvent()
    {
        ClearEvent();
        EventShowBanner += ShowBanner;
        EventHideBanner += HideBanner;
    }
    public void RemoveEvent()
    {
        EventShowBanner -= ShowBanner;
        EventHideBanner -= HideBanner;
    }

    public bool CheckCanShowAds()
    {
        return true;
    }

    public void ShowBanner(bool isShow)
    {
        if (isShow)
        {
            ShowBanner();
        }
        else
        {
            HideBanner();
        }
    }
    
    [Button]
    public void ShowAdsReward(Action successAction, Action failAction, Action closeAction, bool isSkipCapping = false, string eventName = "", params AnalyticsParameter[] parameters)
    {
        RefreshInterCapping();
        AppsFlyerManager.TrackRewarded_ClickShowButton();
        EventTrackingManager.TrackEventFirebase?.Invoke("ads_reward_clicked", parameters);
        AdsManager.Instance.ShowRewardVideo(
        () =>
            {
                successAction?.Invoke();
                InterCooldown += InterCappingAfterReward;
                InterCooldown = Mathf.Clamp(InterCooldown, 0, InterCapping);
                TrackLevelAdsView();
                EventTrackingManager.TrackEventFirebase?.Invoke(eventName, parameters);
                AppsFlyerManager.TrackRewarded_Displayed();
            },
            () =>
            {
                closeAction?.Invoke();
            },
            () =>
            {
                FailToShowdAds(failAction);
                // Chỉ báo cho reward (người chơi chủ động bấm xem); inter tự bật nên fail thì im lặng.
                NetworkNotice.ShowAdsUnavailable();
            }
        );
    }

    private void FailToShowdAds(Action failAction = null)
    {
        failAction?.Invoke();

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("Fail to show reward ads because of no internet connection");
            // ActivityBlockContext.Events.CallNotification?.Invoke(528);
        }
        else
        {
            Debug.Log("Fail to show reward ads because of other reason");
            // ActivityBlockContext.Events.CallNotification?.Invoke(535);
        }
    }
    
    
    [Button]
    public void ShowAdsInter(Action successAction, Action failAction, Action closeAction, Action skipAction, bool isSkipCapping = false, bool isBackfillInter = false, string eventName = "", params AnalyticsParameter[] parameters)
    {
        RefreshInterCapping();
        if (ShopData.Instance.IsRemoveADs.Value && !isBackfillInter)
        {
            successAction?.Invoke();
            skipAction?.Invoke();
            return;
        }

        if (!InterReady && !isSkipCapping)
        {
            successAction?.Invoke();
            skipAction?.Invoke();
            return;
        }

        // Skip N lần show inter đầu mỗi khi mở lại game (chỉ áp dụng cho inter capping thường,
        // không áp cho backfill/skip-capping vốn là show ép). Lần inter đầu tiên (ever) không bao giờ bị skip.
        if (!isSkipCapping && !isBackfillInter && _firstInterAlreadyShownAtLaunch)
        {
            int skipFirstCount = InterAdsCappingRemoteConfigData.Instance
                .GetSkipFirstInterCountOnReopen(GameManager.Instance.Level.Value);
            if (_interSkippedThisReopen < skipFirstCount)
            {
                _interSkippedThisReopen++;
                // Bỏ qua lần này, không set cooldown để lần eligible kế tiếp vẫn được xét.
                successAction?.Invoke();
                skipAction?.Invoke();
                return;
            }
        }

        AppsFlyerManager.TrackInterstitial_ClickShowButton();
        AdsManager.Instance.ShowInterstitial(
            () =>
            {
                InterCooldown = InterCapping;
                if (!HasShownFirstInterEver) HasShownFirstInterEver = true;
                successAction?.Invoke();
                InterCooldown = Mathf.Clamp(InterCooldown, 0, InterCapping);
                TrackLevelAdsView();
                IngameFirebaseAnalystic.MarkInterstitialShown();
                EventTrackingManager.TrackEventFirebase?.Invoke(eventName, parameters);
                AppsFlyerManager.TrackInterstitial_Displayed();
            },
            () =>
            {
                closeAction?.Invoke();
            },
            () =>
            {
                FailToShowdAds(failAction);
            }
        );
        InterCooldown = InterCapping;
        InterJustShowed = true;
    }
    [Button]
    public void ShowBanner()
    {
        // Cùng quy tắc với ScreenInGame.LoadLevel: config ingame_banner_config bật và chưa mua Remove Ads.
        if (!BannerRemoteConfigData.Instance.CanShow(ShopData.Instance.IsRemoveADs.Value))
        {
            return;
        }
        AdsManager.Instance.ShowBannerAds();
    }
    [Button]
    public void HideBanner()
    {
        AdsManager.Instance.HideBannerAds();
    }

    private void Update()
    {
        if (InterCooldown > 0)
        {
            InterCooldown -= Time.deltaTime;
        }   
        InterReady = InterCooldown <= 0;
    }

    private static void TrackLevelAdsView()
    {
        if (LevelManager.Instance == null || !LevelManager.Instance.IsTrackingLevelSession) return;
        LevelManager.Instance.AddLevelAdsCount();
    }
}
