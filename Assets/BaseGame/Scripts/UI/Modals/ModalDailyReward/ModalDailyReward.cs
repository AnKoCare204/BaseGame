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
    public class ModalDailyReward : Modal
    {
        [field: SerializeField] public ModalDailyRewardContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalDailyRewardContext
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
            [field: SerializeField] public SlotDaily[] SlotDaily { get; private set; }
            [field: SerializeField] public Button BtnClose { get; private set; }
            [field: SerializeField] public Button CloseButtonHidden {get; set;}
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            [Button]
            public void InitData()
            {
                for (var i = 0; i < SlotDaily.Length; i++)
                {
                    SlotDaily[i].InitData(i, i == SlotDaily.Length - 1, -1);
                }
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

                View.InitData();
                View.BtnClose.onClick.AddListener(OnClickClose);
                View.CloseButtonHidden.SetOnClickDestination(OnClickClose);
            }

            private void OnClickClose()
            {
                //var option = new ViewOptions(nameof(ModalDailyReward));
                ModalContainer.Find(ContainerKey.Modals).PopAsync(true).Forget();
            }
        }
    }
}