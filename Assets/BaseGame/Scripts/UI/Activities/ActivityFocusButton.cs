using System;
using Core.UI.Screens;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.MVPPattern;
using UnityEngine;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Activities;
using UnityEngine.UI;

namespace Core.UI.Activities
{
    public class ActivityFocusButton : Activity
    {
        [field: SerializeField] public ActivityFocusButtonContext.UIPresenter UIPresenter { get; private set; }

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
    public class ActivityFocusButtonContext
    {
        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField] public Button LinkButton { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                try
                {
                    LinkButton = (Button)args.Span[0];
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message);
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
            [field: SerializeField] public Transform ButtonRect { get; private set; }
            [field: SerializeField] public Transform HandClick { get; private set; }
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

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                Model.LinkButton.onClick.AddListener(OnButtonFocusClick);
                FocusButtonClick();
            }

            private void FocusButtonClick()
            {
                View.ButtonRect.localScale = Vector3.one * 30;
                View.ButtonRect.position = Model.LinkButton.transform.position;
                View.HandClick.position = Model.LinkButton.transform.position;
                LMotion.Create(0f, 1f, 0.5f)
                    .WithOnComplete(OnFocusComplete)
                    .Bind(OnFocusUpdate)
                    .AddTo(View.MainView);
                LMotion.Create(0f, 1f, 0.7f)
                    .WithOnComplete(() => ScreenInGameContext.Events.SetBlockObjectActive?.Invoke(false))
                    .RunWithoutBinding()
                    .AddTo(View.MainView);
            }
            private void OnFocusUpdate(float progress)
            {
                View.ButtonRect.localScale = Vector3.one * Mathf.Lerp(30, 1, progress);
            }
            private void OnFocusComplete()
            {
                View.HandClick.gameObject.SetActive(true);
                View.ButtonRect.localScale = Vector3.one;
                View.ButtonRect.position = Model.LinkButton.transform.position;
                
            }

            private void UnfocusButtonClick()
            {
                LMotion.Create(1f, 0f, 0.5f)
                    .WithOnComplete(UnfocusButtonComplete)
                    .Bind(OnFocusUpdate)
                    .AddTo(View.MainView);
            }
            private void UnfocusButtonComplete()
            {
                Model.LinkButton.onClick.RemoveListener(OnButtonFocusClick);
                ActivityContainer.Find(ContainerKey.Activities).Hide(nameof(ActivityFocusButton));
            }
            private void OnButtonFocusClick()
            {
                View.HandClick.gameObject.SetActive(false);    
                UnfocusButtonClick();
            }

            // Activity có thể bị đóng ngang (thua / về Home gọi CloseAllActivities) trước khi người chơi bấm:
            // gỡ listener khỏi nút thật, không thì lần bấm sau gọi vào view đã huỷ (MissingReferenceException).
            public UniTask Cleanup(Memory<object> args)
            {
                if (Model.LinkButton != null) Model.LinkButton.onClick.RemoveListener(OnButtonFocusClick);
                return UniTask.CompletedTask;
            }
        }
    }
}