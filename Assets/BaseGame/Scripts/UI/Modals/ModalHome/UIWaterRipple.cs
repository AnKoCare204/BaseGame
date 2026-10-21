using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// Gợn sóng nước khi chạm vào nền Home (kiểu FishDom): nhấn xuống sinh một gợn to (2 đỉnh sóng đồng tâm nở ra, ảnh bên dưới
/// bị khúc xạ và sáng / tối theo độ dốc sóng, vòng "trong suốt" không vẽ màu riêng), rê tay thì cứ mỗi TrailSpacing đơn vị sinh
/// một gợn nhỏ, thành vệt gợn dọc đường kéo. Gắn lên Image nền (raycastTarget bật), nên bấm vào nút phía trên không sinh gợn.
/// Cùng một bộ gợn được đẩy lên nhiều lớp UI (Targets: nền + RawImage cá, mỗi lớp một bản sao material shader
/// "FishSort/UI/Water Ripple") nên gợn hiện trên cả cá lẫn nền. Tọa độ gợn tính bằng pixel màn hình, bán kính / độ dày nhân với
/// scaleFactor của canvas. Chạy theo unscaled time, không có âm thanh. Mỗi gợn sinh ra bắn event RippleSpawned để cá trong render
/// texture phản ứng (HomeRenderTexture nghe).
/// </summary>
[RequireComponent(typeof(Image))]
public class UIWaterRipple : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    private const int MaxRipples = 48; // khớp MAX_RIPPLES trong UIWaterRipple.shader

    [System.Serializable]
    private struct RippleStyle
    {
        [Tooltip("Bán kính lúc vừa sinh (đơn vị canvas)")] public float StartRadius;
        [Tooltip("Bán kính lúc tắt hẳn")] public float EndRadius;
        [Tooltip("Thời gian sống (giây)")] public float Life;
        [Tooltip("Nửa độ rộng của đỉnh sóng (đơn vị canvas)")] public float Width;
        [Tooltip("Biên độ khúc xạ ảnh (pixel canvas) ở sườn sóng")] public float Distortion;
        [Tooltip("Độ đậm tối đa 0..1")] public float Strength;
    }

    [System.Serializable]
    private class Target
    {
        [Tooltip("Lớp UI nhận gợn (Image hoặc RawImage), material phải dùng shader FishSort/UI/Water Ripple")]
        public Graphic Graphic;
        [System.NonSerialized] public Material Material;
    }

    private struct Ripple
    {
        public Vector2 Center; // pixel màn hình
        public float Age;
        public RippleStyle Style;
    }

    [field: SerializeField] private Image Image { get; set; }

    /// Bắn mỗi khi sinh một gợn: (vị trí pixel màn hình, có phải gợn của vệt rê tay không). HomeRenderTexture nghe để cá phản ứng.
    public event System.Action<Vector2, bool> RippleSpawned;

    [field: SerializeField, Tooltip("Các lớp UI nhận gợn (nền, lớp cá...)")]
    private Target[] Targets { get; set; }

    [field: SerializeField, Tooltip("Gợn lúc nhấn xuống")]
    private RippleStyle TapStyle { get; set; } = new()
    {
        StartRadius = 40f, EndRadius = 150f, Life = 0.7f, Width = 14f, Distortion = 12f, Strength = 1f,
    };

    [field: SerializeField, Tooltip("Gợn nhỏ sinh dọc đường rê tay")]
    private RippleStyle TrailStyle { get; set; } = new()
    {
        StartRadius = 25f, EndRadius = 75f, Life = 0.9f, Width = 9f, Distortion = 7f, Strength = 0.8f,
    };

    [field: SerializeField, Tooltip("Khoảng cách rê tay giữa 2 gợn nhỏ liên tiếp (đơn vị canvas)")]
    private float TrailSpacing { get; set; } = 40f;

    private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");
    private static readonly int RippleAId = Shader.PropertyToID("_RippleA");
    private static readonly int RippleBId = Shader.PropertyToID("_RippleB");
    private static readonly int RippleUVRectId = Shader.PropertyToID("_RippleUVRect");
    private static readonly int RippleScreenRectId = Shader.PropertyToID("_RippleScreenRect");

    private readonly List<Ripple> ripples = new(MaxRipples);
    private readonly Vector4[] dataA = new Vector4[MaxRipples];
    private readonly Vector4[] dataB = new Vector4[MaxRipples];
    /// <summary>Điểm sinh gợn gần nhất của từng ngón đang rê (pixel màn hình).</summary>
    private readonly Dictionary<int, Vector2> lastTrailPoints = new();

    private void Reset()
    {
        Image = GetComponent<Image>();
    }

    private void Awake()
    {
        if (Image == null) Image = GetComponent<Image>();
        if (Targets == null) return;
        foreach (Target target in Targets)
        {
            if (target.Graphic == null || target.Graphic.material == null) continue;
            target.Material = new Material(target.Graphic.material) { name = target.Graphic.material.name + " (Ripple)" };
            target.Graphic.material = target.Material;
            target.Material.SetVectorArray(RippleAId, dataA);
            target.Material.SetVectorArray(RippleBId, dataB);
            target.Material.SetInt(RippleCountId, 0);
        }
    }

    private void OnDisable()
    {
        ripples.Clear();
        lastTrailPoints.Clear();
        SetCount(0);
    }

    private void OnDestroy()
    {
        if (Targets == null) return;
        foreach (Target target in Targets)
        {
            if (target.Material != null) Destroy(target.Material);
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false; // vệt gợn bám tay ngay từ pixel đầu tiên
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Spawn(eventData.position, TapStyle, false);
        lastTrailPoints[eventData.pointerId] = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 point = eventData.position;
        if (UIMainMenuTab.IsActiveSwiping?.Invoke() ?? false) return;
        if (!lastTrailPoints.TryGetValue(eventData.pointerId, out Vector2 last))
        {
            lastTrailPoints[eventData.pointerId] = point;
            return;
        }
        
        float spacing = TrailSpacing * CanvasScale;
        Vector2 delta = point - last;
        float distance = delta.magnitude;
        if (distance < spacing) return;
        Vector2 step = delta / distance * spacing;
        while (distance >= spacing)
        {
            last += step;
            distance -= spacing;
            Spawn(last, TrailStyle, true);
        }

        lastTrailPoints[eventData.pointerId] = last;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        lastTrailPoints.Remove(eventData.pointerId);
    }

    private float CanvasScale => Image.canvas != null ? Image.canvas.scaleFactor : 1f;

    private void Spawn(Vector2 screenCenter, RippleStyle style, bool isTrail)
    {
        if (ripples.Count >= MaxRipples) ripples.RemoveAt(0); // hết chỗ thì bỏ gợn cũ nhất
        ripples.Add(new Ripple { Center = screenCenter, Age = 0f, Style = style });
        RippleSpawned?.Invoke(screenCenter, isTrail);
    }

    private void LateUpdate()
    {
        if (Targets == null || ripples.Count == 0) return;

        float dt = Time.unscaledDeltaTime;
        float scale = CanvasScale;
        int count = 0;
        for (int i = 0; i < ripples.Count; i++)
        {
            Ripple ripple = ripples[i];
            ripple.Age += dt;
            float t = ripple.Age / Mathf.Max(ripple.Style.Life, 0.01f);
            if (t >= 1f) continue;

            ripples[count] = ripple;
            float grow = 1f - (1f - t) * (1f - t) * (1f - t); // nở nhanh rồi chậm dần
            float fadeIn = Mathf.Clamp01(ripple.Age / 0.06f);
            float fadeOut = (1f - t) * (1f - t);
            float radius = Mathf.Lerp(ripple.Style.StartRadius, ripple.Style.EndRadius, grow) * scale;
            dataA[count] = new Vector4(ripple.Center.x, ripple.Center.y, radius, ripple.Style.Strength * fadeIn * fadeOut);
            dataB[count] = new Vector4(ripple.Style.Width * scale, ripple.Style.Distortion * scale, 0f, 0f);
            count++;
        }

        ripples.RemoveRange(count, ripples.Count - count);
        foreach (Target target in Targets)
        {
            if (target.Material == null) continue;
            UploadRect(target);
            target.Material.SetVectorArray(RippleAId, dataA);
            target.Material.SetVectorArray(RippleBId, dataB);
            target.Material.SetInt(RippleCountId, count);
        }
    }

    private void SetCount(int count)
    {
        if (Targets == null) return;
        foreach (Target target in Targets)
        {
            if (target.Material != null) target.Material.SetInt(RippleCountId, count);
        }
    }

    /// <summary>Vùng ảnh thực sự được vẽ của lớp (tính cả preserveAspect giống Image) theo pixel màn hình và dải UV tương ứng.</summary>
    private void UploadRect(Target target)
    {
        Graphic graphic = target.Graphic;
        RectTransform rt = graphic.rectTransform;
        Rect rect = graphic.GetPixelAdjustedRect();
        Vector4 uv = new(0f, 0f, 1f, 1f);

        if (graphic is Image image)
        {
            Sprite sprite = image.overrideSprite;
            if (sprite != null)
            {
                uv = DataUtility.GetOuterUV(sprite);
                if (image.preserveAspect && image.type == Image.Type.Simple && rect.height > 0f)
                {
                    float spriteRatio = sprite.rect.width / sprite.rect.height;
                    Vector2 pivot = rt.pivot;
                    if (spriteRatio > rect.width / rect.height)
                    {
                        float oldHeight = rect.height;
                        rect.height = rect.width / spriteRatio;
                        rect.y += (oldHeight - rect.height) * pivot.y;
                    }
                    else
                    {
                        float oldWidth = rect.width;
                        rect.width = rect.height * spriteRatio;
                        rect.x += (oldWidth - rect.width) * pivot.x;
                    }
                }
            }
        }
        else if (graphic is RawImage rawImage)
        {
            Rect uvRect = rawImage.uvRect;
            uv = new Vector4(uvRect.xMin, uvRect.yMin, uvRect.xMax, uvRect.yMax);
        }

        Camera cam = graphic.canvas != null && graphic.canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? graphic.canvas.worldCamera
            : null;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rect.min));
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rect.max));

        target.Material.SetVector(RippleUVRectId, new Vector4(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
        target.Material.SetVector(RippleScreenRectId, new Vector4(min.x, min.y, max.x - min.x, max.y - min.y));
    }
}
