using System;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

/// <summary>
/// Hai rect UI (scale theo màn hình qua CanvasScaler) chia màn hình gameplay:
/// - GamePlayRectQueue (phía trên): khi vào level, FitCameraToQueue dời camera (giữ FOV) để LevelRectQueue của Level nằm lọt trong
///   rect này, lấy trục chặt hơn làm chuẩn và căn giữa cả hai trục.
/// - GamePlayRectWall (phía dưới): chiếu lên mặt phẳng bóng (z = PlaneZ) thành hình chữ nhật world, rồi cấu hình tường SoftBodyShape
///   đặt sẵn (WallShape, box đảo) đúng bằng vùng đó để bóng chỉ sinh và di chuyển bên trong. Màn rộng (tablet, màn ngang) thì bề ngang
///   vùng này bị kẹp về MaxWorldWidth quanh tâm, hai bên chỉ còn nền.
/// Vùng tường tính lại mỗi frame, nên đổi độ phân giải, tỉ lệ màn hình, xoay màn hình hay dời camera đều tự khớp.
/// Gameplay khác dùng WorldRect, ClampToRect, RandomPointInRect, ScreenToPlane.
/// </summary>
[DefaultExecutionOrder(-60)]
public class GamePlayRectManager : Singleton<GamePlayRectManager>
{
    [field: SerializeField, Tooltip("Rect UI phía trên: camera được dời để LevelRectQueue của level nằm lọt trong rect này (FitCameraToQueue)")]
    private RectTransform GamePlayRectQueue { get; set; }

    [field: SerializeField, Tooltip("Rect UI phía dưới: chiếu lên mặt phẳng bóng thành vùng sinh / di chuyển bóng, dựng tường bao quanh")]
    private RectTransform GamePlayRectWall { get; set; }

    [field: SerializeField, Tooltip("Canvas chứa GamePlayRectWall (gán sẵn). Overlay thì không cần camera UI; Screen Space Camera thì lấy worldCamera của canvas")]
    private Canvas GamePlayCanvas { get; set; }

    [field: SerializeField, Tooltip("Camera nhìn bóng. Trống = Camera.main")]
    private Camera WorldCamera { get; set; }

    [field: SerializeField, Tooltip("Tọa độ Z của mặt phẳng bóng (bằng SoftBubble.PlaneZ, thường là 0). Rect UI được chiếu lên mặt phẳng này")]
    private float PlaneZ { get; set; }

    [field: SerializeField, Tooltip("Thu hẹp vùng world thêm (m) ở mỗi cạnh. Âm = nới rộng ra ngoài rect")]
    private float Padding { get; set; }

    [field: SerializeField, Min(0f), Tooltip("Bề ngang tối đa (m) của vùng bóng. Màn rộng hơn (tablet, màn ngang) thì vùng bị kẹp lại quanh tâm, hai bên chỉ còn nền, tường vẫn vô hình. 0 = không giới hạn")]
    private float MaxWorldWidth { get; set; }

    [field: SerializeField, Tooltip("Dựng tường va chạm (SoftBodyShape box đảo) bao quanh vùng rect bằng WallShape")]
    private bool BuildWalls { get; set; } = true;

    [field: SerializeField, Tooltip("SoftBodyShape đặt sẵn làm con của manager (GamePlayRectWalls); mỗi lần rect đổi được Configure thành box đảo đúng vùng chơi")]
    private SoftBodyShape WallShape { get; set; }

    [field: SerializeField, Range(0f, 1f)] private float WallFriction { get; set; } = 0.3f;

    [field: SerializeField, Min(0.1f), Tooltip("Độ dày tường theo trục Z. Bóng đã khóa Z nên chỉ cần lớn hơn đường kính bóng")]
    private float WallDepth { get; set; } = 4f;

    [field: SerializeField, Tooltip("Cạnh trên là giới hạn cứng: mặt bóng chạm tới thì cả bóng dừng nguyên khối, giữ hình cầu (không bẹt như chạm tường). Mặt trên của tường box được kéo lên cao để particle không chạm")]
    private bool RigidTop { get; set; } = true;

    [field: SerializeField, Tooltip("Cạnh dưới là giới hạn cứng (giống RigidTop)")] private bool RigidBottom { get; set; }
    [field: SerializeField, Tooltip("Cạnh trái là giới hạn cứng (giống RigidTop)")] private bool RigidLeft { get; set; }
    [field: SerializeField, Tooltip("Cạnh phải là giới hạn cứng (giống RigidTop)")] private bool RigidRight { get; set; }

    [field: SerializeField, Min(1f), Tooltip("Ở cạnh cứng, mặt tường box được đẩy ra ngoài bấy nhiêu (m) để particle không bao giờ chạm mặt đó")]
    private float RigidWallExtension { get; set; } = 10f;

    /// <summary>Vùng chơi trong world (mặt phẳng XY tại PlaneZ), đã trừ Padding và kẹp bề ngang theo MaxWorldWidth.</summary>
    public Rect WorldRect { get; private set; }

    /// <summary>true khi đã chiếu được rect ít nhất một lần.</summary>
    public bool HasWorldRect { get; private set; }

    public float BubblePlaneZ => PlaneZ;

    /// <summary>Tường va chạm đang dùng (WallShape gán sẵn; null khi chưa gán).</summary>
    public SoftBodyShape Walls => WallShape;

    /// <summary>Bắn khi vùng world thay đổi (đổi độ phân giải, xoay màn hình, đổi rect).</summary>
    public event Action<Rect> OnWorldRectChanged;

    private readonly Vector3[] corners = new Vector3[4];
    private bool wallWarned;
    private SoftBubbleWorld boundWorld;

    [field: SerializeField]
    private Camera[] OverlayCams { get; set; }
    private void OnEnable()
    {
        // Ép canvas layout trước để frame đầu đã có rect đúng cho spawner.
        Canvas.ForceUpdateCanvases();
        Refresh(true);
    }

    private void Update()
    {
        Refresh(false);
        // World có thể xuất hiện sau manager (thứ tự Awake) hoặc đổi Inspector lúc Play: áp lại giới hạn cứng khi cần.
        if (HasWorldRect && (SoftBubbleWorld.Instance != boundWorld || RigidMaskWanted != (boundWorld != null ? boundWorld.RigidMask : 0))) ApplyRigidBounds();
    }

    private void OnDisable()
    {
        if (WallShape != null) WallShape.gameObject.SetActive(false);
        if (boundWorld != null) boundWorld.ClearRigidBounds();
        boundWorld = null;
    }

    private int RigidMaskWanted => BuildWalls ? (RigidLeft ? 1 : 0) | (RigidRight ? 2 : 0) | (RigidBottom ? 4 : 0) | (RigidTop ? 8 : 0) : 0;

    /// <summary>Chiếu lại rect. Trả về true nếu đang có vùng hợp lệ. force = true ép cập nhật tường và bắn event.</summary>
    public bool Refresh(bool force = false)
    {
        if (!TryComputeWorldRect(out Rect rect)) return HasWorldRect;

        bool changed = force || !HasWorldRect || !Approximately(rect, WorldRect);
        if (!changed) return true;

        WorldRect = rect;
        HasWorldRect = true;
        UpdateWalls();
        OnWorldRectChanged?.Invoke(rect);
        return true;
    }

    /// <summary>Chiếu 4 góc của GamePlayRectWall qua camera xuống mặt phẳng z = PlaneZ, lấy bao chữ nhật, trừ Padding rồi kẹp bề ngang về MaxWorldWidth.</summary>
    public bool TryComputeWorldRect(out Rect rect)
    {
        if (!TryProjectRect(GamePlayRectWall, PlaneZ, out rect)) return false;
        rect = Rect.MinMaxRect(rect.xMin + Padding, rect.yMin + Padding, rect.xMax - Padding, rect.yMax - Padding);
        if (MaxWorldWidth > 0f && rect.width > MaxWorldWidth)
        {
            // Giữ tâm X, cắt đều hai bên; trục Y không đổi.
            float half = MaxWorldWidth * 0.5f;
            rect = Rect.MinMaxRect(rect.center.x - half, rect.yMin, rect.center.x + half, rect.yMax);
        }
        return rect.width > 0f && rect.height > 0f;
    }

    /// <summary>Vùng world của GamePlayRectQueue trên mặt phẳng bóng (không trừ Padding) với vị trí camera hiện tại.</summary>
    public bool TryComputeQueueWorldRect(out Rect rect) => TryProjectRect(GamePlayRectQueue, PlaneZ, out rect);

    /// <summary>
    /// Dời camera (giữ FOV) để levelRect nằm lọt trong GamePlayRectQueue: trục nào cần phóng nhiều hơn quyết định tỉ lệ, tâm hai vùng
    /// trùng nhau. Camera nhìn thẳng theo +Z nên vùng chiếu trên mặt phẳng của levelRect tỉ lệ thuận với khoảng cách camera tới mặt
    /// phẳng đó: nhân khoảng cách với tỉ lệ rồi dịch X/Y. Camera orthographic thì nhân orthographicSize thay vì dời Z.
    /// Vị trí được đặt qua CameraManager.SetCameraPosition. Lúc Play gọi Refresh(true) ngay để WorldRect / tường khớp camera mới.
    /// Trả về false (kèm cảnh báo) nếu thiếu camera, rect UI hay levelRect.
    /// </summary>
    public bool FitCameraToQueue(LevelRect levelRect)
    {
        if (levelRect == null || GamePlayRectQueue == null)
        {
            Debug.LogWarning("GamePlayRectManager.FitCameraToQueue: thiếu LevelRect hoặc GamePlayRectQueue, giữ nguyên camera.", this);
            return false;
        }

        Camera cam = ResolveCamera();
        if (cam == null) return false;

        float planeZ = levelRect.transform.position.z;
        Canvas.ForceUpdateCanvases();
        if (!TryProjectRect(GamePlayRectQueue, planeZ, out Rect queue) || queue.width <= 0f || queue.height <= 0f) return false;

        Rect level = levelRect.WorldRect;
        if (level.width <= 0f || level.height <= 0f) return false;

        Transform t = cam.transform;
        if (Vector3.Dot(t.forward, Vector3.forward) < 0.999f)
        {
            Debug.LogWarning("GamePlayRectManager.FitCameraToQueue: camera không nhìn thẳng theo +Z nên kết quả căn chỉ gần đúng.", this);
        }

        // Tỉ lệ phóng vùng nhìn: trục chặt hơn (cần phóng nhiều hơn) làm chuẩn, trục kia còn dư và được căn giữa.
        float scale = Mathf.Max(level.width / queue.width, level.height / queue.height);

        Vector3 pos = t.position;
        // Tâm queue lệch khỏi trục camera bao nhiêu thì sau khi phóng lệch gấp scale lần; dịch camera để tâm queue mới trùng tâm level.
        Vector2 offset = (queue.center - new Vector2(pos.x, pos.y)) * scale;
        Vector2 center = level.center - offset;

        float z = pos.z;
        if (cam.orthographic)
        {
            cam.orthographicSize *= scale;
            if (OverlayCams != null && OverlayCams.Length > 0)
            {
                float size = cam.orthographicSize;
                foreach (var ele in OverlayCams) { 
                    ele.orthographicSize = size;
                }
            }
        }
        else z = planeZ - (planeZ - pos.z) * scale;

        SetCameraPosition(cam, new Vector3(center.x, center.y, z));
        if (Application.isPlaying) Refresh(true);
        return true;
    }

    /// <summary>Nút test: căn camera theo LevelRect đang có trong scene (Level đã kéo vào scene hoặc level đang chơi).</summary>
    [Button("Fit Now")]
    private void FitNow()
    {
        LevelRect target = FindFirstObjectByType<LevelRect>();
        if (target == null)
        {
            Debug.LogWarning("GamePlayRectManager: không tìm thấy LevelRect nào trong scene (kéo Level.prefab vào scene hoặc vào Play).", this);
            return;
        }

#if UNITY_EDITOR
        Camera cam = ResolveCamera();
        if (cam != null)
        {
            UnityEditor.Undo.RecordObject(cam.transform, "Fit Camera To Queue");
            UnityEditor.Undo.RecordObject(cam, "Fit Camera To Queue");
        }
#endif
        if (FitCameraToQueue(target)) Debug.Log($"GamePlayRectManager: đã căn camera theo {target.name} ({target.WorldRect}).", this);
    }

    private Camera ResolveCamera() => WorldCamera != null ? WorldCamera : Camera.main;

    /// <summary>Đặt vị trí camera qua CameraManager; ở edit mode tìm manager trong scene (không dùng Instance để khỏi sinh object), không có thì đặt thẳng.</summary>
    private static void SetCameraPosition(Camera cam, Vector3 position)
    {
        Core.Manager.CameraManager manager = Application.isPlaying ? Core.Manager.CameraManager.Instance : FindFirstObjectByType<Core.Manager.CameraManager>();
        if (manager != null) manager.SetCameraPosition(position);
        else cam.transform.position = position;
    }

    /// <summary>Chiếu 4 góc của một rect UI qua camera xuống mặt phẳng z = planeZ, lấy bao chữ nhật world (chưa trừ Padding).</summary>
    private bool TryProjectRect(RectTransform rectTransform, float planeZ, out Rect rect)
    {
        rect = default;
        if (rectTransform == null) return false;

        Camera cam = ResolveCamera();
        if (cam == null) return false;

        Camera uiCamera = GamePlayCanvas != null && GamePlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? GamePlayCanvas.worldCamera : null;

        Plane plane = new(Vector3.forward, new Vector3(0f, 0f, planeZ));
        rectTransform.GetWorldCorners(corners);

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            Ray ray = cam.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 world = ray.GetPoint(enter);
            minX = Mathf.Min(minX, world.x);
            maxX = Mathf.Max(maxX, world.x);
            minY = Mathf.Min(minY, world.y);
            maxY = Mathf.Max(maxY, world.y);
        }

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    /// <summary>Điểm màn hình (pixel) chiếu xuống mặt phẳng bóng.</summary>
    public Vector3 ScreenToPlane(Vector2 screen)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return new Vector3(0f, 0f, PlaneZ);

        Ray ray = cam.ScreenPointToRay(screen);
        Plane plane = new(Vector3.forward, new Vector3(0f, 0f, PlaneZ));
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : new Vector3(0f, 0f, PlaneZ);
    }

    /// <summary>Tọa độ chuẩn hóa trong rect (0,0 = góc trái dưới, 1,1 = góc phải trên) sang world.</summary>
    public Vector3 NormalizedToWorld(Vector2 normalized)
    {
        Rect r = WorldRect;
        return new Vector3(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y, PlaneZ);
    }

    /// <summary>Kẹp tâm một vật bán kính radius vào trong rect, z về PlaneZ.</summary>
    public Vector3 ClampToRect(Vector3 world, float radius = 0f)
    {
        if (!HasWorldRect) return world;

        Rect r = WorldRect;
        float x = ClampInside(world.x, r.xMin, r.xMax, radius);
        float y = ClampInside(world.y, r.yMin, r.yMax, radius);
        return new Vector3(x, y, PlaneZ);
    }

    /// <summary>Vật bán kính radius có nằm trọn trong rect không.</summary>
    public bool Contains(Vector3 world, float radius = 0f)
    {
        if (!HasWorldRect) return false;

        Rect r = WorldRect;
        return world.x >= r.xMin + radius && world.x <= r.xMax - radius
               && world.y >= r.yMin + radius && world.y <= r.yMax - radius;
    }

    /// <summary>Điểm ngẫu nhiên trong rect sao cho vật bán kính radius nằm trọn bên trong.</summary>
    public Vector3 RandomPointInRect(float radius = 0f)
    {
        Rect r = WorldRect;
        float x = RandomInside(r.xMin, r.xMax, radius);
        float y = RandomInside(r.yMin, r.yMax, radius);
        return new Vector3(x, y, PlaneZ);
    }

    private static float ClampInside(float v, float min, float max, float radius)
    {
        float lo = min + radius;
        float hi = max - radius;
        return lo > hi ? (min + max) * 0.5f : Mathf.Clamp(v, lo, hi);
    }

    private static float RandomInside(float min, float max, float radius)
    {
        float lo = min + radius;
        float hi = max - radius;
        return lo > hi ? (min + max) * 0.5f : UnityEngine.Random.Range(lo, hi);
    }

    private static bool Approximately(Rect a, Rect b)
    {
        const float eps = 1e-4f;
        return Mathf.Abs(a.xMin - b.xMin) < eps && Mathf.Abs(a.xMax - b.xMax) < eps
               && Mathf.Abs(a.yMin - b.yMin) < eps && Mathf.Abs(a.yMax - b.yMax) < eps;
    }

    private void UpdateWalls()
    {
        if (!BuildWalls)
        {
            if (WallShape != null) WallShape.gameObject.SetActive(false);
            return;
        }

        if (WallShape == null)
        {
            if (!wallWarned) Debug.LogError("GamePlayRectManager: chưa gán WallShape (SoftBodyShape con GamePlayRectWalls) nên không có tường bao vùng chơi.", this);
            wallWarned = true;
            return;
        }

        Rect r = WorldRect;
        Transform t = WallShape.transform;

        // Cạnh cứng: mặt tường box ở cạnh đó bị đẩy ra ngoài RigidWallExtension để particle không chạm; giới hạn thật do
        // SoftBubbleWorld.SetRigidBounds lo (dịch nguyên khối, giữ hình cầu).
        float ext = Mathf.Max(1f, RigidWallExtension);
        float left = r.xMin - (RigidLeft ? ext : 0f);
        float right = r.xMax + (RigidRight ? ext : 0f);
        float bottom = r.yMin - (RigidBottom ? ext : 0f);
        float top = r.yMax + (RigidTop ? ext : 0f);

        // SoftBodyShape nhân Size với lossyScale nên giữ lossyScale = 1 dù manager có bị scale.
        Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(SafeInverse(parentScale.x), SafeInverse(parentScale.y), SafeInverse(parentScale.z));
        t.SetPositionAndRotation(new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, PlaneZ), Quaternion.identity);

        WallShape.Configure(SoftShapeType.Box, new Vector3(right - left, top - bottom, WallDepth), true, WallFriction);
        WallShape.gameObject.SetActive(true);
        ApplyRigidBounds();
    }

    /// <summary>Đặt (hoặc bỏ) giới hạn cứng theo cạnh lên SoftBubbleWorld đang có.</summary>
    private void ApplyRigidBounds()
    {
        SoftBubbleWorld world = SoftBubbleWorld.Instance;
        if (world == null) world = SoftBubbleWorld.Get();
        if (world == null) return;
        if (boundWorld != null && boundWorld != world) boundWorld.ClearRigidBounds();
        boundWorld = world;
        if (RigidMaskWanted == 0) world.ClearRigidBounds();
        else world.SetRigidBounds(WorldRect, RigidLeft, RigidRight, RigidBottom, RigidTop);
    }

    private static float SafeInverse(float v) => Mathf.Abs(v) > 1e-6f ? 1f / v : 1f;

    private void OnDrawGizmos()
    {
        Rect r;
        if (Application.isPlaying && HasWorldRect) r = WorldRect;
        else if (!TryComputeWorldRect(out r)) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y, PlaneZ), new Vector3(r.width, r.height, 0f));

        // Vùng queue vẽ xanh lơ để so với LevelRectQueue (xanh lá) sau khi Fit.
        if (TryComputeQueueWorldRect(out Rect q))
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(q.center.x, q.center.y, PlaneZ), new Vector3(q.width, q.height, 0f));
        }

        // Cạnh cứng vẽ đỏ đậm.
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 1f);
        if (RigidTop) Gizmos.DrawLine(new Vector3(r.xMin, r.yMax, PlaneZ), new Vector3(r.xMax, r.yMax, PlaneZ));
        if (RigidBottom) Gizmos.DrawLine(new Vector3(r.xMin, r.yMin, PlaneZ), new Vector3(r.xMax, r.yMin, PlaneZ));
        if (RigidLeft) Gizmos.DrawLine(new Vector3(r.xMin, r.yMin, PlaneZ), new Vector3(r.xMin, r.yMax, PlaneZ));
        if (RigidRight) Gizmos.DrawLine(new Vector3(r.xMax, r.yMin, PlaneZ), new Vector3(r.xMax, r.yMax, PlaneZ));
    }
}
