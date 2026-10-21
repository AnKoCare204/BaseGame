using System;
using Core.Manager;
using CoreData;
using Cysharp.Threading.Tasks;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalBooster : Modal
    {
        [field: SerializeField] public ModalBoosterContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }

    [Serializable]
    public class ModalBoosterContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }
            [field: SerializeField] public BoosterSelector BoosterSelector { get; private set; }
            [field: SerializeField] public BoosterInfoData BoosterData { get; private set; }
            [field: SerializeField] public bool HasBoosterByAds { get; private set; }
            [field: SerializeField] public int BoosterPriceX3 { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                if (args.Length > 0)
                {
                    BoosterSelector = (BoosterSelector)args.Span[0];
                    BoosterData = ItemGlobalConfig.Instance.GetBoosterData(BoosterSelector.BoosterType);
                    BoosterPriceX3 = ItemGlobalConfig.Instance.GetBoosterPriceX3(BoosterSelector.BoosterType);
                    int adsCount = LevelManager.Instance.CurrentLevel.GetBoosterByAdsCount(BoosterSelector.BoosterType);
                    HasBoosterByAds = BoosterRemoteConfigData.Instance.HasAdsForBooster(BoosterSelector.BoosterType, adsCount);
                }
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField]
            public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public TextMeshProUGUI BoosterNameTxt { get; private set; }
            [field: SerializeField] public TextMeshProUGUI BoosterDesTxt { get; private set; }
            [field: SerializeField] public TextMeshProUGUI BoosterPriceTxt { get; private set; }
            [field: SerializeField] public Image BoosterIcon { get; private set; }
            [field: SerializeField] public Button CoinBuyBoosterBtn { get; private set; }
            [field: SerializeField] public Button AdsBuyBoosterBtn { get; private set; }
            [field: SerializeField] public Button ExitBtn1 { get; private set; }
            [field: SerializeField] public Button ExitBtn2 { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                if (args.Length > 0)
                {
                    BoosterSelector boosterSelector = (BoosterSelector)args.Span[0];
                    BoosterInfoData boosterData = ItemGlobalConfig.Instance.GetBoosterData(boosterSelector.BoosterType);
                    BoosterNameTxt.text = boosterData.Name;
                    BoosterDesTxt.text = boosterData.Description;
                    BoosterIcon.sprite = boosterData.Sprite;
                    //CoinBuyBoosterBtn.interactable = InGameDataManager.Instance.InGameData.ResourceData.IsEnoughResourceValue(ResourceType.Currency, (int)(CurrencyType.Money), boosterData.Price);
                    // Debug.Log(boosterData.RwLimit > GameplaySaveData.Instance.GetNumbBoosterRwUsed(boosterSelector.BoosterType));
                }
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.BoosterPriceTxt.text = $"{Model.BoosterPriceX3}";
                View.ExitBtn1.SetOnClickDestination(OnButtonCloseClick);
                // View.ExitBtn2.SetOnClickDestination(OnButtonCloseClick);
                View.CoinBuyBoosterBtn.SetOnClickDestination(OnButtonBuyBoosterClick);
                View.AdsBuyBoosterBtn.SetOnClickDestination(OnAdsButtonBuyBoosterClick);
                View.AdsBuyBoosterBtn.gameObject.SetActive(DefaultGlobalConfig.Instance.IsUseADS && Model.HasBoosterByAds);
                LevelManager.Instance.SetPause(true);
            }

            private async UniTask OnButtonBuyBoosterClick()
            {
                int price = Model.BoosterPriceX3;
                bool isEnough = InGameDataManager.Instance.InGameData.ResourceData.IsEnoughResourceValue(ResourceType.Money, price);
                if (!isEnough)
                {
                    //LevelManager.Instance.SetPause(true);
                    ViewOptions viewOptions = new ViewOptions(nameof(ModalShop));
                    await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
                }
                else
                {
                    PlayerResourceManager.Instance.AddResourceValue(Model.BoosterSelector.BoosterType, 3, ResourcePos.ingame.ToString(), ResourcePos.coin.ToString());
                    ResourcePos spendType = Model.BoosterSelector.BoosterType.ToSpendPos();
                    PlayerResourceManager.Instance.SubResourceValue(ResourceType.Money, price, ResourcePos.ingame.ToString(), spendType.ToString());
                    await CloseAndUseBooster();
                }
            }

            private async UniTask OnAdsButtonBuyBoosterClick()
            {
                if (GameManager.Instance.BuildType == BuildType.Cheat)
                {
                    await OnAdsButtonBuyBoosterSuccess();
                    return;
                }

                View.MainView.interactable = false;
                // ADsManager.ShowRewarded(ResourcePos.ingame, Model.BoosterSelector.BoosterType.ToSpendPos(),
                //     () => OnAdsButtonBuyBoosterSuccess().Forget(), OnAdsButtonBuyBoosterFail);

                // ADSAction.OnAdsRewardEvent?.Invoke(ResourcePos.ingame, ResourcePos.booster_addslot + (int)(Model.BoosterSelector.BoosterType - ResourceType.AddSlot), () => OnAdsButtonBuyBoosterSuccess().Forget(), OnAdsButtonBuyBoosterFail);

                AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                    new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                    new AnalyticsParameter("button_name", "buy_booster"),
                    new AnalyticsParameter("reward_name", Model.BoosterData.Name),
                    new AnalyticsParameter("reward_type", "booster"),
                    new AnalyticsParameter("value", 3),
                    new AnalyticsParameter("placement", ResourcePos.ingame.ToString()),
                };

                EventAdsManager.ShowRewardAds?.Invoke(() => OnAdsButtonBuyBoosterSuccess().Forget(), OnAdsButtonBuyBoosterFail, OnAdsButtonBuyBoosterFail, true, "ads_reward_complete", parameters);
                
                
                // InGameAdsController.EventShowAdsReward?.Invoke($"ingame", $"ingame_booster_{Model.BoosterData.BoosterType}", () => OnAdsButtonBuyBoosterSuccess().Forget(), OnAdsButtonBuyBoosterFail);
                // await OnAdsButtonBuyBoosterSuccess();
            }

            private async UniTask OnAdsButtonBuyBoosterSuccess()
            {
                // Callback reward ads chạy sau khi ads đóng (delay 1 frame) — level/booster selector có thể đã bị destroy trong lúc xem ads
                var currentLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
                if (currentLevel == null || Model.BoosterSelector == null) return;
                currentLevel.IncrementBoosterByAdsCount(Model.BoosterSelector.BoosterType);
                PlayerResourceManager.Instance.AddResourceValue(Model.BoosterSelector.BoosterType, 1, ResourcePos.ingame.ToString(), ResourcePos.ads.ToString());
                await CloseAndUseBooster();
            }

            private void OnAdsButtonBuyBoosterFail()
            {
                Debug.Log("OnAdsButtonBuyBoosterFail");
                View.MainView.interactable = true;
            }

            private async UniTask OnButtonCloseClick()
            {
                if (GameManager.Instance.IsState(GameState.InGame))
                {
                    LevelManager.Instance.SetPause(false);
                }
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            private async UniTask CloseAndUseBooster()
            {
                await OnButtonCloseClick();
                await Model.BoosterSelector.SelectBooster("modal_booster");
            }
        }
    }
}
