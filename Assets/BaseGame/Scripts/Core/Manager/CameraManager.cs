using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Raycast từ vị trí chạm tới cá / bể và đổi vị trí world sang UI (khung AntFood).
    /// Cá / ô cắm bể được tra từ collider qua bảng đăng ký (Fish.TryGetByCollider, TankSlotView.TryGetByCollider), không GetComponent.
    /// </summary>
    public class CameraManager : Singleton<CameraManager>
    {
        [field: SerializeField] public Camera MainCamera { get; private set; }
        [field: SerializeField] private LayerMask WhatIsFish { get; set; }
        [field: SerializeField] private LayerMask WhatIsTank { get; set; }
        [field: SerializeField, Tooltip("Tọa độ Z mặt phẳng bóng, dùng khi đổi điểm chạm sang world")] private float PlaneZ { get; set; }

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float screenHeightWorldCache = -1f;
        protected override void Awake()
        {
            base.Awake();
            if (MainCamera == null) MainCamera = Camera.main;
            if (WhatIsFish.value == 0) WhatIsFish = LayerMask.GetMask("Fish");
        }

        private Camera Cam => MainCamera != null ? MainCamera : Camera.main;

        /// <summary>Camera để chiếu vị trí tutorial (FishSort chỉ có một camera).</summary>
        public Camera GetTutCamera() => Cam;
        
        /// <summary>Ported from Mahjong (used by ActivityShowRewardCastleClimb / UIResourceGroupInfo).</summary>
        public bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
        }

        /// <summary>Đặt vị trí world của camera chính. GamePlayRectManager.FitCameraToQueue gọi khi căn camera theo LevelRectQueue của level.</summary>
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

        /// <summary>
        /// Cá dưới con trỏ (gần camera nhất). padding > 0: không trúng thẳng collider nào thì quét thêm hình cầu bán kính padding (m)
        /// dọc tia chạm, để ngón tay che cá nhỏ vẫn bắt được khi đè / rê tay.
        /// </summary>
        public bool TryGetFish(out Fish fish, float padding = 0f)
        {
            fish = null;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(PointerScreen);
            int count = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity, WhatIsFish, QueryTriggerInteraction.Collide);
            if (PickNearestFish(count, out fish) || padding <= 0f) return fish != null;
            count = Physics.SphereCastNonAlloc(ray, padding, hits, Mathf.Infinity, WhatIsFish, QueryTriggerInteraction.Collide);
            return PickNearestFish(count, out fish);
        }

        /// <summary>Cá gần camera nhất dưới một điểm màn hình, cùng quy tắc với <see cref="TryGetFish(out Fish, float)"/>.</summary>
        public bool TryGetFishAt(Vector2 screenPoint, float padding, out Fish fish)
        {
            fish = null;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPoint);
            int count = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity, WhatIsFish, QueryTriggerInteraction.Collide);
            if (PickNearestFish(count, out fish) || padding <= 0f) return fish != null;
            count = Physics.SphereCastNonAlloc(ray, padding, hits, Mathf.Infinity, WhatIsFish, QueryTriggerInteraction.Collide);
            return PickNearestFish(count, out fish);
        }

        /// <summary>Trong count hit đầu của hits, con cá (Fish.TryGetByCollider) có distance nhỏ nhất.</summary>
        private bool PickNearestFish(int count, out Fish fish)
        {
            fish = null;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!Fish.TryGetByCollider(hits[i].collider, out Fish candidate) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                fish = candidate;
            }

            return fish != null;
        }

        /// <summary>Bấm trúng bóng có collider bấm (bóng đá, bóng băng; layer Fish). Gọi sau khi TryGetFish không trúng cá.</summary>
        public bool TryGetBubble(out BubbleView view)
        {
            view = null;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(PointerScreen);
            int count = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity, WhatIsFish, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!BubbleView.TryGetByCollider(hits[i].collider, out BubbleView candidate) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                view = candidate;
            }

            return view != null;
        }

        public bool TryGetTank(out TankSlotView tank)
        {
            tank = null;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(PointerScreen);
            int mask = WhatIsTank.value != 0 ? WhatIsTank.value : Physics.DefaultRaycastLayers;
            int count = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity, mask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!TankSlotView.TryGetByCollider(hits[i].collider, out TankSlotView candidate) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                tank = candidate;
            }

            return tank != null;
        }

        /// <summary>Điểm chạm chiếu lên mặt phẳng bóng z = PlaneZ.</summary>
        public bool TryGetMousePosition(out Vector3 point)
        {
            point = default;
            Camera cam = Cam;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(PointerScreen);
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

        /// <summary>Vị trí world (trên mặt phẳng bóng) tương ứng với một RectTransform UI.</summary>
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
            Camera cam = MainCamera;
            float pixelsPerUnit = (cam.WorldToScreenPoint(at + Vector3.up) - cam.WorldToScreenPoint(at)).magnitude;
            screenHeightWorldCache = pixelsPerUnit > 1e-3f ? cam.pixelHeight / pixelsPerUnit : 10f;
            return screenHeightWorldCache;
        }
    }
}
