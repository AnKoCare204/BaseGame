using System;
using Core.GamePlay;
using Core.UI.Activities;
using Core.UI.Screens;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UI.ModalProfile;

namespace Core.UI.Modals
{
    public class ModalHome : Modal, IUITabLifecycleEvent
    {
        [field: SerializeField] public ModalHomeContext.UIPresenter UIPresenter { get; private set; }

        public override async UniTask Initialize(Memory<object> args)
        {
            await UIPresenter.Initialize(args);
        }

        public void OnTabEnter(Memory<object> args)
        {
            UIPresenter.DidPushEnter(args);
            // Debug.Log("OnTabEnter ModalHome");
        }

        public void OnTabExit(Memory<object> args)
        {
            UIPresenter.DidPushExit(args);
            // Debug.Log("OnTabExit ModalHome");
            // if (!GameManager.Instance.IsState(GameState.MainMenu))
            // {
            //     GameGlobalEvent.OnPurchaseIapSuccess -= UIPresenter.CheckShowAdsIcon;
            //     
            // }
        }
    }


    [Serializable]
    public class ModalHomeContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }

            /// <summary>GameManager bật lại nút Play khi vào level thất bại sau màn loading.</summary>
            public static Action<bool> SetPlayInteractable { get; set; }
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
            [field: SerializeField] public Button ButtonPlay {get; private set;}
            // [field: SerializeField] public Button ButtonSetting {get; private set;}
            // [field: SerializeField] public UIHomeAnim UIHomeAnim {get; private set;}
            // [field: SerializeField] public LevelUnitList LevelUnitList {get; private set;}
            
            [FoldoutGroup("HomeItemFeature")]
            [field: SerializeField] public Button ButtonNoAds { get; private set; }
            [field: SerializeField] public Button BtnStarterPack { get; private set; }
            [field: SerializeField] public Button BtnDailyReward { get; private set; }
            [field: SerializeField] public Button BtnProfile { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            // public void InitLevel(int level)
            // {
            //     LevelUnitList.Init();
            // }
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
                View.ButtonPlay.SetOnClickDestination(OnClickButtonPlay);
                Events.SetPlayInteractable = SetPlayInteractable;
                View.ButtonNoAds.SetOnClickDestination(OnTouchButtonNoAds);
                // View.BtnStarterPack.SetOnClickDestination(OnTouchButtonStarterPack);
                View.BtnDailyReward.SetOnClickDestination(OnTouchButtonDailyReward);
                View.BtnProfile.SetOnClickDestination(OnClickButtonProfile);
                // GameGlobalEvent.OnPurchaseIapSuccess += CheckShowAdsIcon;
                // CheckShowAdsIcon();
                // CheckShowPopupNoAds();
            }

            private static Transform FindNamed(Transform root, string name)
            {
                if (root.name == name) return root;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform found = FindNamed(root.GetChild(i), name);
                    if (found != null) return found;
                }
                return null;
            }

            private void OnTouchButtonDailyReward()
            {
                var newViewOption = new ViewOptions(nameof(ModalDailyReward));
                ModalContainer.Find(ContainerKey.Modals).PushAsync(newViewOption).Forget();
            }

            private async UniTask OnTouchButtonStarterPack()
            {
                var newViewOption = new ViewOptions("ModalStarterPack");
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(newViewOption);
            }
            private async UniTask OnClickButtonProfile()
            {
                ViewOptions viewOptions = new ViewOptions(nameof(ModalProfile));
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
            }
            private async UniTask OnClickButtonSetting()
            {
                ViewOptions viewOptions = new ViewOptions(nameof(ModalSetting));
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
            }

            private async UniTask OnClickButtonPlay()
            {
                // Game có giới hạn số level (hết level thật) thì kiểm ở đây và mở ModalComingSoon thay vì vào bàn.
                if (InGameDataManager.Instance.InGameData.ResourceData.IsEnoughResourceValue(ResourceType.Life, 1) || PlayerResourceManager.Instance.HasInfiniteLife)
                {
                    View.ButtonPlay.interactable = false;
                    if (!await GameManager.Instance.TryLoadCurrentLevel()) View.ButtonPlay.interactable = true;
                }
                else
                {
                    ViewOptions viewOptions = new ViewOptions(nameof(ModalFillHeart));
                    await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
                }
            }

            private void SetPlayInteractable(bool value)
            {
                if (View.ButtonPlay != null) View.ButtonPlay.interactable = value;
            }

            public void DidPushEnter(Memory<object> args)
            {
            }
            
            public void DidPushExit(Memory<object> args)
            {
            }

            public void OnTouchButtonNoAds()
            {
                ViewOptions viewOptions = new ViewOptions("ModalNoAds");
                ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions).Forget();
            }


            public UniTask Cleanup(Memory<object> args)
            {
                Events.SetPlayInteractable = null;
                return UniTask.CompletedTask;
            }
        }
    }
}