using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Helper;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// End game placement board for Boat Race. Shows the five racers ranked left to right and, after a
/// level win, slides each card from the rank the player last saw to its new one while the points
/// count up.
/// Cards and slots must share a parent (Content) so their anchoredPositions are comparable.
/// </summary>
public class UIEndGameBoatRaceProcess : MonoBehaviour
{
    [field: SerializeField] public MMF_Player Anim { get; private set; }
    [field: SerializeField] public GameObject Root { get; private set; }
    [field: SerializeField] public UIBoatRacePlacement[] Placements { get; private set; }
    [Tooltip("Rank slots, left to right: element 0 is 1st place")]
    [field: SerializeField] public RectTransform[] Slots { get; private set; }
    [Tooltip("Stage backgrounds, element 0 is stage 0")]
    [field: SerializeField] public GameObject[] StageBackgrounds { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtTimeEndRun { get; private set; }
    [field: SerializeField] public float DelayBeforeMove { get; private set; } = 0.6f;
    [field: SerializeField] public float MoveDuration { get; private set; } = 0.5f;

    private readonly List<ShipRaceRacer> _lastShownOrder = new();
    private readonly List<ShipRaceRacer> _currentOrder = new();

    private void Start()
    {
        Root.SetActive(false);
        if (!ShipRaceManager.Instance.IsInitialize) return;
        if (!ShipRaceManager.Instance.IsEventUnlock) return;
        // Only a race the player actually joined has a board to show
        if (ShipRaceManager.Instance.Status == ShipRaceStatus.NotStarted) return;
        Init();
        PlayAnim().Forget();
    }

    private void Update()
    {
        if (TxtTimeEndRun == null) return;
        if (!ShipRaceManager.Instance.IsInitialize) return;
        TimeSpan timeLeft = ShipRaceManager.Instance.RunTimeRemaining;
        if (timeLeft.TotalSeconds <= 0d)
        {
            // The run is over and only waiting to be acknowledged: a ticking clock, or a
            // negative one, would both read as wrong here
            TxtTimeEndRun.SetText("Finished");
            return;
        }
        TxtTimeEndRun.SetText(ShipRaceManager.Instance.RunTimeRemaining.TimeSpanToStringUI());
    }

    private void Init()
    {
        ShowStageBackground();
        int target = ShipRaceManager.Instance.CurrentTarget;
        _lastShownOrder.Clear();
        _lastShownOrder.AddRange(ShipRaceManager.Instance.GetRacersRankedByLastShownEndGame());
        _currentOrder.Clear();
        _currentOrder.AddRange(ShipRaceManager.Instance.GetRacers());

        // Cards are bound in the OLD order and parked in their old slots, so the move that follows
        // reads as a re-ranking rather than a redraw
        for (int i = 0; i < Placements.Length; i++)
        {
            UIBoatRacePlacement placement = Placements[i];
            if (placement == null) continue;
            if (i >= _lastShownOrder.Count)
            {
                placement.gameObject.SetActive(false);
                continue;
            }

            ShipRaceRacer racer = _lastShownOrder[i];
            placement.Init(racer, ShipRaceManager.Instance.GetLastShownPointEndGame(racer), target);
            placement.SetPlace(i + 1);
            placement.SetSlot(GetSlot(i));
        }
    }

    private void ShowStageBackground()
    {
        if (StageBackgrounds == null) return;
        int stage = ShipRaceManager.Instance.CurrentStage;
        for (int i = 0; i < StageBackgrounds.Length; i++)
        {
            if (StageBackgrounds[i] == null) continue;
            StageBackgrounds[i].SetActive(i == stage);
        }
    }

    private RectTransform GetSlot(int index)
    {
        if (Slots == null || Slots.Length == 0) return null;
        return Slots[Mathf.Clamp(index, 0, Slots.Length - 1)];
    }

    private async UniTask PlayAnim()
    {
        Root.SetActive(true);
        if (Anim != null) Anim.PlayFeedbacks();
        await UniTask.Delay(Mathf.RoundToInt(DelayBeforeMove * 1000),
            cancellationToken: this.GetCancellationTokenOnDestroy());

        List<UniTask> moves = new();
        for (int i = 0; i < Placements.Length; i++)
        {
            UIBoatRacePlacement placement = Placements[i];
            if (placement == null || !placement.gameObject.activeSelf) continue;
            if (i >= _lastShownOrder.Count) continue;

            ShipRaceRacer racer = _lastShownOrder[i];
            int newIndex = _currentOrder.FindIndex(current => IsSameRacer(current, racer));
            if (newIndex < 0) newIndex = i;

            // Cards move together, so overtakes are visible as they cross
            moves.Add(placement.PlayMove(
                GetSlot(i),
                GetSlot(newIndex),
                ShipRaceManager.Instance.GetLastShownPointEndGame(racer),
                GetCurrentPoint(racer),
                MoveDuration));
        }

        if (moves.Count > 0) await UniTask.WhenAll(moves);

        // Medals are applied after the move: mid-flight a card would be wearing its old rank
        ApplyPlaces();
        ShipRaceManager.Instance.MarkRacersShownEndGame();
    }

    private void ApplyPlaces()
    {
        for (int i = 0; i < Placements.Length; i++)
        {
            UIBoatRacePlacement placement = Placements[i];
            if (placement == null || !placement.gameObject.activeSelf) continue;
            if (i >= _lastShownOrder.Count) continue;

            ShipRaceRacer racer = _lastShownOrder[i];
            int newIndex = _currentOrder.FindIndex(current => IsSameRacer(current, racer));
            // 4th and 5th get no medal: SetPlace only lights 1, 2 and 3
            placement.SetPlace(newIndex < 0 ? i + 1 : newIndex + 1);
        }
    }

    private static int GetCurrentPoint(ShipRaceRacer racer)
    {
        return racer == null ? 0 : racer.Point;
    }

    /// <summary>
    /// The two lists are rebuilt separately, so the rows are different objects: the player row is
    /// matched by its flag and bots by the saved data they wrap, never by name.
    /// </summary>
    private static bool IsSameRacer(ShipRaceRacer a, ShipRaceRacer b)
    {
        if (a == null || b == null) return false;
        if (a.IsPlayer || b.IsPlayer) return a.IsPlayer && b.IsPlayer;
        return a.Bot == b.Bot;
    }
}
