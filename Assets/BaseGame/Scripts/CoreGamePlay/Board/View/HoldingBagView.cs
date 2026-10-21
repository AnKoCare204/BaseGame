using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;

/// <summary>
/// Túi PackedUp (HoldingBag.prefab), đứng ở cuối hàng ô chờ như một ô đặc biệt: không bấm cá vào được, chứa không giới hạn, chỉ giữ
/// loài (BoardModel.Bag), cá bay vào thì huỷ view, cá nhả ra bể thì sinh lại tại Anchor. Level đặt vị trí bằng MoveTo cùng hàng ô chờ
/// (ô chờ trước, túi sau, cách đều). Show: scale từ 0 khi xuất hiện; HideAndDestroy: co về 0 rồi huỷ khi hết cá. Push (cá vào) và Pop
/// (cá ra) nhún phần Body bằng một MotionHandle, spam thì restart từ scale gốc nên không cộng dồn.
/// </summary>
public class HoldingBagView : MonoBehaviour
{
    [field: SerializeField, Tooltip("Điểm cá bay vào / sinh ra khi nhả (con của túi)")]
    public Transform Anchor { get; private set; }

    [field: SerializeField, Tooltip("Phần nhún khi cá vào / ra (thân túi). Trống = nhún cả túi")]
    public Transform Body { get; private set; }

    [field: SerializeField, Tooltip("Chữ số cá đang có trong túi")]
    public TMP_Text CountText { get; private set; }

    [field: SerializeField, Range(0f, 0.6f), Tooltip("Cá vào: thân túi phình thêm bấy nhiêu phần rồi hồi")]
    public float PushScale { get; private set; } = 0.18f;

    [field: SerializeField, Range(0f, 0.6f), Tooltip("Cá ra: thân túi hụt đi bấy nhiêu phần rồi hồi")]
    public float PopScale { get; private set; } = 0.12f;

    [field: SerializeField, Min(0.05f), Tooltip("Thời gian một nhịp nhún (giây)")]
    public float PulseDuration { get; private set; } = 0.25f;

    /// <summary>Đang trượt tới vị trí mới trong hàng.</summary>
    public bool IsMoving { get; private set; }

    public Transform AnchorTransform => Anchor != null ? Anchor : transform;

    private MotionHandle moveHandle;
    private MotionHandle pulseHandle;
    private MotionHandle scaleHandle;
    private Vector3 bodyBaseScale = Vector3.one;

    private void Awake()
    {
        if (Body == null) Body = transform;
        bodyBaseScale = Body.localScale;
    }

    private void OnDestroy()
    {
        moveHandle.TryCancel();
        pulseHandle.TryCancel();
        scaleHandle.TryCancel();
    }

    public void SetCount(int count)
    {
        if (CountText != null) CountText.text = Mathf.Max(0, count).ToString();
    }

    /// <summary>Xuất hiện: scale cả túi từ 0 lên 1 (vị trí do Level.MoveTo).</summary>
    public void Show(float duration)
    {
        scaleHandle.TryCancel();
        transform.localScale = Vector3.zero;
        scaleHandle = LMotion.Create(0f, 1f, Mathf.Max(0.01f, duration))
            .WithEase(Ease.OutBack)
            .Bind(u => { if (this != null) transform.localScale = Vector3.one * u; })
            .AddTo(this);
    }

    /// <summary>Hết cá: co cả túi về 0 rồi huỷ.</summary>
    public void HideAndDestroy(float duration)
    {
        scaleHandle.TryCancel();
        pulseHandle.TryCancel();
        Vector3 from = transform.localScale;
        scaleHandle = LMotion.Create(1f, 0f, Mathf.Max(0.01f, duration))
            .WithEase(Ease.InQuad)
            .WithOnComplete(() => { if (this != null) Destroy(gameObject); })
            .Bind(u => { if (this != null) transform.localScale = from * u; })
            .AddTo(this);
    }

    /// <summary>Trượt tới localPosition trong duration giây (0 = đặt ngay), cùng kiểu HoldingSlotView.MoveTo.</summary>
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

    /// <summary>Cá vừa bay vào: thân túi phình rồi hồi.</summary>
    public void Push() => Pulse(PushScale);

    /// <summary>Cá vừa nhả ra: thân túi hụt rồi hồi.</summary>
    public void Pop() => Pulse(-PopScale);

    private void Pulse(float amount)
    {
        if (Body == null) return;
        pulseHandle.TryCancel();
        Body.localScale = bodyBaseScale;   // spam: về gốc rồi nhún lại, không cộng dồn
        pulseHandle = LMotion.Create(0f, 1f, PulseDuration)
            .WithOnComplete(() => { if (this != null && Body != null) Body.localScale = bodyBaseScale; })
            .Bind(u =>
            {
                if (this == null || Body == null) return;
                float s = Mathf.Sin(u * Mathf.PI);
                Body.localScale = bodyBaseScale * (1f + amount * s);
            })
            .AddTo(this);
    }

    private readonly Dictionary<Renderer, int> baseLayers = new();

    /// <summary>
    /// FTUE: đưa mọi renderer của túi (kể cả số đếm) lên layer TutMesh để vẽ trên lớp tối (on), trả đúng layer gốc của từng renderer (off).
    /// Layer gốc nhớ ở lần gọi đầu thấy renderer đó.
    /// </summary>
    public void SetTut(bool on)
    {
        int tutLayer = LayerMask.NameToLayer("TutMesh");
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!baseLayers.TryGetValue(renderer, out int baseLayer))
            {
                baseLayer = renderer.gameObject.layer;
                baseLayers[renderer] = baseLayer;
            }

            renderer.gameObject.layer = on && tutLayer >= 0 ? tutLayer : baseLayer;
        }
    }
}