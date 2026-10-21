using UnityEngine;
using UnityEngine.UI;

/// Bóng nước nền bay từ dưới lên trên BG gameplay (lớp Bubbles, trên lớp đá, dưới decor trái/phải, trong Canvas con riêng).
/// Nhỏ, mờ, không nhận chạm, để không nhầm với bubble gameplay. Pool Image tạo sẵn; mỗi frame chỉ đặt vị trí / scale / màu.
/// Bóng có sẵn lúc đầu (prewarm) sinh ở lần Simulate đầu tiên vì lúc Awake canvas chưa layout xong kích thước.
public class BgBubbleEmitter : MonoBehaviour
{
    [field: SerializeField, Tooltip("Ảnh bóng nước (VFX/textures/water_bubble.png)")] private Sprite BubbleSprite { get; set; }
    [field: SerializeField, Tooltip("Số bóng tối đa cùng lúc, cũng là số Image tạo sẵn")] private int MaxAlive { get; set; } = 10;
    [field: SerializeField, Tooltip("Số bóng có sẵn ở độ cao ngẫu nhiên lúc bắt đầu, để màn hình không trống")] private int PrewarmCount { get; set; } = 6;
    [field: SerializeField, Tooltip("Dải độ cao chuẩn hóa (0 = mép dưới, 1 = mép trên) của bóng có sẵn")] private Vector2 PrewarmBand { get; set; } = new Vector2(0.05f, 0.8f);
    [field: SerializeField, Tooltip("Khoảng giữa hai lần sinh (giây, min..max)")] private Vector2 SpawnInterval { get; set; } = new Vector2(0.5f, 1.2f);
    [field: SerializeField, Tooltip("Đường kính bóng (đơn vị canvas, min..max)")] private Vector2 Size { get; set; } = new Vector2(14f, 36f);
    [field: SerializeField, Tooltip("Tốc độ lên (đơn vị canvas / giây): x = bóng nhỏ nhất, y = bóng to nhất")] private Vector2 RiseSpeed { get; set; } = new Vector2(70f, 140f);
    [field: SerializeField, Tooltip("Biên độ lắc ngang (min..max)")] private Vector2 WobbleAmplitude { get; set; } = new Vector2(4f, 12f);
    [field: SerializeField, Tooltip("Tần số lắc ngang (Hz, min..max)")] private Vector2 WobbleFrequency { get; set; } = new Vector2(0.6f, 1.4f);
    [field: SerializeField, Tooltip("Màu nhân lên ảnh bóng; alpha = độ đậm tối đa")] private Color Tint { get; set; } = new Color(1f, 1f, 1f, 0.4f);
    [field: SerializeField, Tooltip("Thời gian hiện dần (giây)")] private float FadeIn { get; set; } = 0.4f;
    [field: SerializeField, Tooltip("Phần trên cùng của màn hình nhìn thấy mà bóng mờ dần (0.25 = 25%)")] private float FadeOutBand { get; set; } = 0.25f;
    [field: SerializeField, Tooltip("Phồng thêm khi lên tới mép trên (0.15 = +15%)")] private float Growth { get; set; } = 0.15f;
    [field: SerializeField, Tooltip("Xác suất sinh kèm 1-2 bóng nhỏ ngay dưới thành chuỗi")] private float TrailChance { get; set; } = 0.25f;
    [field: SerializeField, Tooltip("Lề hai bên khi chọn x sinh bóng")] private float EdgeMargin { get; set; } = 40f;

    private struct Bubble
    {
        public Image Image;
        public RectTransform Rect;
        public bool Active;
        public float X;
        public float Y;
        public float Size;
        public float Speed;
        public float Age;
        public float WobbleAmp;
        public float WobbleFreq;
        public float Phase;
    }

    private Bubble[] bubbles;
    private RectTransform layerRect;
    private RectTransform rootRect;
    private float spawnTimer;
    private bool prewarmed;

    public int ActiveCount { get; private set; }

    private void Awake() => Initialize();

    private void Update() => Simulate(Time.deltaTime);

    /// <summary>Tạo pool và hẹn giờ sinh. Gọi nhiều lần không sao; test EditMode gọi thẳng vì Awake không chạy trong edit mode.</summary>
    public void Initialize()
    {
        if (bubbles != null) return;
        layerRect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : layerRect;

        if (BubbleSprite == null)
        {
            // Không có sprite thì Image vẽ ô trắng: tắt hẳn thay vì vẽ sai.
            Debug.LogWarning($"[BgBubbleEmitter] {name}: chưa gán BubbleSprite, tắt hiệu ứng bóng");
            bubbles = new Bubble[0];
            enabled = false;
            return;
        }

        bubbles = new Bubble[Mathf.Max(0, MaxAlive)];
        for (int i = 0; i < bubbles.Length; i++)
        {
            var go = new GameObject("BgBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(layerRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = BubbleSprite;
            image.raycastTarget = false;
            // Màu đặt một lần; độ mờ từng frame đi qua CanvasRenderer.SetAlpha để không dựng lại mesh (đổi Image.color là dựng lại).
            image.color = new Color(Tint.r, Tint.g, Tint.b, 1f);
            image.enabled = false;
            bubbles[i] = new Bubble { Image = image, Rect = rect };
        }

        spawnTimer = BgLayerMath.InRange(SpawnInterval, Random.value);
    }

    /// <summary>Chạy một bước dt giây: bóng lên, bóng qua mép trên trả về pool, tới giờ thì sinh bóng mới (kèm chuỗi bóng nhỏ).</summary>
    public void Simulate(float dt)
    {
        if (bubbles == null || bubbles.Length == 0) return;
        float halfWidth = BgLayerMath.VisibleHalf(rootRect.rect.width, layerRect.rect.width);
        float halfHeight = BgLayerMath.VisibleHalf(rootRect.rect.height, layerRect.rect.height);
        // Lớp chưa được layout (kích thước 0 ở frame đầu): chưa biết vùng nhìn thấy, chờ frame sau.
        if (halfWidth <= 0f || halfHeight <= 0f) return;

        if (!prewarmed)
        {
            prewarmed = true;
            int count = Mathf.Min(PrewarmCount, bubbles.Length);
            for (int i = 0; i < count; i++)
            {
                float y = BgLayerMath.InRange(PrewarmBand, Random.value) * 2f * halfHeight - halfHeight;
                Spawn(BgBubbleMath.SpawnX(Random.value, halfWidth, EdgeMargin), y, RandomSize(), FadeIn, halfHeight);
            }
        }

        for (int i = 0; i < bubbles.Length; i++)
        {
            ref Bubble b = ref bubbles[i];
            if (!b.Active) continue;

            b.Age += dt;
            b.Y += b.Speed * dt;
            if (BgBubbleMath.IsAboveTop(b.Y, halfHeight, b.Size))
            {
                b.Active = false;
                b.Image.enabled = false;
                ActiveCount--;
                continue;
            }

            Place(ref b, halfHeight);
        }

        spawnTimer -= dt;
        if (spawnTimer > 0f || ActiveCount >= bubbles.Length) return;
        spawnTimer = BgLayerMath.InRange(SpawnInterval, Random.value);

        float x = BgBubbleMath.SpawnX(Random.value, halfWidth, EdgeMargin);
        float size = RandomSize();
        float baseY = BgBubbleMath.SpawnY(halfHeight, size);
        Spawn(x, baseY, size, 0f, halfHeight);
        if (Random.value >= TrailChance) return;

        int trail = Random.Range(1, 3);
        for (int i = 0; i < trail; i++)
        {
            float small = Mathf.Max(Size.x, size * Random.Range(0.45f, 0.7f));
            baseY -= small * 1.6f;
            Spawn(x, baseY, small, 0f, halfHeight);
        }
    }

    private float RandomSize() => BgLayerMath.InRange(Size, Random.value);

    private void Spawn(float x, float y, float size, float age, float halfHeight)
    {
        for (int i = 0; i < bubbles.Length; i++)
        {
            ref Bubble b = ref bubbles[i];
            if (b.Active) continue;

            b.Active = true;
            b.X = x;
            b.Y = y;
            b.Size = size;
            b.Age = age;
            b.Speed = BgBubbleMath.RiseSpeedForSize(size, Size, RiseSpeed);
            b.WobbleAmp = BgLayerMath.InRange(WobbleAmplitude, Random.value);
            b.WobbleFreq = BgLayerMath.InRange(WobbleFrequency, Random.value);
            b.Phase = Random.value;
            b.Rect.sizeDelta = new Vector2(size, size);
            b.Image.enabled = true;
            ActiveCount++;
            Place(ref b, halfHeight);
            return;
        }
    }

    private void Place(ref Bubble b, float halfHeight)
    {
        float ny = BgBubbleMath.NormalizedY(b.Y, halfHeight);
        float scale = BgBubbleMath.ScaleAt(ny, Growth);
        b.Rect.anchoredPosition = new Vector2(b.X + BgLayerMath.Wave(b.Age, b.WobbleAmp, b.WobbleFreq, b.Phase), b.Y);
        b.Rect.localScale = new Vector3(scale, scale, 1f);
        b.Image.canvasRenderer.SetAlpha(BgBubbleMath.AlphaAt(b.Age, ny, Tint.a, FadeIn, FadeOutBand));
    }
}
