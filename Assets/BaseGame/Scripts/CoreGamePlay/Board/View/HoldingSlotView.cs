using LitMotion;
using UnityEngine;

/// <summary>
/// Một ô chờ (HoldingSlot.prefab), Level sinh động kiểu QueueHolder của WordSort. Logic ở HoldingEntry của BoardModel.
/// Cá trong ô đứng bằng FishSchool như trong bể nhưng sức chứa 1: một điểm duy nhất ngay tâm Anchor, không đổi chỗ (ShuffleSingle
/// tắt), vẫn trôi nhẹ và nhấp nhô tại chỗ, luôn nhìn về bên phải (RestFacing = Right). Cá bay tới: PrepareArrival giữ điểm và trả
/// về anchor bám theo điểm để cá bay thẳng tới; Place đưa cá vào đàn (con của Anchor, đi theo ô khi dồn hàng), giữ tư thế lúc hạ rồi
/// quay dần về nhìn nghiêng. Take rút cá khỏi đàn cho bên gọi bay đi.
/// </summary>
public class HoldingSlotView : MonoBehaviour
{
    [field: SerializeField] public int Index { get; private set; }
    [field: SerializeField] public Transform Anchor { get; private set; }
    [field: SerializeField] public SpriteRenderer Body { get; private set; }
    [field: SerializeField] public SpriteRenderer Warning { get; private set; }

    [field: SerializeField, Min(0.05f), Tooltip("Bán kính hình cầu đặt cá quanh Anchor (đơn vị local của Anchor); chỉ cần đủ chứa thân cá đã thu nhỏ (HoldingFishScale). Gizmo cầu xanh khi chọn ô")]
    public float SwimRadius { get; private set; } = 0.5f;

    [field: SerializeField, Tooltip("Đàn cá của ô: 1 điểm ở tâm Anchor, không đổi chỗ, trôi nhẹ và nhấp nhô tại chỗ, luôn nhìn phải")]
    public FishSchool FishSchool { get; private set; } = FishSchool.ForHolding();
    [field: SerializeField] public FeelAnimation HideAnimation { get; private set; }
    public Fish Fish { get; private set; }
    public bool IsOccupied => Fish != null;

    /// <summary>Đang trượt tới vị trí mới trong hàng (dồn hàng khi thêm ô).</summary>
    public bool IsMoving { get; private set; }

    private MotionHandle moveHandle;
    private MotionHandle denyHandle;
    private Color bodyColor = Color.white;   // màu gốc của Body, nhớ ở lần SetDeny đầu
    private bool bodyColorSaved;

    private static GamePlayGlobalConfig Config => GamePlayGlobalConfig.Instance;

    /// <summary>Tâm đàn cá và cha của cá trong ô: Anchor, trống thì chính ô.</summary>
    public Transform AnchorTransform => Anchor != null ? Anchor : transform;

    private void Awake()
    {
        FishSchool.Capacity = 1;
        FishSchool.Initialize(AnchorTransform);
    }

    private void LateUpdate()
    {
        FishSchool.Tick(SwimRadius, Time.deltaTime);
    }

    private void OnDestroy()
    {
        moveHandle.TryCancel();
        denyHandle.TryCancel();
    }

    public void Setup(int index)
    {
        Index = index;
        Clear();
        SetWarning(false);
    }

    /// <summary>Giữ trước điểm ở tâm cho con cá sắp bay tới và trả về anchor bám theo điểm đó (cá bay thẳng tới, ô dồn hàng thì anchor đi theo).</summary>
    public Transform PrepareArrival(Fish fish)
    {
        float scale = Config != null ? Config.HoldingFishScale : 1f;
        Transform anchor = FishSchool.ReserveArrival(fish, scale, SwimRadius);
        return anchor != null ? anchor : AnchorTransform;
    }

    /// <summary>Cá đã bay tới anchor: vào đàn tại điểm đã giữ, làm con của Anchor, giữ tư thế lúc hạ rồi quay dần về nhìn phải.</summary>
    public void Place(Fish fish, Transform anchor)
    {
        if (fish == null) return;
        Fish = fish;
        FishSchool.Arrive(fish, anchor, SwimRadius);
    }

    /// <summary>Rút cá khỏi ô (tự thu / Magnet): rời đàn, giữ nguyên GameObject cho bên gọi bay đi.</summary>
    public Fish Take()
    {
        Fish fish = Fish;
        Fish = null;
        if (fish != null) FishSchool.Remove(fish);
        return fish;
    }

    public void SetWarning(bool value)
    {
        if (Warning != null) Warning.gameObject.SetActive(value);
    }

    private int baseLayer = -1;   // layer gốc của Body, nhớ ở lần SetTut đầu

    /// <summary>FTUE: đưa Body / Warning lên layer TutMesh để vẽ trên lớp tối (on), trả layer gốc (off). Cá trong ô làm riêng (Fish.SetTut).</summary>
    public void SetTut(bool on)
    {
        if (baseLayer < 0) baseLayer = Body != null ? Body.gameObject.layer : gameObject.layer;
        int tutLayer = LayerMask.NameToLayer("TutMesh");
        int layer = on && tutLayer >= 0 ? tutLayer : baseLayer;
        if (Body != null) Body.gameObject.layer = layer;
        if (Warning != null) Warning.gameObject.layer = layer;
    }

    /// <summary>
    /// Nhuộm đỏ "không đủ chỗ" (FishSchoolBubble, BoardManager.SetHoldingDeny): Body chuyển sang color (on) hoặc trả về màu gốc (off)
    /// trong duration giây (0 = đặt ngay). Màu gốc nhớ ở lần gọi đầu. Gọi thừa vô hại.
    /// </summary>
    public void SetDeny(bool on, Color color, float duration)
    {
        if (Body == null) return;
        if (!bodyColorSaved)
        {
            bodyColor = Body.color;
            bodyColorSaved = true;
        }

        Color target = on ? color : bodyColor;
        denyHandle.TryCancel();
        if (duration <= 0f)
        {
            Body.color = target;
            return;
        }

        SpriteRenderer body = Body;
        denyHandle = LMotion.Create(body.color, target, duration).Bind(c => body.color = c).AddTo(this);
    }

    /// <summary>Trượt tới localPosition trong duration giây (0 = đặt ngay), kiểu QueueHolder.MoveTo của WordSort.</summary>
    public void MoveTo(Vector3 localPosition, float duration)
    {
        moveHandle.TryCancel();
        if (duration <= 0f)
        {
            transform.localPosition = localPosition;
            IsMoving = false;
            return;
        }

        Vector3 from = transform.localPosition;
        IsMoving = true;
        moveHandle = LMotion.Create(0f, 1f, duration)
            .WithEase(Ease.InOutSine)
            .WithOnComplete(() => IsMoving = false)
            .Bind(u => { if (this != null) transform.localPosition = Vector3.LerpUnclamped(from, localPosition, u); })
            .AddTo(this);
    }

    private Vector3 bodyScale, warningScale;   // scale gốc của Body / Warning, nhớ ở lần PlayReveal đầu
    private bool revealScaleSaved;

    /// <summary>
    /// FTUE: hàng ô chờ hiện ra, phần hình (Body, Warning) bung từ 0 sau delay giây. Không scale ô hay Graphic: Anchor (tâm đàn cá) phải
    /// giữ đúng scale, cá bấm ngay lúc này giữ chỗ theo lossyScale của Anchor (FishSchool.ReserveArrival), scale 0 thì cá hạ ra sau camera.
    /// </summary>
    public void PlayReveal(float delay)
    {
        if (!revealScaleSaved)
        {
            revealScaleSaved = true;
            if (Body != null) bodyScale = Body.transform.localScale;
            if (Warning != null) warningScale = Warning.transform.localScale;
        }

        if (Body != null) PopIn(Body.transform, bodyScale, delay);
        if (Warning != null) PopIn(Warning.transform, warningScale, delay);
    }

    private static void PopIn(Transform t, Vector3 scale, float delay)
    {
        t.localScale = Vector3.zero;
        LMotion.Create(Vector3.zero, scale, 0.35f)
            .WithEase(Ease.OutBack)
            .WithDelay(delay)
            .Bind(x => { if (t != null) t.localScale = x; })
            .AddTo(t.gameObject);
    }

    public void Clear()
    {
        FishSchool.Clear();
        if (Fish != null) Destroy(Fish.gameObject);
        Fish = null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform zone = AnchorTransform;
        if (zone == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(zone.position, zone.rotation, zone.lossyScale * SwimRadius);
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
        Gizmos.matrix = Matrix4x4.identity;
        FishSchool.DrawGizmos();
    }

    public void Hide()
    {
        HideAnimation.Play();
    }
}
