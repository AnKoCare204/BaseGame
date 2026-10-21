using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Camera gameplay: raycast từ vị trí chạm, đổi vị trí màn hình ↔ world ↔ UI (khung AntFood/FishSort).
    /// </summary>
    public class CameraManager : Singleton<CameraManager>
    {
        [field: SerializeField] public Camera MainCamera { get; private set; }
        [field: SerializeField, Tooltip("Tọa độ Z mặt phẳng gameplay, dùng khi đổi điểm chạm sang world")] private float PlaneZ { get; set; }

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float screenHeightWorldCache = -1f;

        protected override void Awake()
        {
            base.Awake();
            if (MainCamera == null) MainCamera = Camera.main;
        }

        private Camera Cam => MainCamera != null ? MainCamera : Camera.main;

        /// <summary>Camera để chiếu vị trí tutorial.</summary>
        public Camera GetTutCamera() => Cam;

        public bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
        }

        public void SetCameraPosition(Vector3 worldPosition)
        {
            Camera cam = Cam;
            if (cam == null) return;
            cam.transform.position = worldPosition;
        }

        private static Vector2 PointerScreen
        {
            get
            {
                if (InputManager.Instance != null) return InputManager.Instance.PointerPosition;
                UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            }
        }

        /// <summary>Collider 3D gần camera nhất dưới một điểm màn hình, lọc theo layer.</summary>
        public bool TryRaycast(Vector2 screenPoint, LayerMask mask, out RaycastHit hit)
        {
            hit = default;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPoint);
            int count = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity, mask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= best) continue;
                best = hits[i].distance;
                hit = hits[i];
            }

            return count > 0;
        }

        /// <summary>Collider 2D dưới một điểm màn hình (game 2D dùng Physics2D).</summary>
        public Collider2D OverlapPoint2D(Vector2 screenPoint, LayerMask mask)
        {
            Camera cam = Cam;
            if (cam == null) return null;
            Vector3 world = cam.ScreenToWorldPoint(screenPoint);
            return Physics2D.OverlapPoint(world, mask);
        }

        /// <summary>Điểm chạm hiện tại chiếu lên mặt phẳng gameplay z = PlaneZ.</summary>
        public bool TryGetMousePosition(out Vector3 point) => TryGetWorldPoint(PointerScreen, out point);

        public bool TryGetWorldPoint(Vector2 screenPoint, out Vector3 point)
        {
            point = default;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPoint);
            Plane plane = new(Vector3.forward, new Vector3(0f, 0f, PlaneZ));
            if (!plane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>Đặt một RectTransform UI trùng vị trí màn hình của một điểm world (canvas overlay hoặc camera).</summary>
        public void SetUIPositionFromWorldTarget(Transform uiTarget, Vector3 worldPosition)
        {
            Camera cam = Cam;
            if (uiTarget == null || cam == null) return;
            RectTransform rect = uiTarget as RectTransform;
            RectTransform parent = uiTarget.parent as RectTransform;
            Vector2 screen = cam.WorldToScreenPoint(worldPosition);
            if (rect == null || parent == null)
            {
                uiTarget.position = new Vector3(screen.x, screen.y, uiTarget.position.z);
                return;
            }

            Canvas canvas = uiTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            {
                rect.localPosition = new Vector3(local.x, local.y, rect.localPosition.z);
            }
        }

        /// <summary>Vị trí world (trên mặt phẳng z của referenceWorldPos) tương ứng với một RectTransform UI, vd nút booster.</summary>
        public Vector3 GetWorldPositionForBooster(Transform uiTarget, Vector3 referenceWorldPos)
        {
            Camera cam = Cam;
            if (uiTarget == null || cam == null) return referenceWorldPos;
            Canvas canvas = uiTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, uiTarget.position);
            Ray ray = cam.ScreenPointToRay(screen);
            Plane plane = new(Vector3.forward, new Vector3(0f, 0f, referenceWorldPos.z));
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : referenceWorldPos;
        }

        public float ScreenHeightWorld(Vector3 at)
        {
            if (screenHeightWorldCache > 0)
            {
                return screenHeightWorldCache;
            }
            Camera cam = Cam;
            float pixelsPerUnit = (cam.WorldToScreenPoint(at + Vector3.up) - cam.WorldToScreenPoint(at)).magnitude;
            screenHeightWorldCache = pixelsPerUnit > 1e-3f ? cam.pixelHeight / pixelsPerUnit : 10f;
            return screenHeightWorldCache;
        }
    }
}
