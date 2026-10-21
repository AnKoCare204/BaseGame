using System;
using Cysharp.Threading.Tasks;
using Game.Helper;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    /// <summary>
    /// Pre-race popup for stage 3: shows what the stage asks for and the join reward, and starts
    /// the race. Joining opens the race board so the player sees the line-up they drew.
    /// </summary>
    public class ModalJoinShipRaceStage3 : Modal
    {
        [field: SerializeField] public ModalJoinShipRaceStage3Context.UIPresenter UIPresenter { get; private set; }

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
    public class ModalJoinShipRaceStage3Context
    {
        public const int StageId = 2;

        public static class Events
        {
            public static Action SampleEvent { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
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
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public Button BtnJoin { get; private set; }
            [field: SerializeField] public Button BtnClose { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TxtTimeEndRun { get; private set; }

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

            private bool IsBusy { get; set; }

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                // Reset per open: a pooled modal comes back with whatever the last session left here
                IsBusy = false;

                View.BtnJoin.SetOnClickDestination(OnClickJoin);
                View.BtnClose.SetOnClickDestination(OnClickClose);
                // The run can roll over while this popup sits open. Polling IsRunExpired is no use:
                // the manager opens the next run in the same frame, so the flag is false again by
                // the time this reads it - and the popup would keep offering a stage that no longer
                // exists. The reset event is the only reliable signal.
                ShipRaceManager.OnRunReset += OnRunReset;

                View.MainView.UpdateAsObservable().Subscribe(OnUpdate).AddTo(View.MainView);
            }

            public UniTask Cleanup(Memory<object> args)
            {
                ShipRaceManager.OnRunReset -= OnRunReset;
                return UniTask.CompletedTask;
            }

            private void OnRunReset()
            {
                AutoClose().Forget();
            }

            private void OnUpdate(Unit _)
            {
                if (!ShipRaceManager.Instance.IsInitialize) return;
                // Sat here until the run ended: there is nothing left to join, so the popup closes
                // itself rather than offering a race that JoinRace would now refuse
                if (ShipRaceManager.Instance.IsRunExpired)
                {
                    View.BtnJoin.enabled = false;
                    AutoClose().Forget();
                    return;
                }

                if (View.TxtTimeEndRun == null) return;
                View.TxtTimeEndRun.SetText(ShipRaceManager.Instance.RunTimeRemaining.TimeSpanToStringUI());
            }

            private async UniTask AutoClose()
            {
                // OnUpdate fires every frame, so the guard stops a pop being queued repeatedly
                if (IsBusy) return;
                IsBusy = true;
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            private async UniTask OnClickJoin()
            {
                if (IsBusy) return;
                if (!ShipRaceManager.Instance.CanJoinRace()) return;
                // This popup's stage is baked in at compile time, so it must match what the manager
                // is actually on - a run that rolled over would otherwise start stage 1 from here
                if (ShipRaceManager.Instance.CurrentStage != StageId)
                {
                    await AutoClose();
                    return;
                }

                IsBusy = true;
                ShipRaceManager.Instance.JoinRace();
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                await ShipRaceModalHelper.ShowStageModal(StageId, ShipRaceModalState.ShowCurrentStage);
            }

            private async UniTask OnClickClose()
            {
                if (IsBusy) return;
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
        }
    }
}
