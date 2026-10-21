using System;
using System.Collections.Generic;
using TW.Utility.CustomComponent;
using UnityEngine;


[DefaultExecutionOrder(150)]
public class Fish : ABetterMonoBehaviour
{
    private static readonly Dictionary<Collider, Fish> ByCollider = new();
    private const float IconBoundsPadding = 0.25f;

    [field: SerializeField] private Transform GraphicContainer {get; set;}
    [field: SerializeField] private FishSwimmer FishSwimmer {get; set;}
    [field: SerializeField] private FishModel FishModel {get; set;}
    [field: SerializeField] private FishProceduralAnimator FishProceduralAnimator {get; set;}
    [field: SerializeField] public Transform Bubble { get; private set; }
    [field: SerializeField] public Collider TapCollider { get; private set; }
    [field: SerializeField] public HiddenBreakEffect HiddenRevealVfx { get; private set; }
    [field: SerializeField] public float HiddenRevealVfxScale { get; private set; } = 1f;

    public Vector3 BodySize { get; private set; } = Vector3.one;
    public float BaseSize { get; private set; } = 1f;
    public FishEntry Entry { get; private set; }
    public int FishId => Entry?.FishId ?? -1;
    public bool IsHidden => Entry is { IsHidden: true };
    public FishSwimmer Swimmer => FishSwimmer;
    public FishProceduralAnimator Animator => FishProceduralAnimator;
    public Transform Graphic => GraphicContainer;
    private int currentModelId = int.MinValue;
    private HeadFirstOrienter headFirst;   // chỉ khi model là FaceCameraHeadFirst; null = model xoay theo vỏ như cá
    private HeadUpPathOrienter headUp;     // chỉ khi model là HeadUpAlongPath (hải cẩu): bơi đỉnh đầu theo đường bơi, đứng yên xoay như vỏ
    private TumbleOrienter tumble;         // chỉ khi model là Tumble (đồ vật): lắc lư đứng thẳng, không quay theo hướng đi
    private bool tumbleStarted;            // false = tumble chưa lấy hướng bắt đầu từ vỏ (LateUpdate đầu tiên sau khi dựng model)
    private Renderer[] modelRenderers = Array.Empty<Renderer>();
    private Material silhouetteMaterial;                          // null = hiện material thật
    private readonly List<Material[]> originalMaterials = new();  // song song modelRenderers, chỉ có khi đang hiện bóng đen
    private bool tappable = true;   // giữ trên Fish chứ không trên collider: model đổi (Reveal) thì collider của model mới theo đúng trạng thái
    public static int HighlightedCount { get; private set; }
    public bool IsHighlighted { get; private set; }
    /// <summary>Số cá đang viền đỏ "không đủ chỗ" (FishOutlineFeature chỉ chạy pass đỏ khi > 0).</summary>
    public static int DeniedCount { get; private set; }
    /// <summary>Đang viền đỏ: đàn FishSchoolBubble dưới tay không đủ chỗ bể + ô chờ (InputManager). Đè lên viền highlight.</summary>
    public bool IsDenied { get; private set; }
    private static readonly List<Fish> IconPreviews = new();
    public static int IconPreviewCount => IconPreviews.Count;
    public bool IsIconPreview { get; private set; }

    public static bool TryGetByCollider(Collider collider, out Fish fish)
    {
        if (collider != null && ByCollider.TryGetValue(collider, out fish) && fish != null) return true;
        fish = null;
        return false;
    }

    private void OnEnable()
    {
        RegisterTapCollider();
        if (IsIconPreview && !IconPreviews.Contains(this)) IconPreviews.Add(this); // bể tắt rồi bật lại: cá mẫu vẫn là icon
    }

    private void OnDisable()
    {
        UnregisterTapCollider();
        if (IsHighlighted) SetHighlighted(false); // cá bị hủy / thu hồi khi đang có viền thì trả lại đếm cho FishOutlineFeature
        if (IsDenied) SetDenied(false);
        if (IsIconPreview) IconPreviews.Remove(this);
    }

    /// <summary>Dựng cá theo entry: model theo Entry.ModelId (cá ẩn = model ẩn) rồi bơi tự do (đàn sẽ tắt swimmer khi nhận cá).</summary>
    public void Initialize(FishEntry entry)
    {
        if (entry == null)
        {
            Debug.LogError("Fish.Initialize: entry null.", this);
            return;
        }

        Entry = entry;
        SetHighlighted(false);
        Clear();
        InitializeGraphic(Entry.ModelId);
        ApplyOutlineLayers(); // model vừa dựng chưa có bit viền (stroke icon)
        InitializeSwimmer();
    }

    /// <summary>
    /// HiddenFish lộ hình thật: bỏ cờ trên Entry (model thấy cùng lúc) rồi dựng lại model theo loài. Không đụng swimmer, đàn hay
    /// vị trí nên gọi được cả khi cá đang trong bóng lẫn đang bay. Gọi thừa vô hại: RefreshGraphic so model đang hiện với
    /// Entry.ModelId, nên cả khi BoardModel đã bỏ cờ trước (TryAutoRevealAll) vẫn đổi model đúng.
    /// </summary>
    public void Reveal()
    {
        if (Entry == null) return;
        Entry.IsHidden = false;
        RefreshGraphic();
    }

    /// <summary>Dựng lại model nếu model đang hiện không khớp Entry.ModelId (sau khi cờ vật cản trên Entry đổi).</summary>
    public void RefreshGraphic()
    {
        if (Entry == null || currentModelId == Entry.ModelId) return;
        bool revealing = currentModelId == FishEntry.HiddenModelId && Entry.ModelId != FishEntry.HiddenModelId;   // cá ẩn đổi sang cá thật
        Clear();
        InitializeGraphic(Entry.ModelId);
        if (FishSwimmer != null) FishSwimmer.WithAnimator(FishProceduralAnimator);
        ApplyOutlineLayers(); // model mới chưa có bit viền, đánh dấu lại
        if (revealing) PlayRevealVfx();
    }

    /// <summary>
    /// VFX cá ẩn lộ hình thật (HiddenRevealVfx) tại tâm model, scale theo nửa chiều dài thân cá thật vừa dựng (BodySize × scale world)
    /// nhân HiddenRevealVfxScale. Parent là root của Level (transform.root) chứ không phải zoneRoot của bóng, để bóng nổ hay tắt nội
    /// dung ngay sau đó không hủy / ẩn effect đang chạy.
    /// </summary>
    private void PlayRevealVfx()
    {
        if (HiddenRevealVfx == null) return;
        Vector3 position = GraphicContainer != null ? GraphicContainer.position : transform.position;
        float halfLength = BodySize.z * 0.5f * Mathf.Abs(transform.lossyScale.z);
        HiddenBreakEffect.Play(HiddenRevealVfx, position, transform.root, Mathf.Max(0.05f, halfLength * HiddenRevealVfxScale));
    }

    /// <summary>Xoá model đang hiện. Xoá theo FishModel chứ không theo Animator vì model ẩn không có Animator.</summary>
    private void Clear()
    {
        UnregisterTapCollider();
        if (TapCollider != null) TapCollider.enabled = false;   // Destroy tới cuối frame mới xoá: tắt ngay để raycast cùng frame không trúng model cũ
        TapCollider = null;
        if (FishModel != null) Destroy(FishModel.gameObject);
        FishModel = null;
        FishProceduralAnimator = null;
        currentModelId = int.MinValue;
        headFirst = null;
        headUp = null;
        tumble = null;
        if (GraphicContainer != null) GraphicContainer.localRotation = Quaternion.identity;
        modelRenderers = Array.Empty<Renderer>();
        originalMaterials.Clear();   // renderer cũ bị hủy theo model; model mới dựng xong sẽ áp lại bóng đen nếu còn bật
    }

    private void InitializeGraphic(int modelId)
    {
        FishModelConfig config = FishModelGlobalConfig.Instance.GetFishModelConfigById(modelId);
        if (config == null || config.FishModel == null)
        {
            Debug.LogError($"Fish: không có FishModel cho ID {modelId} trong FishModelGlobalConfig.", this);
            BindTapCollider();
            return;
        }

        FishModel = Instantiate(config.FishModel, GraphicContainer);
        FishProceduralAnimator = FishModel.Animator;

        // Tâm hộp bao của model đặt trùng pivot của Fish: điểm bơi của FishSchool là tâm thân cá.
        Bounds bounds = FishModel.LocalBounds;
        FishModel.transform.localPosition = -bounds.center;
        FishModel.transform.localRotation = Quaternion.identity;
        BodySize = bounds.size;
        BaseSize = Mathf.Max(FishModel.Size.x, FishModel.Size.y, FishModel.Size.z, 0.01f);
        currentModelId = modelId;
        headFirst = FishModel.Orientation == FishOrientation.FaceCameraHeadFirst ? new HeadFirstOrienter { MaxLean = FishModel.FaceCameraLean } : null;
        headUp = FishModel.Orientation == FishOrientation.HeadUpAlongPath ? new HeadUpPathOrienter() : null;
        tumble = FishModel.Orientation == FishOrientation.Tumble
            ? new TumbleOrienter
            {
                MaxSpeed = FishModel.TumbleSpeed, ChangeRate = FishModel.TumbleChange, TiltLimit = FishModel.TumbleTilt, FacingLimit = FishModel.TumbleFacing
            }
            : null;
        tumbleStarted = false;   // vỏ vừa sinh chưa có tư thế nghỉ (FishSchool đặt trong LateUpdate): tumble bắt đầu ở LateUpdate đầu tiên
        if (headFirst != null) headFirst.Reset(GraphicContainer.position);
        else if (headUp != null) { GraphicContainer.localRotation = Quaternion.identity; headUp.Reset(GraphicContainer.position, GraphicContainer.rotation); }
        else GraphicContainer.localRotation = Quaternion.identity;
        modelRenderers = FishModel.GetComponentsInChildren<Renderer>(true);
        BindTapCollider();

        ApplySilhouette();
    }

    /// <summary>Model FaceCameraHeadFirst: container model quay mặt về camera, đỉnh đầu theo vận tốc thật (đo từ vị trí vỏ), xem HeadFirstOrienter.
    /// Model HeadUpAlongPath: đỉnh đầu theo tiếp tuyến đường bơi, đứng yên thì xoay như vỏ (rotation nghỉ do FishSchool đặt), xem HeadUpPathOrienter.
    /// Model Tumble (đồ vật): lắc lư đứng thẳng (quanh Y đung đưa, mặt trước lệch tối đa FishModel.TumbleFacing khỏi hướng camera; nghiêng X / Z
    /// tối đa FishModel.TumbleTilt), bắt đầu theo hướng ngang của vỏ rồi không theo vỏ nữa, xem TumbleOrienter. Icon mẫu đứng yên ở tư thế chuẩn.</summary>
    private void LateUpdate()
    {
        if (IsIconPreview) return;
        if (headFirst != null)
        {
            // loài theo nhịp: bù hệ số nhịp để đoạn trượt chậm giữa hai nhịp không làm đầu dựng thẳng lại (hướng đi giữ ổn định như khi tốc độ đều)
            float pulse = FishProceduralAnimator != null && FishProceduralAnimator.IsPulseDriven ? FishProceduralAnimator.MoveSpeedMultiplier : 1f;
            GraphicContainer.rotation = headFirst.Step(GraphicContainer.position, Time.deltaTime, 1f / Mathf.Max(pulse, 0.05f));
        }
        else if (headUp != null)
        {
            GraphicContainer.rotation = headUp.Step(GraphicContainer.position, transform.rotation, Time.deltaTime);   // rotation nghỉ = vỏ (GraphicContainer local identity)
        }
        else if (tumble != null)
        {
            if (!tumbleStarted)
            {
                // mỗi đồ vật một hạt giống riêng nên không con nào xoay giống con nào
                tumble.Reset(transform.rotation, UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                tumbleStarted = true;
            }

            GraphicContainer.rotation = tumble.Step(Time.deltaTime);
        }
    }

    // ---------- Bóng đen ----------

    /// <summary>
    /// Bóng đen (cá trong bóng còn vỏ băng BubbleHp): thay mọi material đục của model bằng silhouette (một màu phẳng), cá vẫn bơi và
    /// animate như thường; null = trả material thật. Material trong suốt (bọt của model cá ẩn) giữ nguyên. Model đổi (Reveal /
    /// RefreshGraphic) thì tự áp lại. Gọi thừa vô hại.
    /// </summary>
    public void SetSilhouette(Material silhouette)
    {
        if (silhouetteMaterial == silhouette) return;
        RestoreMaterials();
        silhouetteMaterial = silhouette;
        ApplySilhouette();
    }

    private void ApplySilhouette()
    {
        if (silhouetteMaterial == null || originalMaterials.Count > 0) return;
        foreach (Renderer fishRenderer in modelRenderers)
        {
            Material[] original = fishRenderer != null ? fishRenderer.sharedMaterials : Array.Empty<Material>();
            originalMaterials.Add(original);
            if (fishRenderer == null) continue;
            var swapped = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                bool opaque = original[i] != null && original[i].renderQueue < (int)UnityEngine.Rendering.RenderQueue.Transparent;
                swapped[i] = opaque ? silhouetteMaterial : original[i];
            }

            fishRenderer.sharedMaterials = swapped;
        }
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < originalMaterials.Count && i < modelRenderers.Length; i++)
        {
            if (modelRenderers[i] != null) modelRenderers[i].sharedMaterials = originalMaterials[i];
        }

        originalMaterials.Clear();
        
        BindTapCollider();
    }

    // ---------- Collider bấm ----------

    /// <summary>Bật / tắt bấm chọn con cá này (raycast của CameraManager). Gọi lúc nào cũng được, kể cả trước khi có model.</summary>
    public void SetTappable(bool value)
    {
        tappable = value;
        Collider tap = TapCollider;
        if (tap != null) tap.enabled = value;
    }

    /// <summary>
    /// Chọn collider bấm cho model hiện tại. MeshCollider của model: đổi layer theo Fish vì raycast lọc layer, mà model sinh sau
    /// SetLayerRecursively của container (Reveal) thì còn ở Default. Model không có MeshCollider: BoxCollider dự phòng theo BodySize.
    /// </summary>
    private void BindTapCollider()
    {
        UnregisterTapCollider();
        MeshCollider meshCollider = FishModel != null ? FishModel.MeshCollider : null;
        TapCollider = meshCollider != null && meshCollider.sharedMesh != null ? meshCollider : null;
        if (TapCollider != null)
        {
            TapCollider.gameObject.layer = gameObject.layer;
            TapCollider.enabled = tappable;
        }
        if (isActiveAndEnabled) RegisterTapCollider();
    }

    private void RegisterTapCollider()
    {
        Collider tap = TapCollider;
        if (tap != null) ByCollider[tap] = this;
    }

    private void UnregisterTapCollider()
    {
        Collider tap = TapCollider;
        if (tap != null && ByCollider.TryGetValue(tap, out Fish owner) && owner == this) ByCollider.Remove(tap);
    }

    private void InitializeSwimmer()
    {
        FishSwimmer.WithAnimator(FishProceduralAnimator).StartSwimming();
    }

    // ---------- Viền highlight ----------

    /// <summary>
    /// Bật / tắt viền highlight: đánh dấu rendering layer FishOutlineFeature.RenderingLayer lên mọi Renderer của model, FishOutlineFeature
    /// (trên Renderer2D) vẽ viền quanh silhouette trên màn hình. Model đổi (Reveal) thì RefreshGraphic đánh dấu lại. Gọi thừa vô hại.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (IsHighlighted != highlighted)
        {
            IsHighlighted = highlighted;
            HighlightedCount = Mathf.Max(0, HighlightedCount + (highlighted ? 1 : -1));
        }

        ApplyOutlineLayers();
    }

    /// <summary>
    /// Bật / tắt viền đỏ "không đủ chỗ" (bit FishOutlineFeature.DenyRenderingLayer): InputManager bật cho cả đàn FishSchoolBubble khi đè
    /// tay mà bể + ô chờ không chứa hết đàn, giữ thêm SchoolDenyLinger giây sau khi nhấc. Đang bật thì thay viền highlight. Gọi thừa vô hại.
    /// </summary>
    public void SetDenied(bool denied)
    {
        if (IsDenied != denied)
        {
            IsDenied = denied;
            DeniedCount = Mathf.Max(0, DeniedCount + (denied ? 1 : -1));
        }

        ApplyOutlineLayers();
    }

    /// <summary>Đánh dấu bit rendering layer cho các nhóm viền của FishOutlineFeature: viền đỏ (đè lên highlight), highlight và stroke icon.</summary>
    private void ApplyOutlineLayers()
    {
        foreach (Renderer fishRenderer in modelRenderers)
        {
            if (fishRenderer == null) continue;
            uint mask = fishRenderer.renderingLayerMask
                        & ~(FishOutlineFeature.RenderingLayer | FishOutlineFeature.IconRenderingLayer | FishOutlineFeature.DenyRenderingLayer);
            if (IsDenied) mask |= FishOutlineFeature.DenyRenderingLayer;
            else if (IsHighlighted) mask |= FishOutlineFeature.RenderingLayer;
            if (IsIconPreview) mask |= FishOutlineFeature.IconRenderingLayer;
            fishRenderer.renderingLayerMask = mask;
        }
        GraphicContainer.localScale = Vector3.one * (IsHighlighted || IsDenied ? 1.1f : 1f);
    }

    /// <summary>
    /// AABB thế giới gộp mọi renderer của các cá mẫu đang bật, nới thêm IconBoundsPadding phần kích thước vì bounds của renderer
    /// không ôm hết vây đang vẫy. FishOutlineFeature chỉ chạy các pass stroke icon trong vùng này.
    /// </summary>
    public static bool TryGetIconPreviewBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Fish fish in IconPreviews)
        {
            if (fish == null) continue;
            foreach (Renderer fishRenderer in fish.modelRenderers)
            {
                if (fishRenderer == null) continue;
                Bounds rendererBounds = fishRenderer.bounds;
                rendererBounds.Expand(rendererBounds.size * IconBoundsPadding);
                if (any) bounds.Encapsulate(rendererBounds);
                else bounds = rendererBounds;
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    /// Cá mẫu trên bể (TankView): luôn model thật của loài, không bơi, không bấm được. Có stroke đen quanh silhouette
    /// (FishOutlineFeature.IconRenderingLayer) để nổi trên nền sáng phía sau.
    /// </summary>
    public void SetAsIconPreview(int fishId, float previewScale, Transform parent)
    {
        IsIconPreview = true;
        if (isActiveAndEnabled && !IconPreviews.Contains(this)) IconPreviews.Add(this);

        Initialize(new FishEntry(fishId));
        Swimmer.StopSwimming();
        Swimmer.enabled = false;
        if (FishProceduralAnimator != null) FishProceduralAnimator.enabled = false;   // đồ vật không xương không có animator
        transform.SetParent(parent);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(FishModel.IconRotation);
        transform.localScale = Vector3.one * previewScale;
        SetTappable(false);
    }

    /// <summary>Viền + layer TutMesh cho overlay camera. Không khóa input (allowlist ở BoardManager).</summary>
    public void SetTut(bool enabled)
    {
        if (enabled)
        {
            ApplyTutMeshLayer();
            SetHighlighted(true);
            return;
        }

        int tutLayer = LayerMask.NameToLayer("Fish");
        if (tutLayer >= 0) SetMeshLayer(tutLayer);
        SetHighlighted(false);
    }

    private void ApplyTutMeshLayer()
    {
        int tutLayer = LayerMask.NameToLayer("TutMesh");
        if (tutLayer >= 0) SetMeshLayer(tutLayer);
    }

    private void SetMeshLayer(int layer)
    {
        foreach (Renderer renderer in modelRenderers)
        {
            if (renderer == null) continue;
            renderer.gameObject.layer = layer;
        }
    }

    public void HideGraphic()
    {
        FishModel.gameObject.SetActive(false);
    }
}
