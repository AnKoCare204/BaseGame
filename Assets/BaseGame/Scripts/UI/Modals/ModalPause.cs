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
    public class ModalPause : Modal
    {
        [field: SerializeField] public ModalPauseContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalPauseContext
    {
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
            [field: SerializeField] public Button ButtonRetry {get; set;}
            [field: SerializeField] public Button ButtonClose {get; set;}
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

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.ButtonRetry.SetOnClickDestination(OnButtonRetryClick);
                View.ButtonLeave.SetOnClickDestination(OnButtonLeaveClick);
                View.ButtonClose.SetOnClickDestination(OnButtonCloseClick);
                LevelManager.Instance.SetPause(true);
            }
            public async UniTask OnButtonRetryClick()
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                ViewOptions viewOptions = new ViewOptions(nameof(ModalRetryGame));
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
            }            
            
            private async UniTask OnButtonLeaveClick()
            {
                ViewOptions viewOptions = new ViewOptions(nameof(ModalLeaveGame));
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
            }
            
            private void ShowAdsInterSuccess()
            {
                Debug.Log("Show Ads Inter Success");
                UserDataManager.Instance.IncreaseCountAdsInterstitialInDay();
                NoAdsPopupManager.MarkInterstitialWatched();
            }
            
            private async UniTask OnButtonCloseClick()
            {
                if (GameManager.Instance.IsState(GameState.InGame))
                {
                    LevelManager.Instance.SetPause(false);
                }
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
            public async UniTask BackToMainMenu()
            {
                await GameManager.Instance.TryBackToMainMenu();
            }

            public UniTask Cleanup(Memory<object> args)
            {
                Debug.Log("Cleanup");
                return UniTask.CompletedTask;
            }
        }

    }
}