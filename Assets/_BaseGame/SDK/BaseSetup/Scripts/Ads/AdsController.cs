using System;
using Cysharp.Threading.Tasks;
using Firebase.RemoteConfig;
// using Manager;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.Events;
#pragma warning disable CS0162 // Unreachable code detected

public class AdsController : Singleton<AdsController>
{
    public static Action<string, Action, Action, Action, bool, bool> EventShowAdsInter { get; set; }
    public static Action<string, Action, Action, Action> EventShowAdsReward { get; set; }
    public static Action EventShowBanner { get; set; }
    public static Action EventHideBanner { get; set; }
    public static bool IsRemoveAds {get; private set;}
    
    [field: SerializeField] public float InterCapping {get; private set;}
    [field: SerializeField] public float InterCappingAfterReward {get; private set;}
    [field: SerializeField] public float InterCooldown {get; private set;}
    [field: SerializeField] public bool InterReady { get; private set; } = true;
    [field: SerializeField] public static bool InterJustShowed { get; set; } = false;

    protected override void Awake()
    {
        base.Awake();
        AddEvent();
    }
    private void OnDestroy()
    {
        RemoveEvent();
    }
    public void Initialize()
    {
        // RemoveAds = InGameDataManager.Instance.InGameData.ResourceDataSave.GetReactiveValue(SpecialResourceType.NoAds);
        // RemoveAds.ReactiveProperty.Subscribe(CheckRemoveAds).AddTo(this);
        // if(GameManager.Instance.BuildType == BuildType.Cheat)
        // {
        //     RemoveAds.Value = 1;
        //     CheckRemoveAds(RemoveAds.Value);
        // }
    }


    private void ClearEvent()
    {
        EventShowAdsInter = null;
        EventShowAdsReward = null;
        EventShowBanner = null;
        EventHideBanner = null;
    }
    
    public void AddEvent()
    {
        ClearEvent();
        EventShowAdsInter += ShowAdsInter;
        EventShowAdsReward += ShowAdsReward;
        EventShowBanner += ShowBanner;
        EventHideBanner += HideBanner;
    }
    public void RemoveEvent()
    {
        EventShowAdsInter -= ShowAdsInter;
        EventShowAdsReward -= ShowAdsReward;
        EventShowBanner -= ShowBanner;
        EventHideBanner -= HideBanner;
    }

    private void CheckRemoveAds(float value)
    {
        IsRemoveAds = value > 0;
        if(IsRemoveAds)
            HideBanner();
    }

    public bool CheckCanShowAds()
    {
        return true;
    }
    
    [Button]
    public void ShowAdsReward(string placementId, Action callback, Action closeAction, Action failAction)
    {
        SDKDebugLogger.Log($"ShowAdsReward: {placementId}");
        // if (GameManager.Instance.BuildType == BuildType.Cheat)
        // {
        //     callback?.Invoke();
        // }
        // else
        // {
            AdsManager.Instance.ShowRewardVideo(placementId,
            () =>
                {
                    callback?.Invoke();
                    InterCooldown += InterCappingAfterReward;
                    InterCooldown = Mathf.Clamp(InterCooldown, 0, InterCapping);
                },
                (bool close) =>
                {
                    closeAction?.Invoke();
                },
                () =>
                {
                    FailToShowRewardAds(failAction);
                }
            );
        // }
    }

    private void FailToShowRewardAds(Action failAction = null)
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
    public void ShowAdsInter(string placementId, Action callback, Action closeAction, Action failAction, bool isSkipCapping = false, bool isBackfillInter = false)
    {
        SDKDebugLogger.Log($"ShowAdsInter: {placementId}");
        // if (GameManager.Instance.Level.Value < InterStartShowLevel || (!InterReady && !isSkipCapping))
        //     return;
        if(!CheckCanShowAds())
            return;
        // if ((IsRemoveAds && !isBackfillInter) || GameManager.Instance.BuildType == BuildType.Cheat)
        // {
        //     callback?.Invoke();
        // }
        // else
        {
            AdsManager.Instance.ShowInterstitial(placementId, callback, closeAction, failAction, true, isSkipCapping);
            InterCooldown = InterCapping;
            InterJustShowed = true;
        }
    }
    [Button]
    public void ShowBanner()
    {
        // if (!IsRemoveAds /*&& GameManager.Instance.ShowBanner*/
        //     && GameManager.Instance.Level.Value >= BannerStartShowLevel)
        // {
#if CHEAT_ONLY
            AdsManager.Instance.ShowBannerAds();
            return;
#endif
            AdsManager.Instance.ShowBannerAds();
        // }
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
}
