using System;
using Core.UI.Screens;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Screens;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Activities
{
    public class ActivityLoading : Activity
    {
        [field: SerializeField] public ActivityLoadingContext.UIPresenter UIPresenter { get; private set; }
        
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
    public class ActivityLoadingContext
    {
        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            public Func<UniTask> LoadingTask { get; set; }
            public Func<UniTask> OnLoadingProcessComplete { get; set; }

            public UniTask Initialize(Memory<object> args)
            {
                LoadingTask = args.Span[0] as Func<UniTask>;
                OnLoadingProcessComplete = args.Span[1] as Func<UniTask>;
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

            [field: SerializeField] public Slider Slider { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TxtLoading { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            public void OnLoadingProcessUpdate(float value)
            {
                TxtLoading.text = $"{Mathf.RoundToInt(Slider.value * 100)}%";
                Slider.value = value;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IActivityLifecycleEventSimple
        {
            [field: SerializeField] public UIView View { get; set; } = new();
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
            }

            public void DidEnter(Memory<object> args)
            {
                LoadingProcess().Forget();
            }

            public async UniTask LoadingProcess()
            {
                UniTask handle = LMotion.Create(0.05f, 0.9f, 1.5f)
                    .WithEase(Ease.Linear)
                    .Bind(OnLoadingProcessUpdate)
                    .AddTo(View.MainView)
                    .ToUniTask();
                UniTask task = Model.LoadingTask?.Invoke() ?? UniTask.CompletedTask;
                await UniTask.WhenAll(handle, task);
                await LMotion.Create(0.9f, 1, 0.5f)
                    .WithEase(Ease.Linear)
                    .WithOnComplete(OnLoadingProcessComplete)
                    .Bind(OnLoadingProcessUpdate)
                    .AddTo(View.MainView)
                    .ToUniTask();
            }
            private void OnLoadingProcessUpdate(float value)
            {
                View.OnLoadingProcessUpdate(value);
            }
            private void OnLoadingProcessComplete()
            {
                OnLoadingProcessCompleteAsync().Forget();
            }

            private async UniTask OnLoadingProcessCompleteAsync()
            {
                await ActivityContainer.Find(ContainerKey.Activities)
                    .HideAsync(nameof(ActivityLoading));
                if (Model.OnLoadingProcessComplete != null)
                {
                    await Model.OnLoadingProcessComplete.Invoke();
                }

            }
        }
    }
}