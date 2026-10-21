using Cysharp.Threading.Tasks;
using LitMotion;
using Profile.ProfileItem;
using TMPro;
using UnityEngine;

/// <summary>
/// One racer card on the end game placement board. Cards do not own a rank: the board hands them a
/// slot to sit in, so re-ranking is just a move between slots.
/// </summary>
public class UIBoatRacePlacement : MonoBehaviour
{
    [field: SerializeField] public UserAvatarFrame AvatarFrame { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtName { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtPoint { get; private set; }
    [field: SerializeField] public GameObject GOTop1 { get; private set; }
    [field: SerializeField] public GameObject GOTop2 { get; private set; }
    [field: SerializeField] public GameObject GOTop3 { get; private set; }
    [field: SerializeField] public GameObject GOTop4 { get; private set; }
    [field: SerializeField] public GameObject GOTop5 { get; private set; }
    [field: SerializeField] public GameObject GOUser { get; private set; }
    [field: SerializeField] public GameObject GOBot { get; private set; }

    private RectTransform _rect;
    private int _target = 1;
    private MotionHandle _moveHandle;

    public RectTransform Rect => _rect != null ? _rect : _rect = transform as RectTransform;

    /// <summary>
    /// Player card: name and profile from PlayerData. Bot card: from the saved bot, so its face
    /// matches the one racing on the stage board.
    /// </summary>
    public void Init(ShipRaceRacer racer, int point, int target)
    {
        if (racer == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _target = Mathf.Max(1, target);
        bool isUser = racer.IsPlayer;
        if (GOUser != null) GOUser.SetActive(isUser);
        if (GOBot != null) GOBot.SetActive(!isUser);

        if (isUser)
        {
            if (TxtName != null) TxtName.SetText(PlayerData.Instance.UserName.Value);
            if (AvatarFrame != null)
            {
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
    }

    /// <summary>Reads as progress against the stage goal, e.g. "2/5".</summary>
    public void SetPoint(int point)
    {
        if (TxtPoint == null) return;
        TxtPoint.SetText("{0}/{1}", Mathf.Clamp(point, 0, _target), _target);
    }

    /// <summary>Medals for the top three; 4th and 5th carry none.</summary>
    public void SetPlace(int place)
    {
        if (GOTop1 != null) GOTop1.SetActive(place == 1);
        if (GOTop2 != null) GOTop2.SetActive(place == 2);
        if (GOTop3 != null) GOTop3.SetActive(place == 3);
        if (GOTop4 != null) GOTop4.SetActive(place == 4);
        if (GOTop5 != null) GOTop5.SetActive(place == 5);
    }

    /// <summary>Drops the card straight into a slot, no animation.</summary>
    public void SetSlot(RectTransform slot)
    {
        _moveHandle.TryCancel();
        if (slot == null || Rect == null) return;
        Rect.anchoredPosition = slot.anchoredPosition;
    }

    /// <summary>
    /// Slides from one slot to another while the point counts up. Slots and cards share a parent,
    /// so anchoredPosition is directly comparable - no world space conversion needed.
    /// </summary>
    public async UniTask PlayMove(RectTransform fromSlot, RectTransform toSlot, int fromPoint, int toPoint, float duration)
    {
        _moveHandle.TryCancel();
        if (Rect == null || fromSlot == null || toSlot == null) return;

        Vector2 fromPos = fromSlot.anchoredPosition;
        Vector2 toPos = toSlot.anchoredPosition;
        Rect.anchoredPosition = fromPos;
        SetPoint(fromPoint);

        if (duration <= 0f)
        {
            Rect.anchoredPosition = toPos;
            SetPoint(toPoint);
            return;
        }

        _moveHandle = LMotion.Create(0f, 1f, duration)
            .WithEase(Ease.OutQuad)
            .WithOnComplete(() =>
            {
                Rect.anchoredPosition = toPos;
                // Snap: the eased tween can round the last frame short
                SetPoint(toPoint);
            })
            .Bind(progress =>
            {
                Rect.anchoredPosition = Vector2.Lerp(fromPos, toPos, progress);
                SetPoint(Mathf.RoundToInt(Mathf.Lerp(fromPoint, toPoint, progress)));
            })
            .AddTo(this);
        await _moveHandle.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    private void OnDisable()
    {
        _moveHandle.TryCancel();
    }
}
