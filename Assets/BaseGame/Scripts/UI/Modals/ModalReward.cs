using System;
using System.Collections.Generic;
using CoreData;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Modals;
using TW.Utility.DesignPattern;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalReward : Modal
    {
        [field: SerializeField] public ModalRewardContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalRewardContext
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
            [field: SerializeField] public CanvasGroup TitleCG {get; private set;}
            [field: SerializeField] public UIItemInfo UIItemInfoPrefab { get; private set; }
            [field: SerializeField] public Transform ItemParent { get; private set; }
            [field: SerializeField] public Button CloseBtn { get; private set; }
            

            public UniTask Initialize(Memory<object> args)
            {
                
                return UniTask.CompletedTask;
            }
            public async UniTask InitReward()
            {
                // AudioManager.Instance.PlaySoundFx(AudioKey.SfxUIWinGame);
                CloseBtn.transform.localScale = Vector3.zero;
                CloseBtn.interactable = false;
                TitleCG.alpha = 0f;
                Vector3 titlePos = TitleCG.transform.position;
                Vector3 titlePosStart = TitleCG.transform.position - Vector3.up * 150;
                TitleCG.transform.position = titlePosStart;
                LMotion.Create(0f, 1f, 0.35f).Bind(x => TitleCG.alpha = x).AddTo(MainView);
                LMotion.Create(titlePosStart, titlePos, 0.35f).Bind(x => TitleCG.transform.position = x).AddTo(MainView);
                List<GameResource> rewardResources = RewardManager.Instance.RewardResources;
                for (int i = 0; i < rewardResources.Count; i++)
                {
                    GameResource resource = rewardResources[i];
                    if(resource.Value <= 0) continue;
                    UIItemInfo uiItemInfo = GameObject.Instantiate(UIItemInfoPrefab, ItemParent);
                    uiItemInfo.Init(resource);
                    uiItemInfo.gameObject.SetActive(true);
                    uiItemInfo.ShowItemAnim((i + 1) * 0.1f).Forget();
                }
                await UniTask.Delay(1500, cancellationToken: MainView.GetCancellationTokenOnDestroy());
                await LMotion.Create(0f, 1f, 0.15f).WithEase(Ease.OutBack)
                    .Bind(x => CloseBtn.transform.localScale = Vector3.one * x).AddTo(MainView);
                CloseBtn.interactable = true;
            }
            
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            private Action _callback;

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                _callback = (Action)args.Span[0];
                View.CloseBtn.onClick.AddListener(() => OnButtonCloseClick().Forget());
                View.InitReward().Forget();
            }
            
            private async UniTask OnButtonCloseClick()
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                _callback?.Invoke();
            }
        }
    }
}