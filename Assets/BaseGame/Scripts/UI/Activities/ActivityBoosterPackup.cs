using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TW.UGUI.Core.Activities;

namespace Core.UI.Activities
{
    public class ActivityBoosterPackup : Activity
    {
        [field: SerializeField] public ActivityBoosterPackupContext.UIPresenter UIPresenter { get; private set; }

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
    public class ActivityBoosterPackupContext
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
            [field: SerializeField] public SkeletonGraphic SkeletonGraphic { get; set; }

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
            }

            public void DidEnter(Memory<object> args)
            {
                DelayClose().Forget();
            }

            private async UniTask DelayClose()
            {
                SkeletonGraphic.AnimationState.ClearTrack(0);
                SkeletonGraphic.AnimationState.SetAnimation(0, "animation", false);
                SkeletonGraphic.gameObject.SetActive(true);
                await UniTask.WaitForSeconds(2f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                ActivityContainer.Find(ContainerKey.Activities).Hide(nameof(ActivityBoosterPackup));
            }
        }
    }
}