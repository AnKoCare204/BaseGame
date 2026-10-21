using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Modals;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalRate : Modal
    {
        [field: SerializeField] public ModalRateContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalRateContext
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
            [field: SerializeField] public Button ButtonRate {get; private set;}
            [field: SerializeField] public Button ButtonClose {get; private set;}
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
                View.ButtonRate.SetOnClickDestination(OnClickButtonRate);
                View.ButtonClose.SetOnClickDestination(OnClickButtonClose);
            }

            private async UniTask OnClickButtonRate()
            {
                ShowRatting();
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
            private void ShowRatting()
            {
#if UNITY_ANDROID
                AppReviewManager.Instance.AllInOneFlowClick();
#elif UNITY_IOS
                AppReviewManager.Instance.RequestReview();
#endif
            }
            private async UniTask OnClickButtonClose()
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
        }
    }
}