using UnityEngine;
using UnityEngine.UI;

/// Đàn cá xa bơi ngang qua BG gameplay (lớp FarFish, nằm sau lớp đá, trong Canvas con riêng).
/// Pool Image tạo sẵn một lần; mỗi frame chỉ đặt anchoredPosition / localScale của con đang bơi, không cấp phát.
/// Hệ tọa độ: lớp này phủ kín BgLayers (Envelope 2:3, pivot giữa = tâm màn hình); phần nhìn thấy lấy theo root canvas.
/// Thời gian theo Time.deltaTime nên dừng cùng game khi timeScale = 0.
/// Với FishMaterial (shader FishSort/UI/Swim Silhouette): kênh R của màu Image là pha quẫy đuôi của từng con, alpha là độ mờ;
/// shader chỉ vẽ mỗi điểm ảnh một lần nên con vẽ trước nằm trên: cá gần được xếp lên trước trong hierarchy.
public class BgFishSchool : MonoBehaviour
{
    [field: SerializeField, Tooltip("Các dáng cá silhouette (đầu quay sang trái); mỗi con lấy ngẫu nhiên một dáng, phần tử trống bị bỏ qua")] private Sprite[] FishSprites { get; set; }
    [field: SerializeField, Tooltip("Material bơi (FishSort/UI/Swim Silhouette): uốn thân, quẫy đuôi, không chồng lớp. Trống = UI mặc định, dùng màu Tint")] private Material FishMaterial { get; set; }
    [field: SerializeField, Tooltip("Bề ngang một con ở scale 1 (đơn vị canvas); chiều cao theo tỉ lệ sprite")] private float FishWidth { get; set; } = 100f;
    [field: SerializeField, Tooltip("Số Image tạo sẵn; đàn mới chỉ ra khi còn đủ chỗ cho số con tối thiểu")] private int PoolSize { get; set; } = 10;
    [field: SerializeField, Tooltip("Chờ trước đàn đầu tiên (giây, min..max)")] private Vector2 FirstDelay { get; set; } = new Vector2(2f, 5f);
    [field: SerializeField, Tooltip("Khoảng giữa hai đàn (giây, min..max), tính từ lúc đàn trước xuất phát")] private Vector2 Interval { get; set; } = new Vector2(8f, 15f);
    [field: SerializeField, Tooltip("Số con mỗi đàn (min, max)")] private Vector2Int SchoolSize { get; set; } = new Vector2Int(3, 5);
    [field: SerializeField, Tooltip("Tốc độ bơi (đơn vị canvas / giây, min..max), trước khi nhân hệ số độ xa")] private Vector2 Speed { get; set; } = new Vector2(70f, 120f);
    [field: SerializeField, Tooltip("Hệ số tốc độ theo độ xa: x = gần, y = xa")] private Vector2 SpeedByDepth { get; set; } = new Vector2(1f, 0.7f);
    [field: SerializeField, Tooltip("Dải độ cao của đàn theo chiều cao lớp: 0 = đáy, 1 = đỉnh")] private Vector2 HeightBand { get; set; } = new Vector2(0.45f, 0.85f);
    [field: SerializeField, Tooltip("Khoảng cách ngang giữa hai con liền nhau trong đàn (min..max, nhân scale)")] private Vector2 Spacing { get; set; } = new Vector2(45f, 80f);
    [field: SerializeField, Tooltip("Lệch dọc tối đa của mỗi con so với đàn")] private float SpreadY { get; set; } = 30f;
    [field: SerializeField, Tooltip("Scale theo độ xa: x = gần, y = xa")] private Vector2 ScaleByDepth { get; set; } = new Vector2(1.1f, 0.6f);
    [field: SerializeField, Tooltip("Alpha theo độ xa: x = gần, y = xa")] private Vector2 AlphaByDepth { get; set; } = new Vector2(0.6f, 0.3f);
    [field: SerializeField, Tooltip("Màu nhân lên silhouette; alpha nhân thêm với alpha theo độ xa")] private Color Tint { get; set; } = Color.white;
    [field: SerializeField, Tooltip("Biên độ nhấp nhô dọc (đơn vị canvas)")] private float BobAmplitude { get; set; } = 6f;
    [field: SerializeField, Tooltip("Tần số nhấp nhô (Hz)")] private float BobFrequency { get; set; } = 0.6f;
    [field: SerializeField, Tooltip("Co giãn scaleY khi bơi (0.04 = ±4%)")] private float SwimSquash { get; set; } = 0.04f;
    [field: SerializeField, Tooltip("Tần số co giãn khi bơi (Hz)")] private float SwimFrequency { get; set; } = 2.2f;
    [field: SerializeField, Tooltip("Lề ngoài mép màn hình lúc xuất phát / biến mất; tự nâng lên ít nhất nửa thân con lớn nhất")] private float EdgeMargin { get; set; } = 80f;

    private struct Swimmer
    {
        public Image Image;
        public RectTransform Rect;
        public bool Active;
        public int Direction;
        public float X;
        public float BaseY;
        public float Speed;
        public float Scale;
        public float Phase;
    }

    private Swimmer[] swimmers;
    private Sprite[] sprites;
    private int[] drawOrder;
    private RectTransform layerRect;
    private RectTransform rootRect;
    private float clock;
    private float nextSchoolTimer;

    public int ActiveCount { get; private set; }

    private void Awake() => Initialize();

    private void Update() => Simulate(Time.deltaTime);

    /// <summary>Tạo pool và hẹn giờ đàn đầu. Gọi nhiều lần không sao; test EditMode gọi thẳng vì Awake không chạy trong edit mode.</summary>
    public void Initialize()
    {
        if (swimmers != null) return;
        layerRect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : layerRect;

        int valid = 0;
        if (FishSprites != null) foreach (Sprite sprite in FishSprites) if (sprite != null) valid++;
        if (valid == 0)
        {
            // Không có sprite thì Image vẽ ô trắng: tắt hẳn thay vì vẽ sai.
            Debug.LogWarning($"[BgFishSchool] {name}: chưa gán FishSprites, tắt hiệu ứng cá");
            swimmers = new Swimmer[0];
            enabled = false;
            return;
        }

        sprites = new Sprite[valid];
        valid = 0;
        foreach (Sprite sprite in FishSprites) if (sprite != null) sprites[valid++] = sprite;

        swimmers = new Swimmer[Mathf.Max(0, PoolSize)];
        drawOrder = new int[swimmers.Length];
        for (int i = 0; i < swimmers.Length; i++)
        {
            var go = new GameObject("BgFish", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(layerRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = sprites[0];
            image.material = FishMaterial;
            image.raycastTarget = false;
            image.enabled = false;
            swimmers[i] = new Swimmer { Image = image, Rect = rect };
        }

        nextSchoolTimer = BgLayerMath.InRange(FirstDelay, Random.value);
    }

    /// <summary>Chạy một bước dt giây: cá đang bơi tiến lên, con đã qua hẳn mép trước trả về pool, tới giờ thì ra đàn mới.</summary>
    public void Simulate(float dt)
    {
        if (swimmers == null || swimmers.Length == 0) return;
        clock += dt;
        float halfWidth = VisibleHalfWidth();
        float margin = Margin();

        for (int i = 0; i < swimmers.Length; i++)
        {
            ref Swimmer s = ref swimmers[i];
            if (!s.Active) continue;

            s.X += s.Direction * s.Speed * dt;
            if (BgFishSchoolMath.IsOutside(s.X, s.Direction, halfWidth, margin))
            {
                s.Active = false;
                s.Image.enabled = false;
                ActiveCount--;
                continue;
            }

            Place(ref s);
        }

        nextSchoolTimer -= dt;
        if (nextSchoolTimer <= 0f && TrySpawnSchool()) nextSchoolTimer = BgLayerMath.InRange(Interval, Random.value);
    }

    /// <summary>Ra ngay một đàn nếu pool còn chỗ cho ít nhất SchoolSize.x con (đàn bị cắt bớt nếu không đủ chỗ cho cả đàn).</summary>
    public bool TrySpawnSchool()
    {
        if (swimmers == null || swimmers.Length == 0) return false;
        // Lớp chưa được layout (kích thước 0 ở frame đầu): ra đàn lúc này thì cá xuất phát giữa màn hình.
        if (VisibleHalfWidth() <= 0f || layerRect.rect.height <= 0f) return false;
        int min = Mathf.Max(1, SchoolSize.x);
        int free = swimmers.Length - ActiveCount;
        if (free < min) return false;
        int count = Mathf.Min(free, Random.Range(min, Mathf.Max(min, SchoolSize.y) + 1));

        int direction = BgFishSchoolMath.PickDirection(Random.value);
        float depth = Random.value;
        float scale = BgLayerMath.InRange(ScaleByDepth, depth);
        float speed = BgLayerMath.InRange(Speed, Random.value) * BgLayerMath.InRange(SpeedByDepth, depth);
        float alpha = Tint.a * BgLayerMath.InRange(AlphaByDepth, depth);
        float halfWidth = VisibleHalfWidth();
        float margin = Margin();
        float baseY = BgFishSchoolMath.SchoolBaseY(HeightBand, Random.value, layerRect.rect.height);

        float behind = 0f;
        int placed = 0;
        for (int i = 0; i < swimmers.Length && placed < count; i++)
        {
            ref Swimmer s = ref swimmers[i];
            if (s.Active) continue;

            s.Active = true;
            s.Direction = direction;
            s.Speed = speed;
            s.Scale = scale;
            s.Phase = Random.value;
            s.X = BgFishSchoolMath.StartX(direction, halfWidth, margin, behind);
            s.BaseY = baseY + Random.Range(-SpreadY, SpreadY);
            Sprite sprite = sprites[Random.Range(0, sprites.Length)];
            s.Image.sprite = sprite;
            s.Rect.sizeDelta = new Vector2(FishWidth, FishWidth * sprite.rect.height / sprite.rect.width);
            // Shader bơi đọc pha quẫy đuôi ở kênh R (màu cá lấy từ material); không có material thì R là màu Tint.
            s.Image.color = FishMaterial != null ? new Color(s.Phase, 1f, 1f, alpha) : new Color(Tint.r, Tint.g, Tint.b, alpha);
            s.Image.enabled = true;
            Place(ref s);

            behind += BgLayerMath.InRange(Spacing, Random.value) * scale;
            placed++;
            ActiveCount++;
        }

        SortNearestFirst();
        return true;
    }

    /// <summary>
    /// Xếp cá đang bơi lên đầu hierarchy, con gần (scale lớn) trước: shader chỉ vẽ mỗi điểm ảnh một lần nên con vẽ trước che con sau.
    /// Chỉ chạy lúc ra đàn mới; không cấp phát (mảng thứ tự tạo sẵn).
    /// </summary>
    private void SortNearestFirst()
    {
        int n = 0;
        for (int i = 0; i < swimmers.Length; i++) if (swimmers[i].Active) drawOrder[n++] = i;
        for (int i = 1; i < n; i++)
        {
            int key = drawOrder[i];
            int j = i - 1;
            while (j >= 0 && swimmers[drawOrder[j]].Scale < swimmers[key].Scale)
            {
                drawOrder[j + 1] = drawOrder[j];
                j--;
            }
            drawOrder[j + 1] = key;
        }
        for (int k = 0; k < n; k++) swimmers[drawOrder[k]].Rect.SetSiblingIndex(k);
    }

    private void Place(ref Swimmer s)
    {
        float y = s.BaseY + BgLayerMath.Wave(clock, BobAmplitude, BobFrequency, s.Phase);
        float squash = 1f + BgLayerMath.Wave(clock, SwimSquash, SwimFrequency, s.Phase);
        s.Rect.anchoredPosition = new Vector2(s.X, y);
        s.Rect.localScale = new Vector3(BgFishSchoolMath.FacingSign(s.Direction) * s.Scale, s.Scale * squash, 1f);
    }

    private float VisibleHalfWidth() => BgLayerMath.VisibleHalf(rootRect.rect.width, layerRect.rect.width);

    private float Margin() => BgFishSchoolMath.EffectiveMargin(EdgeMargin, FishWidth, Mathf.Max(ScaleByDepth.x, ScaleByDepth.y));
}
