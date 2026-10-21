using System;
using Core.Manager;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using Manager;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    /// <summary>
    /// Modal mở thêm bể, hiện khi bấm dấu + trên ô bể khóa (BoosterManager.OnLockedTankTapped). Khác ModalBooster: không mua x3
    /// vào kho, chỉ hai lựa chọn mở ngay một ô bằng ads (Free) hoặc bằng gold (BoosterInfoData.Price của UnlockTank trong
    /// ItemGlobalConfig). Gold chỉ trừ sau khi bể mở thành công (BoosterManager.UnlockTankPaidAsync).
    /// </summary>
    public class ModalUnlockTank : Modal
    {
        [field: SerializeField] public ModalUnlockTankContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }
    }

    [Serializable]
    public class ModalUnlockTankContext
    {
        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField] public BoosterInfoData BoosterData { get; private set; }
            [field: SerializeField] public int Price { get; private set; }
            [field: SerializeField] public bool HasAds { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                BoosterData = ItemGlobalConfig.Instance.GetBoosterData(ResourceType.UnlockTank);
                Price = BoosterData != null ? BoosterData.Price : 0;
                Level level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
                int adsCount = level != null ? level.GetBoosterByAdsCount(ResourceType.UnlockTank) : 0;
                HasAds = BoosterRemoteConfigData.Instance.HasAdsForBooster(ResourceType.UnlockTank, adsCount);
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public TextMeshProUGUI NameTxt { get; private set; }
            [field: SerializeField] public TextMeshProUGUI DesTxt { get; private set; }
            [field: SerializeField] public TextMeshProUGUI PriceTxt { get; private set; }
            [field: SerializeField] public Image Icon { get; private set; }
            [field: SerializeField] public Button CoinBtn { get; private set; }
            [field: SerializeField] public Button AdsBtn { get; private set; }
            [field: SerializeField] public Button CloseBtn { get; private set; }

            public UniTask Initialize(Memory<object> args) => UniTask.CompletedTask;

            public void Bind(BoosterInfoData data, int price, bool hasAds)
            {
                if (data != null)
                {
                    if (NameTxt != null) NameTxt.text = data.Name;
                    if (DesTxt != null) DesTxt.text = data.Description;
                    if (Icon != null && data.Sprite != null) Icon.sprite = data.Sprite;
                }

                if (PriceTxt != null) PriceTxt.text = $"{price}";
                if (AdsBtn != null) AdsBtn.gameObject.SetActive(DefaultGlobalConfig.Instance.IsUseADS && hasAds);
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();

            private bool isResolving;

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.Bind(Model.BoosterData, Model.Price, Model.HasAds);
                View.CloseBtn.SetOnClickDestination(OnButtonCloseClick);
                View.CoinBtn.SetOnClickDestination(OnCoinClick);
                View.AdsBtn.SetOnClickDestination(OnAdsClick);
                LevelManager.Instance.SetPause(true);
            }

            /// <summary>Gold: thiếu thì mở shop; đủ thì đóng modal, mở bể rồi mới trừ gold (spend pos booster_unlocktank).</summary>
            private async UniTask OnCoinClick()
            {
                if (isResolving) return;
                if (!InGameDataManager.Instance.InGameData.ResourceData.IsEnoughResourceValue(ResourceType.Money, Model.Price))
                {
                    ViewOptions viewOptions = new ViewOptions(nameof(ModalShop));
                    await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
                    return;
                }

                await CloseAndUnlockAsync(true);
            }

            private void OnAdsClick()
            {
                if (isResolving) return;
                if (GameManager.Instance.BuildType == BuildType.Cheat)
                {
                    OnAdsSuccess();
                    return;
                }

                View.MainView.interactable = false;
                AnalyticsParameter[] parameters =
                {
                    new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                    new AnalyticsParameter("button_name", "unlock_tank"),
                    new AnalyticsParameter("reward_name", Model.BoosterData != null ? Model.BoosterData.Name : "Unlock Tank"),
                    new AnalyticsParameter("reward_type", "tank"),
                    new AnalyticsParameter("value", 1),
                    new AnalyticsParameter("placement", ResourcePos.ingame.ToString()),
                };
                EventAdsManager.ShowRewardAds?.Invoke(OnAdsSuccess, OnAdsFail, OnAdsFail, true, "ads_reward_complete", parameters);
            }

            private void OnAdsSuccess()
            {
                // Callback reward ads chạy sau khi ads đóng — level có thể đã bị hủy trong lúc xem ads.
                Level level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
                if (level == null || View.MainView == null) return;
                level.IncrementBoosterByAdsCount(ResourceType.UnlockTank);
                CloseAndUnlockAsync(false).Forget();
            }

            private void OnAdsFail()
            {
                if (View.MainView != null) View.MainView.interactable = true;
            }

            private async UniTask OnButtonCloseClick()
            {
                if (isResolving) return;
                if (GameManager.Instance.IsState(GameState.InGame)) LevelManager.Instance.SetPause(false);
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            /// <summary>
            /// Đóng modal (vẫn giữ pause để người chơi không bấm cá), chờ bàn hết bận rồi mở bể; mở được thì trừ gold (nếu trả gold),
            /// không mở được (hết đơn) thì báo MessageNoUse và không trừ gì. Xong mới bỏ pause.
            /// </summary>
            private async UniTask CloseAndUnlockAsync(bool payByCoin)
            {
                isResolving = true;
                ModalContainer modals = ModalContainer.Find(ContainerKey.Modals);
                if (modals != null)
                {
                    await UniTask.WaitWhile(() => modals != null && modals.IsInTransition);   // đang chạy anim mở thì PopAsync bị bỏ qua
                    await modals.PopAsync(true);
                }
                bool unlocked = BoosterManager.Instance != null && await BoosterManager.Instance.UnlockTankPaidAsync();
                if (unlocked)
                {
                    if (payByCoin)
                    {
                        PlayerResourceManager.Instance.SubResourceValue(ResourceType.Money, Model.Price, ResourcePos.ingame.ToString(), ResourcePos.booster_unlocktank.ToString());
                    }
                }
                else if (Model.BoosterData != null && !string.IsNullOrEmpty(Model.BoosterData.MessageNoUse))
                {
                    ScreenInGameContext.Events.ShowNotification?.Invoke(Model.BoosterData.MessageNoUse);
                }

                if (GameManager.Instance != null && GameManager.Instance.IsState(GameState.InGame) && LevelManager.Instance != null) LevelManager.Instance.SetPause(false);
            }
        }
    }
}
