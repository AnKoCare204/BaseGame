using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;

/// <summary>
/// Root gameplay của prefab bóng: nối SoftBubble + BubbleFishContainer (cùng GameObject, gán sẵn trên prefab) với BubbleEntry
/// của BoardModel. Level sinh prefab theo kiểu BubbleView nên không cần GetComponent lúc chạy.
/// Bóng nền có hai loại: bóng thường (SoftBubble.prefab) và bóng đá (RockBubble.prefab, variant với SoftBubbleConfig_Rock, vẽ như
/// viên ngọc trai đặc bằng material M_SoftBubbleCartoonPearl trên vỏ; RockVisual là node trống giữ chỗ cho nội dung sau này, tự scale
/// theo bán kính; không có cá).
/// Vỏ HP (MultiHpBubble, visual băng): ShowShell đổi material sang vỏ mờ và hiện HpText; mỗi bể hoàn thành BoardManager gọi UpdateHp,
/// về 0 thì BreakShell trả material nền, ẩn số, nhún. Không đổi config vật lý.
/// Băng (isIce, visual là cụm khói xốp M_SoftBubbleSmoke) là lớp phủ ngoài cùng lên bóng nền (kể cả bóng đang có vỏ HP): Freeze nhớ
/// config + material hiện có, đổi sang khói và tắt toàn bộ nội dung bên trong (đàn cá, RockVisual, ContentVisuals, HpText) để không lộ
/// nền. Bóng chạm đỉnh vùng chơi (TickTopContact) là bật nội dung ngay, rồi tiến độ đếm giờ đẩy vào _SmokeThin của material khói (khói
/// thủng dần từ mép vào, cá lộ qua lỗ); rời đỉnh thì khói đầy lại dần (SmokeRefillSpeed), đầy hẳn mới ẩn nội dung lại. Chạm đủ lâu thì
/// BreakIce trả lại config + material đã nhớ (nền hoặc vỏ HP), khói tan êm (không nổ, không VFX bóng vỡ, không nhún), BoardManager phát
/// SfxSmokeClear: _SmokeThin đi tiếp tới 1 trong CoverDissolveDuration với tốc độ nối liền pha chạm đỉnh rồi tăng dần, xong thì gỡ lớp khói.
/// Lớp phủ vỡ (băng ở BreakIce, vỏ HP ở BreakShell) không biến mất ngay mà bung ra đồng thời tan (PlayCoverDissolve): material của nó ở
/// lại làm lớp phủ thứ hai của mesh, trong CoverDissolveDuration giây _Inflate đẩy lớp phủ phình thêm CoverBurstScale bán kính còn
/// _Dissolve chạy 0 → 1, rồi gỡ; trong lúc đó cá của bóng chưa bấm được (IsCoverDissolving).
/// MultilayerBubble: còn lớp ẩn thì có đúng một bong bóng lồng bên trong, nhỏ hơn bóng nền một chút (SetHiddenLayers, InnerBubbleRadius),
/// đẩy nhẹ ra sau để vẽ trước bóng nền; lộ lớp hoặc AutoFill rút hết một lớp thì quả bung ra đồng thời tan (PlayInnerBurst) và quả mới
/// phình lên nếu vẫn còn lớp ẩn, hết lớp ẩn thì không còn quả và bóng là bóng thường. Băng phủ lên thì quả tắt cùng nội dung.
/// TapCollider (SphereCollider trigger, layer Fish) để bấm vào bóng đá / băng / vỏ HP cho phản hồi.
/// </summary>
[RequireComponent(typeof(SoftBubble), typeof(BubbleFishContainer))]
public class BubbleView : MonoBehaviour
{
    private static readonly Dictionary<Collider, BubbleView> ByCollider = new();

    [field: SerializeField, Tooltip("SoftBubble cùng GameObject (gán sẵn trên prefab)")]
    public SoftBubble Bubble { get; private set; }

    [field: SerializeField, Tooltip("BubbleFishContainer cùng GameObject (gán sẵn trên prefab)")]
    public BubbleFishContainer FishContainer { get; private set; }

    [field: SerializeField, Tooltip("Bóng đá: node visual con của bóng (hiện trống, ngọc trai vẽ bằng material vỏ), localScale tự đặt = bán kính bóng × RockVisualScale. Trống = không phải bóng đá")]
    public Transform RockVisual { get; private set; }

    [field: SerializeField, Range(0.1f, 3f), Tooltip("Bóng đá: localScale của RockVisual so với bán kính bóng (visual dựng theo bán kính 1 thì 1 = vừa khít bóng)")]
    public float RockVisualScale { get; private set; } = 1.2f;

    [field: SerializeField, Tooltip("Visual nội dung bên trong bóng (khoá, chìa, vật cản sau này): băng phủ lên thì tắt hết, vỡ băng bật lại. Đàn cá, RockVisual và HpText được xử lý sẵn")]
    public GameObject[] ContentVisuals { get; private set; } = System.Array.Empty<GameObject>();

    [field: SerializeField, Tooltip("MultiHpBubble: chữ HP con của bóng (TextMeshPro world), tự đặt trước mặt bóng và scale theo bán kính; ẩn khi không có vỏ")]
    public TMP_Text HpText { get; private set; }

    [field: SerializeField, Range(0.1f, 2f), Tooltip("MultiHpBubble: cỡ chữ HP so với bán kính bóng (localScale = bán kính × giá trị này)")]
    public float HpTextScale { get; private set; } = 0.6f;

    [field: SerializeField, Range(0.1f, 2f), Tooltip("Ổ khoá / chìa: cỡ icon so với bán kính bóng")]
    public float LockVisualScale { get; private set; } = 0.9f;

    [field: SerializeField, Tooltip("Khoá chìa: root model ổ khoá (prefab Lock_FishSort lồng bên trong), bám mặt trước bóng. Trống = không hiện ổ (vẫn khoá gameplay)")]
    public Transform KeyLockVisual { get; private set; }


    [field: SerializeField, Tooltip("Khoá đơn: root gắn cá mẫu. Trống = tự tạo con Order lúc chạy")]
    public Transform OrderVisual { get; private set; }

    [field: SerializeField, Range(0.05f, 2f), Tooltip("Khoá đơn: cỡ cá mẫu so với OrderVisual")]
    public float OrderFishScale { get; private set; } = 0.3f;

    [field: SerializeField, Tooltip("Khoá đơn: Parent Transform preview icon")]
    public Transform OrderFishTransform { get; private set; }
    
    [field: SerializeField, Tooltip("Prefab VFX bóng vỡ (BubbleBreakFx, pool) chạy tại tâm lúc bóng hết cá nổ, vỡ băng / vỡ vỏ HP; scale theo bán kính bóng (tuỳ chọn)")]
    public BubbleBreakEffect BreakVfx { get; private set; }

    [field: SerializeField, Tooltip("Collider bấm (SphereCollider trigger trên object con layer Fish), bán kính tự theo bóng; dùng cho phản hồi khi bấm bóng đá / băng / vỏ HP")]
    public SphereCollider TapCollider { get; private set; }

    [field: SerializeField, Range(0f, 0.5f), Tooltip("Bóng phình thêm bấy nhiêu phần lúc bị bấm / vỡ băng / vỡ vỏ (0.08 = +8%), đá nhún gấp đôi")]
    public float TapPulseScale { get; private set; } = 0.08f;

    [field: SerializeField, Min(0.05f), Tooltip("Thời gian nhún (giây)")]
    public float TapPulseDuration { get; private set; } = 0.35f;

    [field: SerializeField, Min(0f), Tooltip("Băng (khói) vỡ: _SmokeThin đi tiếp từ độ tan lúc vỡ tới 1 trong bấy nhiêu giây, khởi đầu đúng tốc độ pha chạm đỉnh rồi tăng dần (0.75 s = giữ nguyên tốc độ suốt, ngắn hơn = cuối nhanh hơn). Vỏ HP vỡ: lớp phủ bung ra đồng thời tan (_Inflate + _Dissolve 0 → 1) trong bấy nhiêu giây. Cá bị khoá bấm tới khi tan xong. 0 = biến mất ngay")]
    public float CoverDissolveDuration { get; private set; } = 0.5f;

    [field: SerializeField, Range(0f, 1f), Tooltip("Băng / vỏ HP vỡ: lớp phủ phình thêm bấy nhiêu phần bán kính trong lúc tan (0.3 = +30%, bung nhanh lúc đầu rồi chậm dần). 0 = chỉ tan tại chỗ")]
    public float CoverBurstScale { get; private set; } = 0.3f;

    [field: SerializeField, Tooltip("Băng: material vẽ phủ lên khối gum (slot 1: viền, vệt sáng, nét mép) để thành \"gum trong bong bóng\". Trống = dùng material nền của prefab")]
    public Material CoverOverlayMaterial { get; private set; }

    [field: SerializeField, Tooltip("MultilayerBubble: mesh quả cầu (đường kính 1) cho các bong bóng lồng bên trong, mỗi lớp ẩn một quả. Trống = Sphere built-in")]
    public Mesh InnerBubbleMesh { get; private set; }

    [field: SerializeField, Range(0.5f, 0.98f), Tooltip("MultilayerBubble: quả lồng bên trong to bằng bấy nhiêu phần bán kính bóng (chỉ nhỏ hơn một chút để thấy bóng có nhiều lớp)")]
    public float InnerBubbleRadius { get; private set; } = 0.86f;

    [field: SerializeField, Min(0f), Tooltip("MultilayerBubble: lộ lớp thì quả lồng ngoài cùng bung ra đồng thời tan (_Inflate + _Dissolve 0 → 1) trong bấy nhiêu giây. 0 = biến mất ngay")]
    public float InnerBurstDuration { get; private set; } = 0.35f;

    [field: SerializeField, Range(0f, 1f), Tooltip("MultilayerBubble: quả lồng phình thêm bấy nhiêu phần bán kính của nó trong lúc tan (bung nhanh lúc đầu rồi chậm dần)")]
    public float InnerBurstScale { get; private set; } = 0.3f;

    public BoardManager Board { get; private set; }
    public BubbleEntry Entry { get; private set; }
    public bool IsBound => Entry != null;
    public bool IsFront => Entry != null && Board != null && Board.Model != null && Board.Model.IsFront(Entry);

    /// <summary>Bóng đá: theo entry khi đã bind, trước đó theo prefab (có RockVisual).</summary>
    public bool IsRock => Entry != null ? Entry.IsRock : RockVisual != null;

    /// <summary>Bóng có lớp băng lúc sinh (đã vỡ hay chưa).</summary>
    public bool IsIce => Entry != null ? Entry.IsIce : frozen;

    /// <summary>Còn lớp băng: nội dung bên trong bị che và không bấm được.</summary>
    public bool IsFrozen => Entry != null ? Entry.IsFrozen : frozen;

    /// <summary>Lớp phủ (băng / vỏ HP) vừa vỡ còn đang tan (PlayCoverDissolve): cá bên dưới còn bị che dở nên BoardManager chưa cho bấm.</summary>
    public bool IsCoverDissolving => dissolveHandle.IsActive();

    /// <summary>Ổ khoá còn hiện trên bóng: model có thể đã mở (bóng chìa nổ) nhưng token chìa còn đang bay tới, BoardManager chặn bấm tới khi ổ mở hình.</summary>
    public bool IsKeyLockShown => keyLockShown;

    /// <summary>Còn vỏ HP: cá bên trong chưa bấm được.</summary>
    public bool IsShelled => Entry != null ? Entry.Hp > 0 : shellActive;

    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static readonly int InflateId = Shader.PropertyToID("_Inflate");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int SmokeThinId = Shader.PropertyToID("_SmokeThin");

    private MeshRenderer innerBubble;      // quả lồng đang có (null = không còn lớp ẩn); quả đang bung tan không còn ở đây
    private float innerGrow = 1f;          // quả mới phình từ 0 lên 1 thay quả vừa bung
    private MotionHandle innerGrowHandle;
    private int hiddenLayerCount;          // số lớp ẩn lần SetHiddenLayers gần nhất, để biết vừa lộ lớp
    private Transform innerRoot;
    private Material innerMaterial;

    private MotionHandle tapHandle;
    private MotionHandle dissolveHandle;
    private MaterialPropertyBlock dissolveBlock;
    private Material frozenMaterial;
    private Material shellMaterial;
    private float tapPunch;
    private bool frozen;
    private bool shellActive;
    private bool keyLockShown;
    private bool orderShown;

    private Fish orderPreview;
    private int orderPreviewFishId;
    private Material baseMaterial;
    private Material prefabMaterial;   // material nền của prefab (bóng thường): lớp phim bóng phủ lên gum khi đóng băng (Freeze)
    private Material lockVeilMaterial;     // lớp kính mờ đang làm material nền vì bóng còn khoá chìa (null = không phủ)
    private Material preLockBaseMaterial;  // material nền trước khi phủ kính mờ, trả lại khi mở khoá
    private SoftBubbleConfig thawedConfig;
    private Material thawedMaterial;
    private float topHoldTime;
    private float smokeThin;                  // _SmokeThin đang đặt trên renderer (0 = khói đầy)
    private bool contentVisible = true;       // trạng thái SetContentVisible gần nhất, để TickTopContact không gọi lại mỗi frame
    private float holdThinSpeed;              // tốc độ _SmokeThin của pha chạm đỉnh (SmokeHoldThinMax / holdTime), để pha tan nối tiếp cùng tốc độ
    private MaterialPropertyBlock smokeBlock; // block chung của renderer (giữ _Seed do SoftBubble gán) cộng _SmokeThin

    public static bool TryGetByCollider(Collider collider, out BubbleView view)
    {
        if (collider != null && ByCollider.TryGetValue(collider, out view) && view != null) return true;
        view = null;
        return false;
    }

    private void Awake()
    {
        if (Bubble != null && Bubble.MeshRenderer != null) prefabMaterial = baseMaterial = Bubble.MeshRenderer.sharedMaterial;
        if (HpText != null) HpText.gameObject.SetActive(false);
        if (KeyLockVisual != null) KeyLockVisual.gameObject.SetActive(false);
        if (OrderVisual != null) OrderVisual.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (TapCollider != null) ByCollider[TapCollider] = this;
    }

    private void OnDisable()
    {
        if (TapCollider != null && ByCollider.TryGetValue(TapCollider, out BubbleView owner) && owner == this) ByCollider.Remove(TapCollider);
    }

    private void OnDestroy()
    {
        tapHandle.TryCancel();
        dissolveHandle.TryCancel();
        innerGrowHandle.TryCancel();
        ClearOrderPreview();
    }

    /// <summary>Đá, collider, chữ HP và bong bóng lồng bám theo bán kính hiện tại (bóng phình lúc spawn, nhún lúc bấm). Bóng thường không có gì để làm.</summary>
    private void LateUpdate()
    {
        if (Bubble == null || (RockVisual == null && TapCollider == null && HpText == null && innerRoot == null)) return;
        float radius = Bubble.CurrentRadius;
        SyncInnerBubble(radius);
        if (RockVisual != null) RockVisual.localScale = Vector3.one * (radius * RockVisualScale * (1f + tapPunch));
        if (TapCollider != null) TapCollider.radius = radius;
        if (HpText != null && HpText.gameObject.activeSelf)
        {
            // Chữ đứng trước mặt bóng (camera nhìn theo +Z), không xoay theo bóng.
            HpText.transform.position = Bubble.Center + Vector3.back * (radius + 0.1f);
            HpText.transform.rotation = Quaternion.identity;
            HpText.transform.localScale = Vector3.one * (radius * HpTextScale * (1f + tapPunch));
        }

        AlignFrontOverlay(KeyLockVisual, radius);
        AlignFrontOverlay(OrderVisual, radius);
    }

    public void Bind(BoardManager board, BubbleEntry entry)
    {
        Board = board;
        Entry = entry;
    }

    public void Unbind()
    {
        Entry = null;
        Board = null;
    }

    public int IndexOf(Fish fish) => FishContainer != null ? FishContainer.IndexOf(fish) : -1;

    /// <summary>
    /// Đổi material nền của bóng (FishSchoolBubble có material riêng). Level gọi ngay sau spawn, trước ShowShell / Freeze, để vỏ HP vỡ
    /// và băng vỡ đều trả về đúng material này. Null = giữ material của prefab.
    /// </summary>
    public void SetBaseMaterial(Material material)
    {
        if (material == null) return;
        baseMaterial = material;
        if (!shellActive) SetSurfaceMaterial(material);
    }

    // ---------- Bong bóng lồng (MultilayerBubble) ----------

    /// <summary>
    /// MultilayerBubble: còn lớp ẩn (count > 0) thì có đúng một bong bóng lồng bên trong, nhỏ hơn bóng nền một chút (InnerBubbleRadius)
    /// để thấy bóng có nhiều lớp, đẩy nhẹ ra sau tâm để vẽ trước bóng nền. Level gọi lúc spawn kèm material; BoardManager gọi lại sau khi
    /// lộ lớp / AutoFill rút bớt lớp: count giảm thì quả đang có bung ra đồng thời tan (PlayInnerBurst), còn lớp ẩn thì quả mới phình lên
    /// từ tâm thay vào, hết lớp ẩn thì không còn quả (bóng thường). material null = giữ material đang có. Gọi thừa vô hại.
    /// </summary>
    public void SetHiddenLayers(int count, Material material = null)
    {
        if (material != null) innerMaterial = material;
        count = Mathf.Max(0, count);
        bool revealed = count < hiddenLayerCount;
        hiddenLayerCount = count;
        if (innerBubble != null && (revealed || count == 0))
        {
            PlayInnerBurst(innerBubble);
            innerBubble = null;
        }

        if (count > 0 && innerBubble == null) CreateInnerBubble(revealed);
        if (Bubble != null) SyncInnerBubble(Bubble.CurrentRadius);
    }

    /// <summary>Sinh quả lồng; grow = phình từ 0 lên cỡ thật trong InnerBurstDuration (quả mới thay quả vừa bung), không thì đủ cỡ ngay.</summary>
    private void CreateInnerBubble(bool grow)
    {
        if (innerRoot == null)
        {
            var root = new GameObject("Layers");
            innerRoot = root.transform;
            innerRoot.SetParent(transform, false);
            innerRoot.localScale = Vector3.one;
            innerRoot.gameObject.SetActive(!frozen);   // băng đang phủ thì quả tắt cùng nội dung
        }

        var go = new GameObject("Layer");
        go.layer = gameObject.layer;
        go.transform.SetParent(innerRoot, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = InnerBubbleMesh != null ? InnerBubbleMesh : Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        Material material = innerMaterial;
        if (material == null && Bubble != null && Bubble.MeshRenderer != null) material = Bubble.MeshRenderer.sharedMaterial;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var block = new MaterialPropertyBlock();
        block.SetFloat(SeedId, Random.value * 100f);   // noise tan mỗi quả một kiểu
        renderer.SetPropertyBlock(block);
        innerBubble = renderer;

        innerGrowHandle.TryCancel();
        innerGrow = grow && InnerBurstDuration > 0f ? 0f : 1f;
        if (innerGrow < 1f)
        {
            innerGrowHandle = LMotion.Create(0f, 1f, InnerBurstDuration).WithEase(Ease.OutCubic).Bind(v => innerGrow = v).AddTo(this);
        }
    }

    /// <summary>Quả lồng bám tâm bóng, bán kính = InnerBubbleRadius × bán kính bóng (kể cả nhún) × innerGrow, lùi ra sau một chút để vẽ trước bóng nền.</summary>
    private void SyncInnerBubble(float radius)
    {
        if (innerRoot == null || Bubble == null) return;
        innerRoot.position = Bubble.Center;
        innerRoot.rotation = Quaternion.identity;
        if (innerBubble == null) return;
        float parentScale = Mathf.Max(1e-4f, transform.lossyScale.x);
        float r = radius * (1f + tapPunch) * InnerBubbleRadius * innerGrow;
        // Lùi ra sau (camera nhìn theo +Z) để sắp xếp theo khoảng cách vẽ quả lồng trước bóng nền.
        innerBubble.transform.localPosition = new Vector3(0f, 0f, radius * 0.06f / parentScale);
        innerBubble.transform.localScale = Vector3.one * (2f * r / parentScale);
    }

    /// <summary>Quả lồng vừa rời danh sách bung ra đồng thời tan tại chỗ (_Inflate + _Dissolve trên property block riêng) rồi huỷ.</summary>
    private void PlayInnerBurst(MeshRenderer inner)
    {
        if (inner == null) return;
        GameObject go = inner.gameObject;
        if (InnerBurstDuration <= 0f || inner.sharedMaterial == null || !inner.sharedMaterial.HasProperty(DissolveId))
        {
            Destroy(go);
            return;
        }

        var block = new MaterialPropertyBlock();
        inner.GetPropertyBlock(block);
        float burst = 0.5f * InnerBurstScale;   // mesh bán kính 0.5 đơn vị object: phình thêm InnerBurstScale phần bán kính của quả
        LMotion.Create(0f, 1f, InnerBurstDuration)
            .WithOnComplete(() => { if (go != null) Destroy(go); })
            .Bind(value =>
            {
                if (inner == null) return;
                float inv = 1f - value;
                block.SetFloat(DissolveId, value);
                block.SetFloat(InflateId, burst * (1f - inv * inv * inv));
                inner.SetPropertyBlock(block);
            })
            .AddTo(this);
    }

    // ---------- Vỏ HP ----------

    /// <summary>
    /// Bật vỏ HP: material vỏ (null = giữ material đang có), chữ HP, và cá bên trong chỉ hiện bóng đen bằng fishSilhouette (null = cá vẫn
    /// hiện mờ qua vỏ như thường). Level gọi ngay sau spawn, trước Freeze.
    /// </summary>
    public void ShowShell(int hp, Material material, Material fishSilhouette = null)
    {
        if (Bubble == null || hp <= 0) return;
        shellActive = true;
        shellMaterial = material;
        if (material != null) SetSurfaceMaterial(material);
        if (FishContainer != null) FishContainer.SetSilhouette(fishSilhouette);
        if (HpText != null)
        {
            HpText.text = hp.ToString();
            HpText.gameObject.SetActive(!frozen);
        }
    }

    /// <summary>Bể vừa hoàn thành: đổi số HP; về 0 thì vỡ vỏ.</summary>
    public void UpdateHp(int hp)
    {
        if (!shellActive) return;
        if (hp <= 0)
        {
            BreakShell();
            return;
        }

        if (HpText != null) HpText.text = hp.ToString();
    }

    /// <summary>
    /// Vỏ HP vỡ: trả material nền (băng đang phủ thì chỉ đổi material sẽ trả lại khi băng vỡ, không có hiệu ứng vì vỏ đang bị che), ẩn
    /// số, vỏ bung ra đồng thời tan (PlayCoverDissolve), VFX, nhún.
    /// </summary>
    public void BreakShell()
    {
        if (!shellActive) return;
        shellActive = false;
        SetSurfaceMaterial(baseMaterial);
        if (FishContainer != null) FishContainer.SetSilhouette(null);   // cá hiện hình thật ngay dưới lớp vỏ đang tan
        if (HpText != null) HpText.gameObject.SetActive(false);
        if (!frozen)
        {
            PlayCoverDissolve(shellMaterial);
            PlayBreakVfx();
            PlayTapFeedback();
        }
    }

    /// <summary>Đổi material bề mặt của bóng nền. Đang băng thì băng giữ material của nó, chỉ nhớ material này để trả lại khi băng vỡ.</summary>
    private void SetSurfaceMaterial(Material material)
    {
        if (material == null || Bubble == null || Bubble.MeshRenderer == null) return;
        if (frozen) thawedMaterial = material;
        else if (Bubble.MeshRenderer.sharedMaterial != material) Bubble.MeshRenderer.sharedMaterial = material;
    }

    // ---------- Lớp băng ----------

    /// <summary>
    /// Phủ băng lên bóng nền: nhớ config + material hiện có (nền hoặc vỏ HP), đổi sang config / material băng (null = giữ), che toàn bộ
    /// nội dung bên trong. Lớp băng (visual gum) là khối mờ nên vẽ thêm CoverOverlayMaterial (trống = material nền của prefab) phủ lên
    /// (slot 1: viền, vệt sáng, nét mép) để thành "gum trong bong bóng", hợp với môi trường nước. Level gọi ngay sau spawn khi
    /// Entry.IsFrozen; gọi lại khi đang băng thì bỏ qua.
    /// </summary>
    public void Freeze(SoftBubbleConfig iceConfig, Material iceMaterial)
    {
        if (frozen || Bubble == null) return;
        frozen = true;
        topHoldTime = 0f;
        thawedConfig = Bubble.Config;
        thawedMaterial = Bubble.MeshRenderer != null ? Bubble.MeshRenderer.sharedMaterial : null;
        frozenMaterial = iceMaterial;
        if (iceConfig != null) Bubble.SetConfig(iceConfig);
        if (iceMaterial != null && Bubble.MeshRenderer != null)
        {
            Material overlay = CoverOverlayMaterial != null ? CoverOverlayMaterial : prefabMaterial;
            Bubble.MeshRenderer.sharedMaterials = overlay != null && overlay != iceMaterial
                ? new[] { iceMaterial, overlay }
                : new[] { iceMaterial };
        }

        SetSmokeThin(0f);
        SetContentVisible(false);
    }

    /// <summary>
    /// Đếm thời gian mép trên bóng chạm đỉnh vùng chơi (BoardManager tính touching mỗi frame khi còn băng). Cộng dồn đủ holdTime giây
    /// thì trả true = băng vỡ; rời đỉnh (nảy, bị đẩy xuống) thì giờ đếm tụt dần nhanh gấp SmokeRefillSpeed lần tốc độ đếm chứ không
    /// reset ngay, nên khói đầy lại từ từ và bóng nảy ở trần vẫn cộng dồn được. Tiến độ đếm đẩy vào _SmokeThin: khói thủng dần khi chạm
    /// đỉnh. Thứ tự nhìn thấy là chạm đỉnh → bật nội dung ngay → khói tan: frame đầu chạm đỉnh đã bật cá để lộ qua lỗ khói đang mở;
    /// rời đỉnh tới khi khói đầy lại hẳn (giờ đếm về 0) mới ẩn nội dung lại.
    /// </summary>
    public bool TickTopContact(bool touching, float holdTime, float dt)
    {
        if (Bubble == null) return false;
        topHoldTime = touching ? topHoldTime + dt : Mathf.Max(0f, topHoldTime - dt * SmokeRefillSpeed);
        holdThinSpeed = holdTime > 0f ? SmokeHoldThinMax / holdTime : 0f;
        SetSmokeThin(holdTime > 0f ? SmokeHoldThinMax * Mathf.Clamp01(topHoldTime / holdTime) : 0f);
        if (touching != contentVisible && (touching || topHoldTime <= 0f)) SetContentVisible(touching);
        return topHoldTime >= holdTime;
    }

    /// <summary>Chạm đỉnh đủ giờ thì khói mới tan tới mức này (múi co, hé nước); phần còn lại tan trong PlayCoverDissolve khi cá đã hiện, để thấy cá lộ dần qua khói.</summary>
    private const float SmokeHoldThinMax = 0.5f;

    /// <summary>Rời đỉnh thì giờ đếm (và độ tan của khói) tụt nhanh gấp bấy nhiêu lần tốc độ đếm: khói đầy lại trong khoảng holdTime / giá trị này giây.</summary>
    private const float SmokeRefillSpeed = 2f;

    /// <summary>Lớp phủ băng đang dùng là khói (material có _SmokeThin): vỡ thì tan êm thay vì bung và nổ.</summary>
    private bool IsSmokeCover => frozenMaterial != null && frozenMaterial.HasProperty(SmokeThinId);

    /// <summary>
    /// Đặt _SmokeThin (0 = khói đầy, 1 = tan hết) lên block chung của renderer, giữ nguyên _Seed mà SoftBubble đã gán. Material lớp
    /// phủ không có _SmokeThin (vỏ khác khói) thì bỏ qua.
    /// </summary>
    private void SetSmokeThin(float value)
    {
        if (Bubble == null || Bubble.MeshRenderer == null || !IsSmokeCover) return;
        if (Mathf.Approximately(value, smokeThin)) return;
        smokeThin = value;
        MeshRenderer renderer = Bubble.MeshRenderer;
        smokeBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(smokeBlock);
        smokeBlock.SetFloat(SmokeThinId, value);
        renderer.SetPropertyBlock(smokeBlock);
    }

    /// <summary>
    /// Băng vỡ (model đã bỏ cờ qua BoardModel.TryBreakIce): trả config + material đã nhớ, chắc chắn nội dung (cá, đá, vỏ HP) đang bật
    /// (TickTopContact đã bật từ frame đầu chạm đỉnh). Lớp khói chỉ tan êm từ đúng độ tan đang có (PlayCoverDissolve kéo _SmokeThin tới 1
    /// nối tiếp tốc độ pha chạm đỉnh), không bung, không nhún, không VFX bóng vỡ; lớp phủ không phải khói thì bung ra tan và nổ như cũ.
    /// </summary>
    public void BreakIce()
    {
        if (!frozen) return;
        frozen = false;
        topHoldTime = 0f;
        if (Bubble != null)
        {
            if (thawedConfig != null) Bubble.SetConfig(thawedConfig);
            MeshRenderer renderer = Bubble.MeshRenderer;
            if (renderer != null) renderer.sharedMaterials = new[] { thawedMaterial != null ? thawedMaterial : renderer.sharedMaterial };   // bỏ luôn lớp màng phủ
        }

        SetContentVisible(true);
        PlayCoverDissolve(frozenMaterial);
    }

    /// <summary>
    /// Lớp phủ vừa vỡ (cover = material băng hoặc vỏ HP) bung ra đồng thời tan thay vì biến mất ngay: cover thành lớp phủ thứ hai của cùng
    /// mesh (slot 1, vẽ sau material nền nên đè lên bóng + nội dung, lõm theo bóng mềm). Trên riêng slot đó _Dissolve chạy 0 → 1 còn
    /// _Inflate đẩy đỉnh theo pháp tuyến tới CoverBurstScale × bán kính (ease-out: bung nhanh lúc đầu), xong thì gỡ lớp phủ. Slot 0 vẫn là
    /// material nền nên SetSurfaceMaterial giữa chừng không đụng lớp phủ. Mỗi lúc chỉ một lớp tan: vỏ HP vỡ đúng lúc băng còn đang tan thì
    /// vỏ chỉ đổi material nền như cũ. Cover không có _Dissolve hoặc duration = 0 thì bỏ qua.
    /// Khói (cover có _SmokeThin): không bung, chỉ kéo _SmokeThin từ độ tan đang có tới 1 theo đường bậc hai thin = start + còn lại ×
    /// (k·u + (1 − k)·u²), k chọn sao cho tốc độ khởi đầu đúng bằng tốc độ pha chạm đỉnh (holdThinSpeed) rồi tăng dần tới cuối, nên
    /// mắt không thấy khựng hay nhảy tốc độ ở frame vỡ; CoverDissolveDuration dài quá (k chạm 1) thì chạy tuyến tính chậm hơn. Cá đã
    /// hiện bên dưới từ lúc vỡ nên lộ dần qua lỗ khói.
    /// </summary>
    private void PlayCoverDissolve(Material cover)
    {
        MeshRenderer renderer = Bubble != null ? Bubble.MeshRenderer : null;
        if (renderer == null || cover == null || CoverDissolveDuration <= 0f || dissolveHandle.IsActive()) return;
        bool smoke = cover.HasProperty(SmokeThinId);
        if (!smoke && !cover.HasProperty(DissolveId)) return;
        renderer.sharedMaterials = new[] { renderer.sharedMaterial, cover };
        if (smoke)
        {
            float start = smokeThin;
            float remaining = 1f - start;
            float k = holdThinSpeed > 0f && remaining > 0f ? Mathf.Clamp01(holdThinSpeed * CoverDissolveDuration / remaining) : 1f;
            dissolveHandle = LMotion.Create(0f, 1f, CoverDissolveDuration)
                .WithOnComplete(EndCoverDissolve)
                .Bind(u =>
                {
                    if (this != null) SetSmokeThin(start + remaining * (k * u + (1f - k) * u * u));
                })
                .AddTo(this);
            return;
        }

        dissolveBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(dissolveBlock);   // block của cả renderer (SoftBubble gán _Seed): lớp phủ giữ đúng seed của bóng
        // _Inflate tính theo đơn vị object của mesh: bán kính world chia scale của bóng.
        float burst = Bubble.CurrentRadius * CoverBurstScale / Mathf.Max(1e-4f, Bubble.transform.lossyScale.x);
        dissolveHandle = LMotion.Create(0f, 1f, CoverDissolveDuration)
            .WithOnComplete(EndCoverDissolve)
            .Bind(value =>
            {
                if (this == null || renderer == null) return;
                float inv = 1f - value;
                dissolveBlock.SetFloat(DissolveId, value);
                dissolveBlock.SetFloat(InflateId, burst * (1f - inv * inv * inv));
                renderer.SetPropertyBlock(dissolveBlock, 1);
            })
            .AddTo(this);
    }

    private void EndCoverDissolve()
    {
        if (this == null || Bubble == null || Bubble.MeshRenderer == null) return;
        MeshRenderer renderer = Bubble.MeshRenderer;
        if (renderer.sharedMaterials.Length < 2) return;
        renderer.SetPropertyBlock(null, 1);
        renderer.sharedMaterials = new[] { renderer.sharedMaterial };
    }

    /// <summary>Bật / tắt mọi thứ bên trong bóng: đàn cá, RockVisual, ContentVisuals, chữ HP (chỉ bật lại khi còn vỏ). Băng che thì tắt để không lộ nền.</summary>
    public void SetContentVisible(bool visible)
    {
        contentVisible = visible;
        if (FishContainer != null) FishContainer.SetVisible(visible);
        if (RockVisual != null && RockVisual.gameObject.activeSelf != visible) RockVisual.gameObject.SetActive(visible);
        if (innerRoot != null && innerRoot.gameObject.activeSelf != visible) innerRoot.gameObject.SetActive(visible);
        foreach (GameObject content in ContentVisuals)
        {
            if (content != null && content.activeSelf != visible) content.SetActive(visible);
        }

        if (HpText != null) HpText.gameObject.SetActive(visible && shellActive);
        RefreshOverlays();
    }

    /// <summary>
    /// Phát VFX bóng vỡ (BreakVfx, pool) tại tâm bóng, scale theo bán kính hiện tại (bóng nổ: đã phình BubblePopScale; băng / vỏ HP:
    /// bán kính thường). Instance pool nằm dưới parent của bóng (BubbleRoot của Level) nên mất theo level. Không có prefab thì bỏ qua.
    /// BoardManager gọi ngay trước khi DespawnBubble; BreakIce / BreakShell gọi cùng lúc lớp phủ bung ra tan; UpdateKeyLock gọi khi chìa
    /// bay tới làm lớp khoá ngoài vỡ.
    /// </summary>
    public void PlayBreakVfx()
    {
        if (BreakVfx == null || Bubble == null) return;
        float radius = Mathf.Max(Bubble.CurrentRadius, Bubble.TargetRadius, 0.1f);
        BubbleBreakEffect.Play(BreakVfx, Bubble.Center, transform.parent, radius);
    }

    // ---------- Phản hồi bấm ----------

    /// <summary>
    /// Bóng đá / băng / vỏ HP bị bấm (hoặc vừa vỡ): bóng phình nhẹ rồi hồi theo hình sin (SetRadius tức thời mỗi frame, như tween lớn
    /// lúc spawn) và đá nhún theo. Đang tween bán kính (vừa spawn) thì chỉ nhún đá, không đụng bán kính để không cắt tween.
    /// Nhịp đang chạy thì bỏ qua lần bấm mới: bấm dồn không được hủy nhịp cũ rồi lấy TargetRadius đang phình làm bán kính gốc, vì
    /// thế bóng sẽ to dần theo mỗi lần bấm. Nếu trong lúc nhún có ai đổi bán kính (bóng nổ, cá bay đi) thì nhường, không trả về gốc.
    /// </summary>
    public void PlayTapFeedback()
    {
        if (Bubble == null || tapHandle.IsActive()) return;
        tapPunch = 0f;
        bool pulseRadius = !Bubble.IsResizing && TapPulseScale > 0f;
        float restRadius = Bubble.TargetRadius;   // bán kính gốc, chỉ lấy khi không có nhịp nào chạy
        float lastSet = restRadius;               // giá trị mình đặt gần nhất, lệch là do người khác đổi
        tapHandle = LMotion.Create(0f, 1f, TapPulseDuration)
            .WithOnComplete(() =>
            {
                if (this == null) return;
                tapPunch = 0f;
                if (pulseRadius && Bubble != null && !Bubble.IsResizing && Mathf.Approximately(Bubble.TargetRadius, lastSet)) Bubble.SetRadius(restRadius);
            })
            .Bind(u =>
            {
                if (this == null) return;
                float s = Mathf.Sin(u * Mathf.PI);
                tapPunch = s * TapPulseScale * 2f;
                if (!pulseRadius || Bubble == null) return;
                if (Bubble.IsResizing || !Mathf.Approximately(Bubble.TargetRadius, lastSet))
                {
                    pulseRadius = false;   // bán kính bị đổi từ ngoài: thôi nhún bán kính, chỉ còn đá / chữ nhún
                    return;
                }

                lastSet = restRadius * (1f + TapPulseScale * s);
                Bubble.SetRadius(lastSet);
            })
            .AddTo(this);
    }

    #region Lock/Key

    // ---------- Ổ khoá / chìa ----------

    /// <summary>
    /// Khoá chìa: model ổ khoá (KeyLockVisual) và config cứng. lockMaterial = màu của cặp (GamePlayGlobalConfig.KeyLockMaterials theo
    /// BubbleEntry.KeyPairIndex) gán cho mọi renderer dưới KeyLockVisual; null = giữ material trên prefab. veilMaterial = lớp kính mờ
    /// (M_SoftBubbleCartoonLock: fill navy mờ phủ đều) làm material nền suốt lúc còn khoá để ổ nổi hơn cá và nền phía sau: vỏ HP / băng
    /// vỡ đều trả về nó, mở khoá thì trả material nền trước đó (RestoreLockVeil). Ổ khoá nằm trước mặt bóng nên không bị mờ. Không dùng
    /// overlay đơn.
    /// </summary>
    public void ShowKeyLock(SoftBubbleConfig hardConfig, Material lockMaterial = null, Material veilMaterial = null)
    {
        keyLockShown = true;
        ApplyConfig(hardConfig);
        if (veilMaterial != null && lockVeilMaterial == null)
        {
            preLockBaseMaterial = baseMaterial;
            lockVeilMaterial = veilMaterial;
            baseMaterial = veilMaterial;
            if (!shellActive) SetSurfaceMaterial(veilMaterial);   // đang băng thì chỉ nhớ, băng vỡ mới hiện
        }

        if (KeyLockVisual != null)
        {
            if (lockMaterial != null)
            {
                foreach (Renderer lockRenderer in KeyLockVisual.GetComponentsInChildren<Renderer>(true)) lockRenderer.sharedMaterial = lockMaterial;
            }

            SetActive(KeyLockVisual, keyLockShown);
        }
    }

    /// <summary>
    /// Bóng chìa: token chìa bơi trong đàn (BubbleFishContainer.ShowKey), ẩn theo vỏ HP / khói như nội dung. keyMaterial = màu của cặp
    /// (cùng màu ổ khoá nó mở); null = giữ material trên prefab. Bóng vẫn thường, không khoá, không đổi config / material.
    /// </summary>
    public void ShowKey(Material keyMaterial = null)
    {
        if (FishContainer != null) FishContainer.ShowKey(keyMaterial);
    }

    /// <summary>Khoá đơn: cá mẫu loài đơn + config cứng. Level gọi sau ShowShell, trước Freeze.</summary>
    public void ShowOrder(int orderFishId, SoftBubbleConfig hardConfig)
    {
        orderShown = true;
        ApplyConfig(hardConfig);
        SetOrderFish(orderFishId);
        if (OrderVisual != null)
        {
            SetActive(OrderVisual, orderShown);
        }
    }

    /// <summary>Đồng bộ khoá chìa với model. Trả true đúng frame vừa mở: ổ xoay thu nhỏ, VFX vỡ, bóng nhún. Sprite chìa không đụng ở đây.</summary>
    public bool UpdateKeyLock(bool locked)
    {
        if (locked || !keyLockShown) return false;
        keyLockShown = false;
        if (KeyLockVisual != null)
        {
            SetActive(KeyLockVisual, keyLockShown);
        }
        RestoreSurface();
        RestoreLockVeil();
        PlayBreakVfx();   // lớp khoá ngoài vỡ: cùng VFX với vỏ HP / băng vỡ, tại tâm bóng, scale theo bán kính
        PlayTapFeedback();
        return true;
    }

    /// <summary>Mở khoá chìa: bỏ lớp kính mờ, material nền về như trước khi khoá (đang băng thì chỉ nhớ, băng vỡ mới trả). Không phủ thì bỏ qua.</summary>
    private void RestoreLockVeil()
    {
        if (lockVeilMaterial == null) return;
        Material previous = preLockBaseMaterial != null ? preLockBaseMaterial : prefabMaterial;
        if (thawedMaterial == lockVeilMaterial) thawedMaterial = previous;
        lockVeilMaterial = null;
        preLockBaseMaterial = null;
        baseMaterial = previous;
        if (!shellActive) SetSurfaceMaterial(previous);
    }

    /// <summary>Đồng bộ khoá đơn với model. Trả true đúng frame vừa mở: huy hiệu đơn phóng lên rồi tan, không VFX vỡ và không nhún bóng.</summary>
    public bool UpdateOrderLock(bool locked)
    {
        if (locked || !orderShown) return false;
        orderShown = false;
        if (OrderVisual != null)
        {
            SetActive(OrderVisual, orderShown);
        }
        RestoreSurface();
        PlayTapFeedback();
        return true;
    }
    private void RestoreSurface()
    {
        if (Bubble == null) return;
        if (thawedConfig != null) Bubble.SetConfig(thawedConfig);
        if (thawedMaterial != null && Bubble.MeshRenderer != null) Bubble.MeshRenderer.sharedMaterial = thawedMaterial;
    }

    private void ApplyConfig(SoftBubbleConfig hardConfig)
    {
        if (hardConfig == null || Bubble == null) return;
        thawedConfig = Bubble.Config;
        Bubble.SetConfig(hardConfig);
    }

    private void SetOrderFish(int fishId)
    {
        if (fishId < 0)
        {
            ClearOrderPreview();
            return;
        }

        if (orderPreview != null && orderPreviewFishId == fishId) return;
        ClearOrderPreview();

        Fish prefab = PrefabGlobalConfig.Instance != null ? PrefabGlobalConfig.Instance.fishPrefab : null;
        if (prefab == null || OrderVisual == null) return;

        orderPreview = Instantiate(prefab);
        orderPreview.SetAsIconPreview(fishId, OrderFishScale, OrderFishTransform);
        orderPreviewFishId = fishId;
    }

    private void ClearOrderPreview()
    {
        if (orderPreview != null)
        {
            Destroy(orderPreview.gameObject);
            orderPreview = null;
        }

        orderPreviewFishId = -1;
    }

    private void RefreshOverlays()
    {
        bool show = !frozen;
        SetActive(KeyLockVisual, show && keyLockShown);
        SetActive(OrderVisual, show && orderShown);
    }

    private static void SetActive(Transform target, bool visible)
    {
        if (target != null && target.gameObject.activeSelf != visible) target.gameObject.SetActive(visible);
    }

    private void AlignFrontOverlay(Transform overlay, float radius)
    {
        if (overlay == null || !overlay.gameObject.activeSelf || Bubble == null) return;
        overlay.position = Bubble.Center + Vector3.back * (radius + 0.12f);
        overlay.rotation = Quaternion.identity;
        overlay.localScale = Vector3.one * (radius * LockVisualScale * (1f + tapPunch));
    }

    #endregion
}