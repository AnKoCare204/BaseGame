using System;
using Core.GamePlay.GlobalEnum;
using Core.Manager;
using Core.UI.Activities;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using UnityEngine.UI;
using LitMotion;

namespace Core.UI.Modals
{
    public class ModalLose : Modal
    {
        [field: SerializeField] public ModalLoseContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalLoseContext
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
            [field: SerializeField] public int ReviveCoin { get; private set; }
            [field: SerializeField] public bool HasReviveByAds { get; private set; }
            [field: SerializeField] public int ReviveByAdsCount { get; private set; }
            [field: SerializeField] public int ReviveByCoinCount { get; private set; }
            
            public UniTask Initialize(Memory<object> args)
            {
                ReviveByAdsCount = LevelManager.Instance.CurrentLevel.ReviveByAdsCount;
                ReviveByCoinCount = LevelManager.Instance.CurrentLevel.ReviveByCoinCount;
                
                ReviveCoin = ReviveRemoteConfigData.Instance.GetCurrentReviveCoin(ReviveByCoinCount);
                HasReviveByAds = ReviveRemoteConfigData.Instance.GetCurrentReviveAds(ReviveByAdsCount) == 1;
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
            [field: SerializeField] public Button ButtonKeepPlaying { get; private set; }
            // [field: SerializeField] public Button ButtonAds { get; private set; }
            [field: SerializeField] public Button ButtonClose { get; private set; }
            [field: SerializeField] public TextMeshProUGUI reviveCoinTxt { get; private set; }
            [field: SerializeField] public HoldToPreviewButton holdToPreviewButton { get; private set; }
            [field: SerializeField] public UIShopPack IAPPack { get; private set; }
            
            [field: SerializeField] public GameObject GONormalDes { get; private set; }
            [field: SerializeField] public GameObject GoEventDes { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
            public void SetReviveCoin(int reviveCoin)
            {
                reviveCoinTxt.text = $"{reviveCoin}";
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            [field: SerializeField] public LoseEventPriority LoseEventPriority { get; private set; }
            private bool ShowWarning { get; set; }

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.SetReviveCoin(Model.ReviveCoin);
                View.ButtonKeepPlaying.SetOnClickDestination(OnClickButtonKeepPlaying);
                // View.ButtonAds.SetOnClickDestination(OnClickButtonAds);
                View.ButtonClose.SetOnClickDestination(OnClickButtonClose);
                
                AudioManager.Instance.PlaySoundFx(AudioKey.SfxLose);

                View.holdToPreviewButton.OnHoldStart = HideUI;
                View.holdToPreviewButton.OnHoldEnd = ShowUI;

                View.IAPPack.OnShown();

                // View.ButtonAds.gameObject.SetActive(Model.HasReviveByAds);
                ShowWarning = false;
            }
            private async UniTask OnClickButtonKeepPlaying()
            {
                bool isEnough = InGameDataManager.Instance.InGameData.ResourceData.IsEnoughResourceValue(ResourceType.Money, Model.ReviveCoin);
                if (!isEnough)
                {
                    ViewOptions viewOptions = new ViewOptions(nameof(ModalShop));
                    await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
                }
                else
                {
                    IngameFirebaseAnalystic.Instance.TrackReviveOfferResult("coin");
                    PlayerResourceManager.Instance.SubResourceValue(ResourceType.Money, Model.ReviveCoin, ResourcePos.ingame.ToString(), ResourcePos.revive.ToString());
                    LevelManager.Instance.CurrentLevel.ReviveByCoinCount++;
                    await ContinueSuccess("coin");
                }
            }
            
            private async UniTask ContinueSuccess(string claimType)
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                await LevelManager.Instance.CurrentLevel.Revive();
                IngameFirebaseAnalystic.Instance.TrackBoosterEarn(ResourcePos.revive.ToString(),"AddSlot_revive",1);
                IngameFirebaseAnalystic.Instance.TrackBoosterSpeend(ResourcePos.revive.ToString(),"AddSlot_revive");
                GameManager.Instance.SetState(GameState.InGame);
                LevelManager.Instance.SetPause(false);
                ScreenInGameContext.Events.ShowBotPanel?.Invoke();
                // GameGlobalEvent.CheckWinCondition?.Invoke(0);
            }

            private async UniTask OnClickButtonClose()
            {
                if (!ShowWarning)
                {
                    ShowWarning = true;
                    bool canShowWarning = LoseEventPriority.IsAnyEventCanWarning();
                    if (canShowWarning)
                    {
                        View.GONormalDes.SetActive(false);
                        View.GoEventDes.SetActive(true);
                        return;
                    }
                }
                IngameFirebaseAnalystic.Instance.TrackReviveOfferResult("lose");
                await GameManager.Instance.FailedLevel();
            }

            // void InitShopPack()
            // {
            //     for (var i = 0; i < View.UiShopPacks.Count; i++)
            //     {
            //         View.UiShopPacks[i].Init(() => ContinueSuccess().Forget(), false);
            //     }
            // }

            [Button]
            private async UniTask HideUI()
            {
                var container = ModalContainer.Find(ContainerKey.Modals);
                var backdrop = container.Backdrops[^1].View.GetComponent<CanvasGroup>(); 
                await LMotion.Create(1f, 0f, 0.3f)
                    .WithEase(Ease.OutQuad)
                    .Bind(t =>
                    {
                        View.MainView.alpha = t;
                        backdrop.alpha = t;


                    }).AddTo(View.MainView);
            }

            private async UniTask ShowUI()
            {
                var container = ModalContainer.Find(ContainerKey.Modals);
                var backdrop = container.Backdrops[^1].View.GetComponent<CanvasGroup>();
                await LMotion.Create(0f, 1f, 0.3f)
                    .WithEase(Ease.OutQuad)
                    .Bind(t =>
                    {
                        View.MainView.alpha = t;
                        backdrop.alpha = t;


                    }).AddTo(View.MainView);
            }

            public UniTask Cleanup(Memory<object> args)
            {
                View.holdToPreviewButton.OnHoldStart = null;
                View.holdToPreviewButton.OnHoldEnd = null;
                return UniTask.CompletedTask;
            }
        }
    }
}