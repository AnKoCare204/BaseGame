using System;
using System.Collections.Generic;
using Core.UI.Activities;
using Game.Helper;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    /// <summary>
    /// Race board for stage 1. The ships animate from where they were last drawn to their live
    /// points, then the win or lose panel appears. Claiming (win) or confirming (lose) closes this
    /// modal and opens the join modal for whatever stage the player is on afterwards.
    /// </summary>
    public class ModalShipRaceStage1Info : Modal
    {
        [field: SerializeField] public ModalShipRaceStage1InfoContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalShipRaceStage1InfoContext
    {
        public const int StageId = 0;
        // 5 lanes, player always in the middle one
        public const int PlayerLaneIndex = 2;

        public static class Events
        {
            public static Action SampleEvent { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField] public ShipRaceModalState ModalState { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                ModalState = args.Length > 0
                    ? args.Span[0] as ShipRaceModalState? ?? ShipRaceModalState.ShowCurrentStage
                    : ShipRaceModalState.ShowCurrentStage;
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public ShipPath[] ShipPaths { get; private set; }
            // Texts are authored in the prefab and live outside the result panels, so each one is
            // toggled on its own instead of riding on a parent
            [field: Title("Texts")]
            [field: SerializeField] public GameObject GOTxtNormal { get; private set; }
            [field: SerializeField] public GameObject GOTxtWin { get; private set; }
            [field: SerializeField] public GameObject GOTxtLose { get; private set; }
            [Tooltip("Shown instead of the lose text when the run clock ended the race")]
            [field: SerializeField] public GameObject GOTxtEventEnd { get; private set; }
            [field: Title("Result panels")]
            [field: SerializeField] public GameObject GOWin { get; private set; }
            [field: SerializeField] public GameObject GOLose { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TxtTimeEndRun { get; private set; }
            [Tooltip("Countdown block. Hidden when the run clock is what ended the race")]
            [field: SerializeField] public GameObject PanelTime { get; private set; }
            [field: SerializeField] public Button BtnClaimReward { get; private set; }
            [field: SerializeField] public Button BtnConfirmLose { get; private set; }
            [field: SerializeField] public Button BtnClose { get; private set; }
            [field: SerializeField] public Button BtnInfo { get; private set; }
            [field: SerializeField] public Button BtnRewardInfo { get; private set; }
            [field: SerializeField] public Transform TfShowInfo { get; private set; }
            [field: SerializeField] public Button BtnCloseBG { get; private set; }
            
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
            [field: Title("Anim")]
            [field: SerializeField] public float DelayBeforeMove { get; private set; } = 0.3f;
            [field: SerializeField] public float DelayBeforeResultPanel { get; private set; } = 0.2f;

            private List<ShipRaceRacer> Racers { get; set; } = new();
            private bool IsBusy { get; set; }

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                // Reset per open: a pooled modal comes back with whatever the last session left here,
                // and a stuck IsBusy would make every button dead on the next open
                IsBusy = false;

                View.BtnClose.SetOnClickDestination(OnClickClose);
                View.BtnInfo.SetOnClickDestination(OnClickInfo);
                View.BtnConfirmLose.SetOnClickDestination(OnClickConfirmLose);
                View.BtnRewardInfo.SetOnClickDestination(OnClickBtnRewardInfo);
                View.BtnCloseBG.SetOnClickDestination(OnClickClose);

                InitLanes();
                // Until the ships have travelled every state shows the plain stage description
                ShowNormalState();
                // No countdown on a lost race: the run is either over, or this stage is done with
                SetActive(View.PanelTime, Model.ModalState != ShipRaceModalState.ShowLoseStage);
                View.BtnClose.gameObject.SetActive(Model.ModalState == ShipRaceModalState.ShowCurrentStage);

                View.MainView.UpdateAsObservable().Subscribe(OnUpdate).AddTo(View.MainView);
            }

            public void DidPushEnter(Memory<object> args)
            {
                PlayRaceAnim().Forget();
            }

            public UniTask Cleanup(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }

            private void OnUpdate(Unit _)
            {
                if (View.TxtTimeEndRun == null) return;
                if (!ShipRaceManager.Instance.IsInitialize) return;
                // The countdown is hidden on a lost race, so there is nothing to write to
                if (Model.ModalState == ShipRaceModalState.ShowLoseStage) return;

                TimeSpan timeLeft = ShipRaceManager.Instance.RunTimeRemaining;
                if (timeLeft.TotalSeconds <= 0d)
                {
                    // The run is over and only waiting to be acknowledged: a ticking clock, or a
                    // negative one, would both read as wrong here
                    View.TxtTimeEndRun.SetText("Finished");
                    return;
                }

                View.TxtTimeEndRun.SetText(timeLeft.TimeSpanToStringUI());
            }

            /// <summary>
            /// Lanes are laid out with the player in the middle and the bots filling outwards, so the
            /// player's ship always reads from the same place on the board.
            /// </summary>
            private void InitLanes()
            {
                Racers = ShipRaceManager.Instance.GetRacersInJoinOrder();
                int target = ShipRaceManager.Instance.GetStageTarget(StageId);

                for (int lane = 0; lane < View.ShipPaths.Length; lane++)
                {
                    ShipPath path = View.ShipPaths[lane];
                    if (path == null) continue;
                    ShipRaceRacer racer = GetRacerForLane(lane);
                    if (racer == null)
                    {
                        path.gameObject.SetActive(false);
                        continue;
                    }

                    path.gameObject.SetActive(true);
                    // Start at the last drawn point: PlayRaceAnim moves it to the live one
                    path.Init(racer, target, ShipRaceManager.Instance.GetLastShownPoint(racer));
                }
            }

            /// <summary>Middle lane is the player, bots fill the lanes around it in join order.</summary>
            private ShipRaceRacer GetRacerForLane(int lane)
            {
                if (lane == PlayerLaneIndex)
                {
                    return Racers.Find(racer => racer.IsPlayer);
                }

                int botIndex = lane < PlayerLaneIndex ? lane : lane - 1;
                int found = 0;
                foreach (ShipRaceRacer racer in Racers)
                {
                    if (racer.IsPlayer) continue;
                    if (found == botIndex) return racer;
                    found++;
                }

                return null;
            }

            private async UniTask PlayRaceAnim()
            {
                IsBusy = true;
                
                // Re-apply the start positions now that the modal is laid out and fully entered:
                // anything measured during Initialize could still have been pre-layout
                ResetLanesToLastShown();
                await UniTask.Delay(Mathf.RoundToInt(DelayBeforeMove * 1000),
                    cancellationToken: View.MainView.GetCancellationTokenOnDestroy());

                List<UniTask> moves = new();
                for (int lane = 0; lane < View.ShipPaths.Length; lane++)
                {
                    ShipPath path = View.ShipPaths[lane];
                    if (path == null || !path.gameObject.activeSelf) continue;
                    ShipRaceRacer racer = GetRacerForLane(lane);
                    if (racer == null) continue;
                    // All lanes move together: the race reads as a race, not a queue
                    moves.Add(path.PlayMove(ShipRaceManager.Instance.GetLastShownPoint(racer), racer.Point));
                }

                if (moves.Count > 0) await UniTask.WhenAll(moves);
                ShipRaceManager.Instance.MarkRacersShown();
                ShowWinnerBadge();
                if (!ShipRaceData.Instance.IsShowTut)
                {
                    ShipRaceData.Instance.IsShowTut = true;
                    ShipRaceData.Instance.SaveData();
                    await ShowInfo();
                    await ShowInfoBoatRaceComplete();
                }
                await UniTask.Delay(Mathf.RoundToInt(DelayBeforeResultPanel * 1000),
                    cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
              
                ShowResultPanel();
                // Auto claim: no button to press. Safe to await here because PlayRaceAnim runs from
                // DidPushEnter, NOT from a SetOnClickDestination handler - those are drained one at a
                // time by ActionExecuteRunner, so awaiting the reward popup's close from inside one
                // would deadlock (its close button is queued behind us).
                await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await TryAutoClaimReward();
                IsBusy = false;
            }

            private async UniTask TryAutoClaimReward()
            {
                if (Model.ModalState != ShipRaceModalState.ShowWinStage) return;
                if (!ShipRaceManager.Instance.NeedShowWin) return;
                await ShipRaceManager.Instance.TryClaimReward();
                await UIManager.Instance.ClaimRewardComplete();
                // Reward is banked: the win panel has nothing left to offer
                SetActive(View.GOWin, false);
                View.BtnClose.gameObject.SetActive(true);
                View.BtnCloseBG.gameObject.SetActive(true);
            }
            private async UniTask ShowInfo()
            {
                ActivityShowRewardTreetopContext.Events.CloseActivity?.Invoke();
                ViewOptions activityShipRaceInfo = new(nameof(ActivityBoatRaceInfo));
                await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(activityShipRaceInfo);
            }
            private async UniTask ShowInfoBoatRaceComplete()
            {
                ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
                await UniTask.WaitUntil(IsCloseTutBoatRace, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());

                return;

                bool IsCloseTutBoatRace()
                {
                    if (container.Activities.Count == 0) return true;
                    return !container.Activities.ContainsKey(nameof(ActivityBoatRaceInfo));
                }
            }

            private void ResetLanesToLastShown()
            {
                for (int lane = 0; lane < View.ShipPaths.Length; lane++)
                {
                    ShipPath path = View.ShipPaths[lane];
                    if (path == null || !path.gameObject.activeSelf) continue;
                    ShipRaceRacer racer = GetRacerForLane(lane);
                    if (racer == null) continue;
                    // The lane only has its real size once the modal is laid out and entered, so the
                    // markers are re-read here before anything is placed against them
                    path.RefreshAnchors();
                    path.SetPoint(ShipRaceManager.Instance.GetLastShownPoint(racer));
                }
            }

            /// <summary>
            /// Crowns the bot that took the race, once the ships have stopped. ShipPath ignores this
            /// on the player's lane, so a player win never puts a badge on the board.
            /// </summary>
            private void ShowWinnerBadge()
            {
                for (int lane = 0; lane < View.ShipPaths.Length; lane++)
                {
                    ShipPath path = View.ShipPaths[lane];
                    if (path == null || !path.gameObject.activeSelf) continue;
                    ShipRaceRacer racer = GetRacerForLane(lane);
                    if (racer == null) continue;
                    path.ShowTop1(ShipRaceManager.Instance.IsWinnerBot(racer));
                }
            }

            /// <summary>
            /// Swaps in the result state once the ships have stopped. Text objects and result panels
            /// are separate objects in the prefab, so each is toggled on its own.
            /// </summary>
            private void ShowResultPanel()
            {
                switch (Model.ModalState)
                {
                    case ShipRaceModalState.ShowWinStage:
                        SetActive(View.GOTxtNormal, false);
                        SetActive(View.GOTxtWin, true);
                        SetActive(View.GOTxtLose, false);
                        SetActive(View.GOWin, true);
                        SetActive(View.GOLose, false);
                        break;
                    case ShipRaceModalState.ShowLoseStage:
                        // Timed out reads differently from being beaten: the event ended, nobody won
                        bool isEventEnd = ShipRaceManager.Instance.IsLoseByRunEnd;
                        SetActive(View.GOTxtNormal, false);
                        SetActive(View.GOTxtWin, false);
                        SetActive(View.GOTxtLose, !isEventEnd);
                        SetActive(View.GOTxtEventEnd, isEventEnd);
                        SetActive(View.GOWin, false);
                        SetActive(View.GOLose, true);
                        SetActive(View.BtnClose.gameObject, true);
                        // SetActive(View.BtnCloseBG.gameObject, true);
                        break;
                    default:
                        View.BtnClose.gameObject.SetActive(true);
                        break;
                }
            }

            private void ShowNormalState()
            {
                SetActive(View.GOTxtNormal, true);
                SetActive(View.GOTxtWin, false);
                SetActive(View.GOTxtLose, false);
                SetActive(View.GOTxtEventEnd, false);
                SetActive(View.GOWin, false);
                SetActive(View.GOLose, false);
            }

            private static void SetActive(GameObject target, bool isActive)
            {
                if (target == null) return;
                target.SetActive(isActive);
            }

            /// <summary>Lose path: acknowledge, close, then offer the same stage again.</summary>
            private async UniTask OnClickConfirmLose()
            {
                if (IsBusy) return;
                IsBusy = true;
                ShipRaceManager.Instance.ConfirmLose();
                await CloseAndOpenJoin();
            }

            /// <summary>
            /// Waits for the pop to finish before pushing, so the join modal enters on a clean stack.
            /// The stage is read after the claim, which is what advances it.
            /// </summary>
            private async UniTask CloseAndOpenJoin()
            {
                ActivityShowRewardOnTopContext.Events.CloseActivity?.Invoke();
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                // Claiming the last stage closes the event for this run: CurrentStage rolls back to 0
                // but there is nothing left to join, so the join popup must not reopen on stage 1
                if (!ShipRaceManager.Instance.CanJoinRace()) return;
                await ShipRaceModalHelper.ShowJoinModal(ShipRaceManager.Instance.CurrentStage);
            }

            private async UniTask OnClickClose()
            {
                if (IsBusy) return;
                //await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
                if (Model.ModalState == ShipRaceModalState.ShowLoseStage)
                {
                    IsBusy = true;
                    ShipRaceManager.Instance.ConfirmLose();
                    await CloseAndOpenJoin();
                    return;
                }
                await CloseAndOpenJoin();
            }

            private async UniTask OnClickInfo()
            {
                await ShowInfo();
            }

            private async UniTask OnClickBtnRewardInfo()
            {
                ViewOptions viewOptions = new(nameof(ActivityShowRewardOnTop));
                if (ShipRaceData.Instance.IsRewardClaimed)
                {
                    await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(viewOptions,new List<GameResource>(),View.TfShowInfo.position);
                }
                else
                {
                    List<GameResource> resources =
                        ShipRaceGlobalConfig.Instance.GetStageConfig(0).Rewards.GameResourceList;
                    await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(viewOptions,resources,View.TfShowInfo.position);
                }
            }
        }
    }
}
