using Cysharp.Threading.Tasks;
using LitMotion;
using Profile.ProfileItem;
using Spine.Unity;
using TMPro;
using UnityEngine;

/// <summary>
/// One lane of the race board: a ship travelling from TfStartShip to TfEndShip, where the end point
/// is the stage target. The same prefab is used for the player and for bots - only the visual and
/// the name differ.
/// </summary>
public class ShipPath : MonoBehaviour
{
    [field: SerializeField] public Transform ShipObject { get; private set; }
    [field: SerializeField] public Transform TfStartShip { get; private set; }
    [field: SerializeField] public Transform TfEndShip { get; private set; }
    [field: SerializeField] public UserAvatarFrame  AvatarFrame { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtName { get; private set; }
    [field: SerializeField] public GameObject GOTop1 { get; private set; }
    [field: SerializeField] public GameObject GOCurPoint { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtCurPoint { get; private set; }
    [field: SerializeField] public float MoveDurationPerPoint { get; private set; } = 0.35f;
    [field: SerializeField] public float MaxMoveDuration { get; private set; } = 1.2f;
    [Tooltip("Ease in and out: the ship pulls away slowly, runs, then settles instead of snapping")]
    [field: SerializeField] public Ease MoveEase { get; private set; } = Ease.InOutCubic;
    [Tooltip("Optional hand-authored curve. Overrides MoveEase when it has keys")]
    [field: SerializeField] public AnimationCurve MoveCurve { get; private set; }
    [field: SerializeField] public SkeletonGraphic SkeletonGraphic { get; private set; }

    private int _target;
    private bool _isUser;
    private MotionHandle _moveHandle;
    private RectTransform _shipRect;
    private RectTransform _shipParentRect;
    private Vector2 _startAnchored;
    private Vector2 _endAnchored;
    private bool _isAnchorResolved;

    /// <summary>
    /// Reads the two markers once the canvas has laid them out and converts them into the ship's
    /// own anchored space.
    /// TfStartShip is anchored to the bottom and TfEndShip to the top, so their anchoredPositions
    /// are measured against different reference corners and can not be lerped directly. Their
    /// resolved world positions can, which is what this converts.
    /// </summary>
    private void ResolveAnchors()
    {
        if (ShipObject == null || TfStartShip == null || TfEndShip == null) return;
        _shipRect = ShipObject as RectTransform;
        _shipParentRect = ShipObject.parent as RectTransform;
        if (_shipRect == null || _shipParentRect == null) return;

        // Layout runs at end of frame: without this the rects are still at their prefab size and
        // both markers resolve to the same point
        Canvas.ForceUpdateCanvases();
        _startAnchored = ToShipSpace(TfStartShip);
        _endAnchored = ToShipSpace(TfEndShip);
        _isAnchorResolved = true;
    }

    private Vector2 ToShipSpace(Transform marker)
    {
        return _shipParentRect.InverseTransformPoint(marker.position);
    }

    /// <summary>
    /// Player lane: name and profile come from PlayerData, and the frame keeps following it live.
    /// Bot lane: everything comes from the saved bot, so its face and name stay put all race.
    /// </summary>
    public void Init(ShipRaceRacer racer, int target, int point)
    {
        if (racer == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _target = Mathf.Max(1, target);
        _isUser = racer.IsPlayer;
        bool isUser = _isUser;
        // Raised later by ShowTop1, once the ships have finished moving
        if (GOTop1 != null) GOTop1.SetActive(false);

        if (isUser)
        {
            if (TxtName != null) TxtName.SetText(PlayerData.Instance.UserName.Value);
            if (AvatarFrame != null)
            {
                // Marks it as the local player so the frame tracks profile changes on its own
                AvatarFrame.SetIsCurrentUser(true);
                AvatarFrame.SetUpProfile(
                    PlayerData.Instance.AvatarIndex.Value,
                    PlayerData.Instance.AvatarFrameIndex.Value,
                    PlayerData.Instance.BadgeIndex.Value);
            }
        }
        else
        {
            if (TxtName != null) TxtName.SetText(racer.Name);
            if (AvatarFrame != null)
            {
                AvatarFrame.SetIsCurrentUser(false);
                ShipRaceBotData bot = racer.Bot;
                AvatarFrame.SetUpProfile(
                    bot?.AvatarIndex ?? 0,
                    bot?.FrameIndex ?? 0,
                    bot?.BadgeIndex ?? -1);
            }
        }

        SetPoint(point);
        SkeletonGraphic.AnimationState.ClearTrack(0);
        SkeletonGraphic.AnimationState.SetAnimation(0, "idle", true);
    }

    /// <summary>
    /// Marks this lane as the race winner. Ignored on the player's lane: a player win is shown by
    /// the win panel and its reward, not by a badge on the board.
    /// </summary>
    public void ShowTop1(bool isShow)
    {
        if (GOTop1 == null) return;
        if (_isUser) return;
        GOTop1.SetActive(isShow);
    }

    public void SetPoint(int point)
    {
        _moveHandle.TryCancel();
        SetProgress(GetProgress(point));
        SetCurPoint(point);
    }

    /// <summary>
    /// Counter follows the ship. It disappears at the target: a racer that finished is shown by its
    /// ship sitting on the finish line, not by a number.
    /// </summary>
    private void SetCurPoint(int point)
    {
        int clamped = Mathf.Clamp(point, 0, _target);
        if (GOCurPoint != null) GOCurPoint.SetActive(clamped < _target);
        if (TxtCurPoint != null) TxtCurPoint.SetText("{0}", clamped);
    }

    /// <summary>
    /// Sails from <paramref name="fromPoint"/> to <paramref name="toPoint"/>. Duration scales with
    /// the distance travelled so a ship that gained 3 points does not snap as fast as one that
    /// gained 1, capped so a long catch-up stays watchable.
    /// </summary>
    public async UniTask PlayMove(int fromPoint, int toPoint)
    {
        _moveHandle.TryCancel();
        float fromProgress = GetProgress(fromPoint);
        float toProgress = GetProgress(toPoint);
        SetProgress(fromProgress);
        SetCurPoint(fromPoint);
        if (Mathf.Approximately(fromProgress, toProgress)) return;

        float duration = Mathf.Min(MaxMoveDuration, MoveDurationPerPoint * Mathf.Abs(toPoint - fromPoint));
        if (duration <= 0f)
        {
            SetProgress(toProgress);
            SetCurPoint(toPoint);
            return;
        }

        SkeletonGraphic.AnimationState.SetAnimation(0, "move", true);
        // An authored MoveCurve wins over the built in ease; both go through LitMotion's own easing
        bool useCurve = MoveCurve != null && MoveCurve.length > 0;
        var builder = LMotion.Create(fromProgress, toProgress, duration);
        _moveHandle = (useCurve ? builder.WithEase(MoveCurve) : builder.WithEase(MoveEase))
            .WithOnComplete(() =>
            {
                SkeletonGraphic.AnimationState.SetAnimation(0, "idle", true);
                // Snap to the exact value: an eased tween can leave the last frame a hair short
                SetProgress(toProgress);
                SetCurPoint(toPoint);
            })
            .Bind(progress =>
            {
                SetProgress(progress);
                // Counter ticks up with the ship instead of jumping at the end
                SetCurPoint(Mathf.RoundToInt(progress * _target));
            })
            .AddTo(this);
        await _moveHandle.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    private float GetProgress(int point)
    {
        return Mathf.Clamp01((float)point / _target);
    }

    /// <summary>
    /// Drives anchoredPosition, never position or localPosition. For a RectTransform those two are
    /// outputs of the layout system: anything written to them is recomputed from anchoredPosition on
    /// the next layout pass, which is what made the ship snap back to its authored spot.
    /// </summary>
    private void SetProgress(float progress)
    {
        if (!_isAnchorResolved) ResolveAnchors();
        if (_shipRect == null) return;
        _shipRect.anchoredPosition = Vector2.Lerp(_startAnchored, _endAnchored, progress);
    }

    /// <summary>Re-reads the markers, e.g. after the modal finished entering or the lane resized.</summary>
    public void RefreshAnchors()
    {
        _isAnchorResolved = false;
        ResolveAnchors();
    }

    private void OnDisable()
    {
        _moveHandle.TryCancel();
    }
}
