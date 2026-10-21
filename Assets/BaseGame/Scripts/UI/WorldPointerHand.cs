using Core.Manager;
using UnityEngine;

/// <summary>
/// Bàn tay chỉ (animator HandClick) bám theo một vật trên bàn chơi hoặc một RectTransform UI, cập nhật mỗi LateUpdate. Prefab
/// WorldPointerHand.prefab có Canvas overlay riêng, không GraphicRaycaster, mọi Graphic tắt raycastTarget nên không chặn bấm. Dùng
/// chung cho TutorialStage và IdleHintController (spec FTUE đợt 1 mục 13): owner khác nhau thì Hide của bên này không tắt tay của
/// bên kia.
/// </summary>
public class WorldPointerHand : MonoBehaviour
{
    [field: SerializeField, Tooltip("RectTransform bàn tay (con HandClick có Animator)")] public RectTransform Hand { get; private set; }
    [field: SerializeField, Tooltip("Lệch (đơn vị canvas) so với điểm chỉ để đầu ngón tay nằm đúng điểm")] public Vector2 Offset { get; private set; } = new Vector2(30f, -40f);

    private Transform worldTarget;
    private RectTransform uiTarget;
    private object owner;

    public bool IsShowing => Hand != null && Hand.gameObject.activeSelf;

    public void PointAt(Transform target, object by = null)
    {
        worldTarget = target;
        uiTarget = null;
        Show(by);
    }

    public void PointAt(RectTransform target, object by = null)
    {
        uiTarget = target;
        worldTarget = null;
        Show(by);
    }

    /// <summary>Tắt tay. by khác null mà không phải bên đang giữ tay thì bỏ qua.</summary>
    public void Hide(object by = null)
    {
        if (by != null && owner != null && !ReferenceEquals(by, owner)) return;
        worldTarget = null;
        uiTarget = null;
        owner = null;
        if (Hand != null) Hand.gameObject.SetActive(false);
    }

    private void Show(object by)
    {
        owner = by;
        if (Hand == null) return;
        Hand.gameObject.SetActive(true);
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (Hand == null || !Hand.gameObject.activeSelf) return;
        if (worldTarget == null && uiTarget == null)
        {
            // Vật bị huỷ (cá đã bay vào bể, level dọn): tự tắt.
            Hide();
            return;
        }

        Vector2 screen;
        if (worldTarget != null)
        {
            Camera cam = CameraManager.Instance != null ? CameraManager.Instance.GetTutCamera() : Camera.main;
            if (cam == null) return;
            screen = cam.WorldToScreenPoint(worldTarget.position);
        }
        else
        {
            Canvas canvas = uiTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            screen = RectTransformUtility.WorldToScreenPoint(uiCamera, uiTarget.position);
        }

        RectTransform parent = Hand.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local))
            Hand.localPosition = new Vector3(local.x + Offset.x, local.y + Offset.y, 0f);
    }
}
