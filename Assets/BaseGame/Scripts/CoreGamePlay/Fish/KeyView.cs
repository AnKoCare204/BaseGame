using UnityEngine;

/// <summary>
/// Token chìa (obstacle Lock & Key) trong bóng chìa: một Fish (KeyToken.prefab là variant của Fish.prefab) dùng model chung ID
/// FishEntry.KeyModelId (key_bubble: quả cầu nhỏ như con spawner, không animator, không collider), bơi cùng đàn như cá thường (FishSchool
/// cấp điểm, đổi chỗ, scale theo bán kính bóng) nhưng không bấm được: không có TapCollider và không nằm trong BubbleFishContainer.Fishes
/// nên BoardManager không tìm thấy. Icon chìa là model 3D Key_FishSort (con của token, ngoài Graphic): container gọi SyncIcon sau khi đàn tick để icon nằm giữa bong bóng nhỏ
/// (IconOffset lệch về phía camera để vẽ đè lên mặt bóng), không xoay theo thân mà tự xoay đều quanh trục Y thế giới (SpinSpeed, góc cộng
/// dồn trong Update); lúc bay sang ổ (Flying) tự đồng bộ trong LateUpdate. Bóng còn vỏ HP (cá chỉ hiện bóng đen) thì token cũng ra bóng đen (SetSilhouette): quả cầu nhỏ tô material bóng
/// đen thành một đĩa tối, icon chìa tắt để không lộ đó là chìa, vẫn giữ chỗ trong đàn; vỏ vỡ trả lại như thường. Khói che thì tắt theo
/// cả nội dung. Bóng chìa nổ: BoardManager rút token (BubbleFishContainer.DetachKey) cho bay sang ổ khoá, tới nơi ổ mới mở.
/// </summary>
[RequireComponent(typeof(Fish))]
public class KeyView : MonoBehaviour
{
    [field: SerializeField, Tooltip("Fish cùng GameObject (gán sẵn trên prefab)")]
    public Fish Fish { get; private set; }

    [field: SerializeField, Tooltip("Root model chìa (prefab Key_FishSort lồng bên trong; con của token, không nằm dưới Graphic nên không bị SetSilhouette đổi material, chỉ bị tắt)")]
    public Transform Icon { get; private set; }

    [field: SerializeField, Tooltip("Model chìa đặt lệch khỏi tâm bong bóng nhỏ theo world (z âm về phía camera). Phải ra hẳn trước mặt cầu (bán kính cầu 0.78 + nửa bề rộng chìa lúc xoay ngang) vì cầu là transparent vẽ đè lên mesh đặc nằm bên trong")]
    public Vector3 IconOffset { get; private set; } = new(0f, 0f, -1.25f);

    [field: SerializeField, Min(0f), Tooltip("Model chìa xoay đều quanh trục Y thế giới bấy nhiêu độ / giây (0 = đứng yên hướng camera)")]
    public float SpinSpeed { get; private set; } = 90f;

    /// <summary>Đang ẩn visual (SetHidden): bong bóng nhỏ và icon tắt, vẫn bơi trong đàn.</summary>
    public bool Hidden { get; private set; }

    /// <summary>Đã rời đàn, đang bay sang ổ khoá (DetachKey): tự đồng bộ icon mỗi LateUpdate vì không còn container gọi.</summary>
    public bool Flying { get; private set; }

    /// <summary>Đang hiện bóng đen (SetSilhouette): quả cầu nhỏ tô một màu phẳng, icon tắt, vẫn bơi trong đàn.</summary>
    public bool Silhouetted => silhouette != null;

    private Material silhouette;
    private Renderer[] modelRenderers = System.Array.Empty<Renderer>();
    private Material[][] originalMaterials = System.Array.Empty<Material[]>();
    private float spinAngle;   // góc xoay quanh Y hiện tại (độ), cộng dồn theo SpinSpeed

    /// <summary>Dựng model token, tắt bấm. Container gọi ngay sau Instantiate, trước khi đưa vào đàn.</summary>
    public void Setup()
    {
        if (Fish == null) Fish = GetComponent<Fish>();
        Fish.Initialize(new FishEntry(FishEntry.KeyModelId));
        Fish.SetTappable(false);
        SyncIcon();
    }

    /// <summary>Màu của cặp khoá / chìa (GamePlayGlobalConfig.KeyLockMaterials): gán cho mọi renderer dưới Icon; null = giữ material trên prefab. Container gọi ngay sau Setup.</summary>
    public void SetPairMaterial(Material material)
    {
        if (material == null || Icon == null) return;
        foreach (Renderer iconRenderer in Icon.GetComponentsInChildren<Renderer>(true)) iconRenderer.sharedMaterial = material;
    }

    /// <summary>Ẩn / hiện toàn bộ visual (model bong bóng nhỏ + icon) nhưng vẫn giữ chỗ và bơi trong đàn. Container gọi theo SetSilhouette. Gọi thừa vô hại.</summary>
    public void SetHidden(bool hidden)
    {
        Hidden = hidden;
        if (Fish != null && Fish.Graphic != null && Fish.Graphic.gameObject.activeSelf == hidden) Fish.Graphic.gameObject.SetActive(!hidden);
        RefreshIcon();
    }

    /// <summary>
    /// Bóng còn vỏ HP (cá chỉ hiện bóng đen): quả cầu nhỏ đổi sang material bóng đen thành một đĩa tối bơi cùng đàn, icon chìa tắt để
    /// không lộ đó là chìa; null = trả material thật và hiện lại icon. Container gọi theo SetSilhouette. Gọi thừa vô hại.
    /// </summary>
    public void SetSilhouette(Material material)
    {
        if (silhouette == material) return;
        RestoreMaterials();
        silhouette = material;
        if (material != null) ApplySilhouette(material);
        RefreshIcon();
    }

    private void ApplySilhouette(Material material)
    {
        modelRenderers = Fish != null && Fish.Graphic != null ? Fish.Graphic.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
        originalMaterials = new Material[modelRenderers.Length][];
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            originalMaterials[i] = modelRenderers[i].sharedMaterials;
            var swapped = new Material[originalMaterials[i].Length];
            for (int j = 0; j < swapped.Length; j++) swapped[j] = material;
            modelRenderers[i].sharedMaterials = swapped;
        }
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < modelRenderers.Length && i < originalMaterials.Length; i++)
        {
            if (modelRenderers[i] != null && originalMaterials[i] != null) modelRenderers[i].sharedMaterials = originalMaterials[i];
        }

        modelRenderers = System.Array.Empty<Renderer>();
        originalMaterials = System.Array.Empty<Material[]>();
    }

    /// <summary>Icon chỉ hiện khi không ẩn và không bóng đen.</summary>
    private void RefreshIcon()
    {
        bool show = !Hidden && silhouette == null;
        if (Icon != null && Icon.gameObject.activeSelf != show) Icon.gameObject.SetActive(show);
    }

    /// <summary>Container rút token khỏi đàn để bay sang ổ: trả hình thật (bỏ bóng đen, bỏ ẩn), từ giờ tự đồng bộ icon.</summary>
    public void BeginFlight()
    {
        Flying = true;
        SetSilhouette(null);
        SetHidden(false);
    }

    /// <summary>Đặt icon theo vị trí hiện tại của token (container gọi sau FishSchool.Tick): không theo thân, chỉ xoay quanh trục Y theo spinAngle.</summary>
    public void SyncIcon()
    {
        if (Icon == null) return;
        Icon.position = transform.position + IconOffset;
        Icon.rotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    private void Update()
    {
        if (SpinSpeed > 0f) spinAngle = Mathf.Repeat(spinAngle + SpinSpeed * Time.deltaTime, 360f);
    }

    private void LateUpdate()
    {
        if (Flying) SyncIcon();
    }
}
