using Core.UI.Modals;
using Cysharp.Threading.Tasks;
using Game.Helper;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home screen entry point for Ship Race. Nothing is shown automatically: a finished race only
/// raises the noti dot here, and the result popup opens when the player taps in.
/// </summary>
public class UIButtonShipRace : MonoBehaviour
{
    [field: SerializeField] public Button Button { get; set; }
    [field: SerializeField] public Button ButtonLock { get; set; }
    [field: SerializeField] public GameObject GOTime { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtTimeEndEvent { get; private set; }
    [field: SerializeField] public GameObject GOLock { get; private set; }
    [field: SerializeField] public GameObject GOUnlock { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtLevelUnlock { get; private set; }
    [field: SerializeField] public GameObject GONoti { get; private set; }

    private void Start()
    {
        Button.SetOnClickDestination(OnClick);
        ButtonLock.SetOnClickDestination(OnClickBtnLock);
        ShipRaceManager.OnInitializeComplete += OnInitializeComplete;
        ShipRaceManager.OnRaceStateChanged += TryShowNoti;
        Init();
    }

    private void OnDestroy()
    {
        ShipRaceManager.OnInitializeComplete -= OnInitializeComplete;
        ShipRaceManager.OnRaceStateChanged -= TryShowNoti;
    }

    private void OnInitializeComplete()
    {
        Init();
    }

    private void Init()
    {
        TxtLevelUnlock.SetText("Level {0}", ShipRaceRemoteConfig.Instance.LevelUnlock);
        gameObject.SetActive(false);
        if (!ShipRaceManager.Instance.IsInitialize) return;
        // Nothing left to do this run: every stage won, and the next run has not opened yet
        if (IsEventClosedForRun()) return;
        if (!ShipRaceManager.Instance.IsEventUnlock)
        {
            // GOUnlock.SetActive(false);
            // GOLock.SetActive(true);
            return;
        }
        gameObject.SetActive(true);
        GOUnlock.SetActive(true);
        GOLock.SetActive(false);
        TryShowNoti();
    }

    private void Update()
    {
        if (!ShipRaceManager.Instance.IsInitialize) return;
        if (!ShipRaceManager.Instance.IsEventUnlock) return;
        // The join window closing is a clock event, not a state change, so it is caught here
        if (IsEventClosedForRun())
        {
            gameObject.SetActive(false);
            return;
        }

        // The old run is only held open so the player can acknowledge its result: counting down to
        // a run that already ended reads as wrong, so the timer waits for the new one to open
        if (GOTime != null) GOTime.SetActive(!ShipRaceManager.Instance.IsRunExpired);
        if (ShipRaceManager.Instance.IsRunExpired) return;

        TxtTimeEndEvent.SetText(ShipRaceManager.Instance.RunTimeRemaining.TimeSpanToStringUI());
    }

    /// <summary>Dot for anything waiting on the player: an unclaimed win, or a loss to acknowledge.</summary>
    /// <summary>
    /// True once this run has nothing left to offer: all stages won, or so little time left that a
    /// new race could not reach its own deadline. A pending win or loss keeps the button up, since
    /// the player still has a result to see.
    /// </summary>
    private bool IsEventClosedForRun()
    {
        ShipRaceManager manager = ShipRaceManager.Instance;
        // A pending result keeps the button up: the run stays open until the player acknowledges it
        if (manager.NeedShowWin || manager.NeedShowLose) return false;
        if (manager.IsRunning) return false;
        return manager.IsAllStageWon;
    }

    private void TryShowNoti()
    {
        // The run can close while the button is on screen (claiming the last stage, or the clock
        // running down), so the whole button is re-evaluated here rather than only the dot
        if (IsEventClosedForRun())
        {
            gameObject.SetActive(false);
            return;
        }

        if (GONoti == null) return;
        bool isShowNoti = ShipRaceManager.Instance.NeedShowWin || ShipRaceManager.Instance.NeedShowLose;
        GONoti.SetActive(isShowNoti);
    }

    /// <summary>
    /// One entry point, four outcomes: unclaimed win or pending loss open the stage board in that
    /// state, a race in progress opens the live board, and anything else opens the join popup.
    /// </summary>
    private async UniTask OnClick()
    {
        ShipRaceManager manager = ShipRaceManager.Instance;
        if (!manager.IsInitialize) return;
        int stage = manager.CurrentStage;

        if (manager.NeedShowWin)
        {
            await ShipRaceModalHelper.ShowStageModal(stage, ShipRaceModalState.ShowWinStage);
            return;
        }

        if (manager.NeedShowLose)
        {
            await ShipRaceModalHelper.ShowStageModal(stage, ShipRaceModalState.ShowLoseStage);
            return;
        }

        if (manager.IsRunning)
        {
            await ShipRaceModalHelper.ShowStageModal(stage, ShipRaceModalState.ShowCurrentStage);
            return;
        }

        await ShipRaceModalHelper.ShowJoinModal(stage);
    }

    private void OnClickBtnLock()
    {
        UIManager.Instance.ShowInfo($"Reach Level {ShipRaceRemoteConfig.Instance.LevelUnlock} to unlock Tidal Dash!");
    }
}
