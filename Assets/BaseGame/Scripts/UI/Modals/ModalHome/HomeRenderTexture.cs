using UnityEngine;
using UnityEngine.UI;

/// RawImage hiện render texture cá ở màn Home: bật là bảo manager render, tắt là dừng.
/// Cũng là cầu nối chạm: nghe UIWaterRipple.RippleSpawned (pixel màn hình), đổi sang toạ độ 0..1 của ảnh (có tính uvRect của RawImage)
/// rồi gọi FishHomeRenderManager.Startle để cá giật mình / tò mò. UIWaterRipple không cần biết gì về cá.
public class HomeRenderTexture : MonoBehaviour
{
    [field: SerializeField] private RawImage RenderTexture {get; set;}
    [field: SerializeField, Tooltip("Gợn sóng trên nền Home; mỗi gợn sinh ra được đẩy sang cá trong render texture (để trống = cá không phản ứng)")]
    private UIWaterRipple Ripple {get; set;}

    private FishHomeRenderManager manager; // giữ lại để OnDisable không gọi Instance: lúc thoát game manager đã hủy, Instance sẽ tự sinh object rác

    public void Start()
    {
        RenderTexture.texture = FishHomeRenderManager.Instance.RenderTexture;
    }

    private void OnEnable()
    {
        manager = FishHomeRenderManager.Instance;
        manager.StartRender();
        if (Ripple != null) Ripple.RippleSpawned += OnRipple;
    }

    private void OnDisable()
    {
        if (Ripple != null) Ripple.RippleSpawned -= OnRipple;
        if (manager != null) manager.StopRender();
    }

    private void OnRipple(Vector2 screenPosition, bool isTrail)
    {
        if (manager == null || RenderTexture == null) return;
        RectTransform rt = RenderTexture.rectTransform;
        Canvas canvas = RenderTexture.canvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPosition, cam, out Vector2 local)) return;
        manager.Startle(FishHomeSwimMath.LocalToViewport(local, rt.rect, RenderTexture.uvRect), isTrail);
    }
}
