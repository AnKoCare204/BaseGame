using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TW.UGUI.Core.Modals;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalNoAds : Modal
    {
        [field: SerializeField] public ModalNoAdsContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
            AddLifecycleEvent(new MainMenuModalLifecycle(), 2);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ModalNoAdsContext
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
            [field: SerializeField] public Button CloseButton { get; private set; }
            [field: SerializeField] public UIShopPack ShopPack { get; private set; }
            [field: SerializeField] public SkeletonGraphic PackAnim {get; set;}
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

                View.CloseButton.SetOnClickDestination(OnTouchCloseButton);
                View.ShopPack.SetActionBuyPack(OnBuyNoAdsSuccess);

                View.ShopPack.OnShown();
            }

            public void DidPushEnter(Memory<object> args)
            {
                View.PackAnim.AnimationState.SetAnimation(0, "start", false);
                View.PackAnim.AnimationState.AddAnimation(0, "idle", true, 0f);
            }

            public void OnTouchCloseButton()
            {
                ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            public async UniTask OnBuyNoAdsSuccess(PackageName packageName)
            {
                Debug.Log("BuyNoAdsSuccess");
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
        }
    }
}