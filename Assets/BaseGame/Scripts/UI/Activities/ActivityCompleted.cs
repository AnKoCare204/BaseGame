using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TW.UGUI.Core.Activities;

namespace Core.UI.Activities
{
    public class ActivityCompleted : Activity
    {
        [field: SerializeField] public ActivityCompletedContext.UIPresenter UIPresenter { get; private set; }

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
    public class ActivityCompletedContext
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
            [field: SerializeField]
            public SkeletonGraphic SkeletonGraphic { get; private set; }
            
            [field: SerializeField] public Transform TrsSkeleton { get; private set; }
            public AnimationCurve moveCurve;
            public AnimationCurve hideCurve;
            public Vector2 offset;
            public float timeAnim;
            public float timeDelay;
            public float timeSoundDelay;
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            [Button]
            public async UniTask AnimShow()
            {
                TrsSkeleton.localPosition = Vector3.zero;
                var currentPos =  TrsSkeleton.position;
                var targetPos = currentPos + (Vector3)offset;
                await LMotion.Create(currentPos, targetPos, timeAnim).WithEase(moveCurve).Bind(x =>TrsSkeleton.position = x).AddTo(MainView);
                
                currentPos =  TrsSkeleton.position;
                targetPos = currentPos - (Vector3)offset;
                await LMotion.Create(currentPos, targetPos, timeAnim).WithDelay(timeDelay).WithEase(hideCurve).Bind(x =>TrsSkeleton.position = x).AddTo(MainView);
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
            }

            public void DidEnter(Memory<object> args)
            {
                _ = View.AnimShow();
                View.SkeletonGraphic.AnimationState.SetAnimation(0,"start",false);
                // View.SkeletonGraphic.AnimationState.AddAnimation(0, "idle", false,0f);
                View.SkeletonGraphic.AnimationState.AddAnimation(0, "end", false,0f);
                AudioManager.Instance.PlaySoundFx(AudioKey.SfxComplete, View.timeSoundDelay);
                DelayHide().Forget();
            }

            private async UniTask DelayHide()
            {
                await UniTask.WaitForSeconds(View.timeAnim, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await ActivityContainer.Find(ContainerKey.Activities).HideAsync(nameof(ActivityCompleted));
            }
        }
    }
}