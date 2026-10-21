using System;
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
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalLeaveGame : Modal
    {
        [field: SerializeField] public ModalLeaveGameContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalLeaveGameContext
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

            public UniTask Initialize(Memory<object> args)
            {
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
            [field: SerializeField] public Button ButtonLeave {get; set;}
            [field: SerializeField] public Button ButtonClose {get; set;}
            [field: SerializeField] public Button ButtonStay {get; set;}
            [field: SerializeField] public GameObject GONormalDes { get; private set; }
            [field: SerializeField] public GameObject GoEventDes { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
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
                View.ButtonLeave.SetOnClickDestination(OnButtonLeaveClick);
                View.ButtonClose.SetOnClickDestination(OnButtonCloseClick);
                View.ButtonStay.SetOnClickDestination(OnButtonCloseClick);
                ShowWarning = false;
            }

            private async UniTask OnButtonLeaveClick()
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
                View.MainView.interactable = false;
                AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                    new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                    new AnalyticsParameter("placement", ResourcePos.quit_level.ToString()),
                };
                
                // EventTracking.Instance.LogLevelFail("quitgame");
                int playTime = LevelManager.Instance.GetPlayTimeToNow();
                int retryCount = LevelManager.Instance.CurrentRetryCount;
                int reviveCount = LevelManager.Instance.CurrentReviveCount;
                IngameFirebaseAnalystic.Instance.TrackLevelFail(playTime,retryCount, "quit" , reviveCount);
                
                if (GameManager.Instance.CanShowAdsInter())
                {
                    EventAdsManager.ShowInterstitialAds?.Invoke(null, null, ShowAdsInterSuccess, null, false, false, "ads_inter_show",
                        parameters);
                }

                
                await GameManager.Instance.FailedLevel();

                // ADsManager.ShowInterstitial(ResourcePos.quit_level);
                // ADSAction.OnAdsInterEvent?.Invoke(ResourcePos.quit_level);
                
            }
            
            private void ShowAdsInterSuccess()
            {
                UserDataManager.Instance.IncreaseCountAdsInterstitialInDay();
                NoAdsPopupManager.MarkInterstitialWatched();
            }

            public async UniTask BackToMainMenu()
            {
                await GameManager.Instance.TryBackToMainMenu();
            }
            private async UniTask OnButtonCloseClick()
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            public void DidPushEnter(Memory<object> args)
            {
                Debug.Log("DidPushEnter");
                LevelManager.Instance.SetPause(true);
            }
            public void DidPopEnter(Memory<object> args)
            {
                Debug.Log("DidPopEnter");
            }

            public void DidPushExit(Memory<object> args)
            {
                Debug.Log("DidPushExit");
            }
            public void DidPopExit(Memory<object> args)
            {
                Debug.Log("DidPopExit");
                // Bị WinLevel / PrepareWin đóng (thắng đã chắc) thì giữ pause, chỉ unpause khi còn đang chơi (như ModalPause.OnButtonCloseClick).
                if (GameManager.Instance.IsState(GameState.InGame)) LevelManager.Instance.SetPause(false);
            }
        }
    }
}