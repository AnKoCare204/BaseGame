using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using MoreMountains.Feedbacks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Activities;
using UnityEngine.UI;
using CoreData;
using TW.Utility.Extension;
using UnityEngine.Rendering;

namespace Core.UI.Activities
{
    public class ActivityNotificationOverlay : Activity
    {
        [field: SerializeField] public ActivityNotificationOverlayContext.UIPresenter UIPresenter { get; private set; }

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
    public class ActivityNotificationOverlayContext
    {
        public static class Events
        {
            public static Action ShowLoadingBlock  { get; set; }
            public static Action HideLoadingBlock  { get; set; }
            public static Action<string> ShowSuccess  { get; set; }
            public static Action<string> ShowError  { get; set; }
            public static Action<string> ShowInfo { get; set; }
            public static Action<string> ShowWarning { get; set; }
            public static Action<List<GameResource>, Vector3> ShowResourceGroup { get; set; }
            public static Action HideResourceGroup  { get; set; }
            public static Action ShowAdsBreak { get; set; }
            public static Action HideAdsBreak { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]

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
            [field: SerializeField] public CanvasGroup LoadingGroup { get; private set; }
            
            [field: SerializeField] public GameObject ToastGroup { get; private set; }
            [field: SerializeField] public TextMeshProUGUI ToastTitle { get; private set; }
            [field: SerializeField] public TextMeshProUGUI ToastDescription { get; private set; }
            [field: SerializeField] public UIResourceGroupInfo ResourceGroupInfo { get; private set; }
            [field: SerializeField] public Button ButtonCloseToast { get; private set; }
            [field: SerializeField] public MMF_Player ToastShowAnim { get; private set; }
            [field: SerializeField] public MMF_Player ToastHideAnim { get; private set; }
            [field: SerializeField] public CanvasGroup AdsBreakGroup { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IActivityLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            private MotionHandle LoadingHandle  { get; set; }
            private MotionHandle AdsBreakHandle  { get; set; }
            // Chủ khóa tab riêng cho loading và toast: đóng cái này không được mở tab khi popup khác còn hiện.
            private static readonly object LoadingTabLock = new();
            private static readonly object ToastTabLock = new();
            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.ButtonCloseToast.SetOnClickDestination(OnClickButtonCloseToast);

                Events.ShowLoadingBlock += ShowLoadingBlock;
                Events.HideLoadingBlock += HideLoadingBlock;
                Events.ShowSuccess += ShowSuccess;
                Events.ShowError += ShowError;
                Events.ShowInfo += ShowInfo;
                Events.ShowWarning += ShowWarning;
                Events.ShowResourceGroup += ShowResourceGroup;
                Events.HideResourceGroup += HideResourceGroup;
                Events.ShowAdsBreak += ShowAdsBreak;
                Events.HideAdsBreak += HideAdsBreak;
            }



            public UniTask Cleanup(Memory<object> args)
            {
                Events.ShowLoadingBlock -= ShowLoadingBlock;
                Events.HideLoadingBlock -= HideLoadingBlock;
                Events.ShowSuccess -= ShowSuccess;
                Events.ShowError -= ShowError;
                Events.ShowInfo -= ShowInfo;
                Events.ShowWarning -= ShowWarning;
                Events.ShowResourceGroup -= ShowResourceGroup;
                Events.HideResourceGroup -= HideResourceGroup;
                Events.ShowAdsBreak -= ShowAdsBreak;
                Events.HideAdsBreak -= HideAdsBreak;
                return UniTask.CompletedTask;
            }

            private void ShowLoadingBlock()
            {
                View.LoadingGroup.SetActive(true);
                UIMainMenuTab.SetTabLock(LoadingTabLock, true);
                float currentValue = View.LoadingGroup.alpha;
                LoadingHandle.TryCancel();
                LoadingHandle = LMotion.Create(currentValue, 1f, 0.3f)
                    .Bind(OnLoadingBlockUpdate)
                    .AddTo(View.MainView);
            }

            private void HideLoadingBlock()
            {
                float currentValue = View.LoadingGroup.alpha;
                LoadingHandle.TryCancel();
                LoadingHandle = LMotion.Create(currentValue, 0f, 0.3f)
                    .WithOnComplete(OnHideLoadingBlockComplete)
                    .Bind(OnLoadingBlockUpdate)
                    .AddTo(View.MainView);
            }

            private void OnLoadingBlockUpdate(float progress)
            {
                View.LoadingGroup.alpha = progress;
            }
            private void OnHideLoadingBlockComplete()
            {
                View.LoadingGroup.SetActive(false);
                UIMainMenuTab.SetTabLock(LoadingTabLock, false);
            }

            private void ShowSuccess(string message)
            {
                ShowToast("Success", message);
            }

            private void ShowError(string message)
            {
                ShowToast("Error", message);
            }

            private void ShowInfo(string message)
            {
                ShowToast("Info", message);
            }

            private void ShowWarning(string message)
            {
                ShowToast("Warning", message);
            }

            private void ShowToast(string title, string description)
            {
                UIMainMenuTab.SetTabLock(ToastTabLock, true);
                View.ToastTitle.SetText(title);
                View.ToastDescription.SetText(description);
                View.ToastShowAnim.PlayFeedbacks();
            }
            private void OnClickButtonCloseToast()
            {
                View.ToastHideAnim.PlayFeedbacks();
                UIMainMenuTab.SetTabLock(ToastTabLock, false);
            }

            private void ShowResourceGroup(List<GameResource> resources, Vector3 position)
            {
                View.ResourceGroupInfo.WithResourceList(resources).WithPosition(position).SetActive(true);
            }

            private void HideResourceGroup()
            {
                View.ResourceGroupInfo.SetActive(false);
            }
            private void ShowAdsBreak()
            {
                View.AdsBreakGroup.SetActive(true);
                View.AdsBreakGroup.alpha = 0;
                AdsBreakHandle.TryCancel();
                AdsBreakHandle = LMotion.Create(0f, 1f, 0.2f)
                    .Bind(OnAdsBreakUpdate)
                    .AddTo(View.MainView);
            }

            private void HideAdsBreak()
            {
                AdsBreakHandle.TryCancel();
                AdsBreakHandle = LMotion.Create(View.AdsBreakGroup.alpha, 0f, 0.2f)
                    .WithOnComplete(OnHideAdsBreakComplete)
                    .Bind(OnAdsBreakUpdate)
                    .AddTo(View.MainView);
            }
            private void OnAdsBreakUpdate(float progress)
            {
                View.AdsBreakGroup.alpha = progress;
            }

            private void OnHideAdsBreakComplete()
            {
                View.AdsBreakGroup.SetActive(false);
            }
        }
    }
}