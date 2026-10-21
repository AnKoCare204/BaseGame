using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.GamePlay;
using Core.Manager;
using Core.UI;
using Core.UI.Modals;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using Manager;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using R3;
using TW.UGUI.Core.Modals;

public class UIShopPack : MonoBehaviour
{
    [field: SerializeField] public PackageName PackageName { get; set; }
    [field: SerializeField] public TextMeshProUGUI PackageNameTxt { get; set; }
    [field: SerializeField] public List<UIItemInfo> UIItemInfos { get; set; } = new();
    [field: SerializeField] public ShopPackageDataConfig ShopPackageDataConfig { get; set; } = new();
    [field: SerializeField] public Button PurchaseBtn { get; set; }
    [field: SerializeField] public TextMeshProUGUI TxtPackPrice { get; set; }
    [field: SerializeField] public bool IsLoaded { get; private set; }
    [field: SerializeField] private ResourcePos placement { get; set; } = ResourcePos.shop;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private Image bgImage;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private List<Image> bgItem;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private Image mainItems;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private Sprite bgSprite;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private Sprite bgItemSprite;
    [BoxGroup("Editor Only")]
    [SerializeField]
    private Sprite mainItemsSprite;
    
    private Func<PackageName, UniTask> callbackBuyPack;
    private void Start()
    {
        Init();
        PurchaseBtn.SetOnClickDestination(OnPurchase);
        InGameDataManager.Instance.InGameData.SettingData.LanguageCode
            .Subscribe(OnLanguageChanged).AddTo(this);
    }

    public void Init()
    {
        InitUI();
    }
    
    public void SetActionBuyPack(Func<PackageName, UniTask> action)
    {
        callbackBuyPack = action;
    }

    [Button]
    public void InitDefault()
    {
        if (bgImage != null)
            bgImage.sprite = bgSprite;
        for (int i = 0; i < bgItem.Count; i++)
        {
            if (bgItem[i] != null)
            {
                bgItem[i].sprite = bgItemSprite;
            }
        }
        if (mainItems != null)
            mainItems.sprite = mainItemsSprite;
        ShopPackageDataConfig = ShopGlobalConfig.Instance.GetShopPackageDataConfig(PackageName);
        SetPackageName();
    }

    [Button]
    public void InitUI()
    {
        ShopPackageDataConfig = ShopGlobalConfig.Instance.GetShopPackageDataConfig(PackageName);
        if (ShopPackageDataConfig == null)
        {
            // Debug.LogError($"ShopPackageDataConfig Null {PackageName}");
            IsLoaded = false;
            gameObject.SetActive(false);
            return;
        }
        SetPackPrice();
        SetPackageName();
        int itemCount = 0;
        for (int i = 0; i < UIItemInfos.Count; i++)
        {
            if (i < ShopPackageDataConfig.GameResources.Count)
            {
                if (ShopPackageDataConfig.GameResources[i] != null
                    && ShopPackageDataConfig.GameResources[i].Value.Value > 0)
                {
                    UIItemInfos[i].Init(ShopPackageDataConfig.GameResources[i]);
                    UIItemInfos[i].gameObject.SetActive(true);
                }
                else
                {
                    UIItemInfos[i].gameObject.SetActive(false);
                }
            }
            else
            {
                UIItemInfos[i].gameObject.SetActive(false);
            }
        }
        IsLoaded = true;
    }

    private void SetPackPrice()
    {
        if(GameManager.Instance.BuildType == BuildType.Cheat)
        {
            TxtPackPrice.text = $"${ShopPackageDataConfig.price}";
            PurchaseBtn.interactable = true;
            return;
        }
        switch (ShopPackageDataConfig.priceType)
        {
            case PriceType.IAP:
                // Chưa gắn IAP: hiện giá cấu hình và khóa nút mua cho tới khi IAPEventManager được SDK nối.
                TxtPackPrice.text = $"{IAPController.Instance.GetIAPPackage(ShopPackageDataConfig.packageId).GetPrice()}";
                PurchaseBtn.interactable = IAPEventManager.OnPurchase != null;
                break;
            case PriceType.Money:
                TxtPackPrice.text = $"{ShopPackageDataConfig.price}";
                PurchaseBtn.interactable = true;
                break;
        }
    }

    private void OnPurchase()
    {
// #if CHEAT_ONLY
//         PurchaseSuccess();
//         return;
// #endif
        if (GameManager.Instance.BuildType == BuildType.Cheat)
        {
            PurchaseSuccess();
            return;
        }
        Debug.Log($"OnPurchase {ShopPackageDataConfig.packageName} - {ShopPackageDataConfig.priceType}");
        switch (ShopPackageDataConfig.priceType)
        {
            case PriceType.IAP:
                OnIAPPurchase();
                break;
            case PriceType.Money:
                OnMoneyPurchase();
                break;
        }
    }

    private void OnTestPurchase()
    {
        PurchaseSuccess();
    }

    private void OnIAPPurchase()
    {
        IAPEventManager.OnPurchase?.Invoke(ShopPackageDataConfig.packageId, PurchaseSuccess, null);
        Debug.Log($"OnIAPPurchase {ShopPackageDataConfig.packageId}");
        AnalyticsParameter[] parameters = new AnalyticsParameter[]
        {
            new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
            new AnalyticsParameter("placement", placement.ToString()),
            new AnalyticsParameter("pack_ID", ShopPackageDataConfig.packageId),
            new AnalyticsParameter("price", ShopPackageDataConfig.price.ToString()),
        };
        EventTrackingManager.TrackEventFirebase?.Invoke("iap_click", parameters);
    }

    private void OnAdsPurchase()
    {
        // InGameAdsController.EventShowAdsReward?.Invoke($"AdsRw_GetResource_{ShopPackageDataConfig.packageName}", OnAdsPurchaseSuccess, null);
    }

    private void OnAdsPurchaseSuccess()
    {
        // InGameDataManager.Instance.InGameData.ResourceDataSave.SetPackageCondition(ShopPackageDataConfig.packageName, DateTime.Now.AddMinutes(10).ToString(CultureInfo.InvariantCulture));
        //         adsWaitingTime = InGameDataManager.Instance.InGameData.ResourceDataSave.GetPackageCondion(ShopPackageDataConfig.packageName);
        //         CheckPurchaseByAds();
        //         PurchaseSuccess();
    }

    private void OnMoneyPurchase()
    {
        PurchaseSuccess();
    }

    public void PurchaseSuccess()
    {
        PurchaseSuccessAsync().Forget();
    }

    public async UniTask PurchaseSuccessAsync()
    {
        if(callbackBuyPack != null)
            await callbackBuyPack.Invoke(ShopPackageDataConfig.packageName);
        //ResourcePos placement = GameManager.Instance.GameState == GameState.MainMenu ? ResourcePos.home : ResourcePos.ingame;
        bool isFirstOrder = !ShopData.Instance.IsPackPurchased(ShopPackageDataConfig.packageName.ToString());
        string claimType = ShopPackageDataConfig.priceType switch
        {
            PriceType.IAP => ResourcePos.iap.ToString(),
            PriceType.Ads => ResourcePos.ads.ToString(),
            _ => ResourcePos.free.ToString(),
        };
        if (ShopPackageDataConfig.packageName == PackageName.fail_offer)
        {
            RewardManager.Instance.AddReward(ShopPackageDataConfig.GameResources, placement.ToString(), claimType);
            RewardManager.Instance.ShowReward(()=>
            {
                OnPurchaseFailOffer().Forget();
            });
        }
        else
        {
            List<GameResource> gameResources = ShopPackageDataConfig.GameResources.Where(re => re.Amount > 0).ToList();
            ClaimGameResourceGroup claimGameResourceGroup = new ClaimGameResourceGroup();
            claimGameResourceGroup.GameResourceList = gameResources;
            claimGameResourceGroup.ClaimType = claimType;
            claimGameResourceGroup.Placement = placement.ToString();
            
            RewardManager.Instance.AddResource(claimGameResourceGroup);
            RewardManager.Instance.ClaimResource(claimGameResourceGroup).Forget();
        }
        ShopData.Instance.AddPurchasedPack(ShopPackageDataConfig.packageName.ToString());
        GameGlobalEvent.OnPurchaseIapSuccess?.Invoke();
        
        AnalyticsParameter[] parameters = new AnalyticsParameter[]
        {
            new AnalyticsParameter("level", IngameFirebaseAnalystic.TrackedLevelId),
            new AnalyticsParameter("pack_ID", ShopPackageDataConfig.packageId),
            new AnalyticsParameter("placement", placement.ToString()),
            new AnalyticsParameter("isfirst", isFirstOrder ? "1" : "0"),
            new AnalyticsParameter("price", ShopPackageDataConfig.price),
        };
        EventTrackingManager.TrackEventFirebase?.Invoke("iap_purchases_confirmed", parameters);
        if(ShopPackageDataConfig.priceType == PriceType.IAP)
            UserData.Instance.IncreaseIAPCount();
    }

    private void OnLanguageChanged(string language)
    {
        SetPackageName();
    }

    private void SetPackageName()
    {
        if (PackageNameTxt != null)
        {
            PackageNameTxt.text = $"{ShopPackageDataConfig.packageNameToUI}";
        }
    }

    private async UniTask OnPurchaseFailOffer()
    {
        ModalContainer modalContainer = ModalContainer.Find(ContainerKey.Modals);
        await UniTask.WaitUntil(() => !modalContainer.IsInTransition, cancellationToken: this.GetCancellationTokenOnDestroy());
        while (modalContainer.Modals.Count > 0)
        {
            await modalContainer.PopAsync(true);
        }

        await LevelManager.Instance.CurrentLevel.Revive();
        //TimeIngameManager.Instance.ResetTimeDelayLose();
        GameManager.Instance.SetState(GameState.InGame);
        LevelManager.Instance.SetPause(false);
        ScreenInGameContext.Events.ShowBotPanel?.Invoke();
    }

    public async UniTask OnShown()
    {
        await UniTask.WaitUntil(() => (ShopPackageDataConfig != null) || !gameObject.activeInHierarchy, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (gameObject.activeInHierarchy) IngameFirebaseAnalystic.Instance.TrackIAPShow(ShopPackageDataConfig.packageId, placement.ToString());
    }
}