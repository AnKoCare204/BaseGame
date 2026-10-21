using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.MVPPattern;
using UnityEngine;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Activities;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using CoreData;
using TW.Utility.Extension;

namespace Core.UI.Modals
{
    public class ActivityClaimReward : Activity
    {
        [field: SerializeField] public ActivityHomeClaimRewardContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
            AddLifecycleEvent(new MainMenuPopupLifecycle(), 2);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ActivityHomeClaimRewardContext
    {
        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField] public ClaimGameResourceGroup ClaimGameResourceGroup { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                try
                {
                    ClaimGameResourceGroup = (ClaimGameResourceGroup)args.Span[0];
                }
                catch (Exception e)
                {
                    ClaimGameResourceGroup = new ClaimGameResourceGroup();
                }
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public TextGroup TextTitle {get; private set;}
            [field: SerializeField] public UIRewardItem UIItemInfoPrefab { get; private set; }
            [field: SerializeField] public Transform ItemParent { get; private set; }
            [field: SerializeField] public Button CloseBtn { get; private set; }
            [field: SerializeField] public TMPTextAnim TextButton { get; private set; }
            [field: SerializeField] public CanvasGroup Background { get; private set; }
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
            [field: SerializeField] private float Duration { get; set; } = 1f;
            [field: SerializeField] private float UnitDuration { get; set; } = 0.5f;
            [field: SerializeField] private AnimationCurve ScaleCurve { get; set; }
            [field: SerializeField] private AnimationCurve AlphaCurve { get; set; }
            public List<UIRewardItem> UIRewardItemList { get; set; } = new();
            private float DelayPerUnit { get; set; }
            private MotionHandle MotionHandle { get; set; }
            public async UniTask Initialize(Memory<object> args)
            {
                AudioManager.Instance.PlaySoundFx(AudioKey.SfxRewardClaim);
                await Model.Initialize(args);
                await View.Initialize(args);
                View.CloseBtn.SetActive(false);
                View.TextTitle.SetActive(false);
                View.Background.SetActive(false);
                
                View.CloseBtn.SetOnClickDestination(OnButtonCloseClick);
                InitReward();
            }

            public UniTask Cleanup(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            private async UniTask OnButtonCloseClick()
            {
                ClaimReward();
                await WaitClaimSuccess();
                await CloseAndOpenCardPackAsync();
                // await ActivityContainer.Find(ContainerKey.Activities).HideAsync(nameof(ActivityClaimReward));
            }

            private async UniTask CloseAndOpenCardPackAsync()
            {
                UIManager.Instance.BeginClaimRewardPostCloseFlow();
                try
                {
                    await ActivityContainer.Find(ContainerKey.Activities).HideAsync(nameof(ActivityClaimReward));
                    // if (CardCollectionManager.HasInstance && CardCollectionManager.Config != null)
                    //     await CardCollectionManager.Instance.CheckOpenCardPackAsync();
                }
                finally
                {
                    UIManager.Instance.EndClaimRewardPostCloseFlow();
                }
            }
            private async UniTask WaitClaimSuccess()
            {
                MotionHandle.TryCancel();
                MotionHandle = LMotion.Create(1f, 0f, 1f)
                    .Bind(OnAnimUpdate)
                    .AddTo(View.MainView);
                await UniTask.Delay(1000, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
            }

            private void InitReward()
            {
                switch (Model.ClaimGameResourceGroup.ClaimGameResourceType)
                {
                    case ClaimGameResourceType.Default:
                        InitDefaultReward();
                        break;
                    case ClaimGameResourceType.GamePlay:
                        InitGamePlayReward();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private void InitGamePlayReward()
            {
                for (int i = 0; i < Model.ClaimGameResourceGroup.GameResourceList.Count; i++)
                {
                    GameResource resource = Model.ClaimGameResourceGroup.GameResourceList[i];
                    if(TryClaimCoinReward(resource, View.MainView.transform.position)) continue;
                    TryClaimOtherReward(resource);
                }
                ResourceData.Instance.RemoveClaimGameResourceGroup(Model.ClaimGameResourceGroup);
                DelayAndSelfClose(2f).Forget();
            }
            private async UniTask DelayAndSelfClose(float delay)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await CloseAndOpenCardPackAsync();
            }
            
            [Button]
            private void InitDefaultReward()
            {
                View.CloseBtn.SetActive(true);
                View.TextTitle.SetActive(true);
                View.Background.SetActive(true);
                View.TextTitle.PlayAnimation(Model.ClaimGameResourceGroup.TitleName);
                View.TextButton.PlayAnimation();
                PlayResourceAnimation();
            }
            private void PlayResourceAnimation()
            {
                View.CloseBtn.interactable = false;
                for (int i = 0; i < UIRewardItemList.Count; i++)
                {
                    Object.Destroy(UIRewardItemList[i].gameObject);
                }
                UIRewardItemList.Clear();
                for (int i = 0; i < Model.ClaimGameResourceGroup.GameResourceList.Count; i++)
                {
                    GameResource resource = Model.ClaimGameResourceGroup.GameResourceList[i];
                    UIRewardItem uiItemInfo = Object.Instantiate(View.UIItemInfoPrefab, View.ItemParent);
                    uiItemInfo.Init(resource);
                    uiItemInfo.gameObject.SetActive(true);
                    UIRewardItemList.Add(uiItemInfo);
                }
                int length = UIRewardItemList.Count;
                DelayPerUnit = (Duration - UnitDuration) / length;
                MotionHandle.TryCancel();
                MotionHandle = LMotion.Create(0f, Duration, Duration)
                    .WithOnComplete(OnAnimComplete)
                    .Bind(OnAnimUpdate)
                    .AddTo(View.MainView);
            }
            private void OnAnimUpdate(float progress)
            {
                for (int i = 0; i < UIRewardItemList.Count; i++)
                {
                    float unitProgress = Mathf.Clamp01((progress - DelayPerUnit * i)/UnitDuration);
                    float scaleValue = ScaleCurve.Evaluate(unitProgress);
                    UIRewardItemList[i].transform.localScale = Vector3.one * scaleValue;
                }
                View.Background.alpha = AlphaCurve.Evaluate(progress/Duration);
            }
            private void OnAnimComplete()
            {
                View.CloseBtn.interactable = true;
                for (int i = 0; i < UIRewardItemList.Count; i++)
                {
                    UIRewardItemList[i].transform.localScale = Vector3.one;
                }
            }

            private void ClaimReward()
            {
                for (int i = 0; i < Model.ClaimGameResourceGroup.GameResourceList.Count; i++)
                {
                    GameResource resource = Model.ClaimGameResourceGroup.GameResourceList[i];
                    UIRewardItem uiRewardItem = UIRewardItemList[i];
                    if(TryClaimCoinReward(resource, uiRewardItem.transform.position)) continue;
                    TryClaimOtherReward(resource);
                }
                ResourceData.Instance.RemoveClaimGameResourceGroup(Model.ClaimGameResourceGroup);
            }

            private bool TryClaimCoinReward(GameResource resource, Vector3 position)
            {
                if (resource.Type != ResourceType.Money) return false;
                int currentCoin = PlayerResourceManager.Instance.GetResource(ResourceType.Money).Amount;
                int nextCoin = currentCoin + resource.Amount;
                UIMoneyResourceEffect.TryPlayAnim(position, currentCoin, nextCoin);
                PlayerResourceManager.Instance.AddResourceValue(resource.Type, resource.Amount.Value, 
                    Model.ClaimGameResourceGroup.ClaimType);
                return true;
            }

            private bool TryClaimOtherReward(GameResource resource)
            {
                PlayerResourceManager.Instance.AddResourceValue(resource.Type, resource.Amount.Value, 
                    Model.ClaimGameResourceGroup.ClaimType);
                return true;
            }
        }
    }
}