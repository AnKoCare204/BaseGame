using TMPro;
using UnityEngine;

/// <summary>
/// Con spawner (obstacle Spawner) trong bóng: một Fish (Spawner.prefab là variant của Fish.prefab) dùng model chung ID
/// FishEntry.SpawnerModelId (spawner_bubble: quả cầu nhỏ vẽ bằng material bong bóng M_SoftBubbleCartoonSpawner, không animator, không
/// collider), bơi cùng đàn như cá thường (FishSchool cấp điểm, đổi chỗ, scale theo bán kính bóng) nhưng không bấm được: không có
/// TapCollider và không nằm trong BubbleFishContainer.Fishes nên BoardManager không tìm thấy. CountText = số cá còn trong hàng đợi
/// (BubbleEntry.SpawnerFishes); container gọi SyncText sau khi đàn tick để chữ nằm giữa bong bóng nhỏ (TextOffset lệch về phía camera để
/// vẽ đè lên mặt bóng), luôn hướng thẳng về camera không xoay theo thân. Hết cá thì container co về 0 rồi hủy (BubbleFishContainer.UpdateSpawner).
/// Bóng còn vỏ HP (BubbleHp, cá chỉ hiện bóng đen) thì spawner cũng ra bóng đen (SetSilhouette): quả cầu nhỏ tô material bóng đen thành
/// một đĩa tối bơi cùng đàn, chữ đếm tắt để không lộ đó là spawner; vỏ vỡ trả lại như thường. SetHidden tắt hẳn cả bong bóng lẫn chữ
/// nhưng vẫn giữ chỗ trong đàn (dự phòng, container hiện không dùng).
/// </summary>
[RequireComponent(typeof(Fish))]
public class SpawnerView : MonoBehaviour
{
    [field: SerializeField, Tooltip("Fish cùng GameObject (gán sẵn trên prefab)")]
    public Fish Fish { get; private set; }

    [field: SerializeField, Tooltip("Chữ đếm cá còn lại trong hàng đợi (TextMeshPro world, con của spawner)")]
    public TMP_Text CountText { get; private set; }

    [field: SerializeField, Tooltip("Chữ đặt lệch khỏi tâm bong bóng nhỏ theo world (z âm về phía camera để chữ vẽ đè lên mặt bóng; y = 0 là giữa bóng). Quả cầu bán kính 0.78 (spawner_bubble scale 1.56)")]
    public Vector3 TextOffset { get; private set; } = new(0f, 0f, -0.39f);

    /// <summary>Số cá còn trong hàng đợi đang hiện trên chữ.</summary>
    public int Count { get; private set; }

    /// <summary>Đang ẩn visual (SetHidden): bong bóng nhỏ và chữ tắt, vẫn bơi trong đàn.</summary>
    public bool Hidden { get; private set; }

    /// <summary>Đang hiện bóng đen (SetSilhouette): quả cầu nhỏ tô một màu phẳng, chữ tắt, vẫn bơi trong đàn.</summary>
    public bool Silhouetted => silhouette != null;

    private Material silhouette;
    private Renderer[] modelRenderers = System.Array.Empty<Renderer>();
    private Material[][] originalMaterials = System.Array.Empty<Material[]>();

    /// <summary>Dựng model spawner, tắt bấm, đặt số. Container gọi ngay sau Instantiate, trước khi đưa vào đàn.</summary>
    public void Setup(int count)
    {
        if (Fish == null) Fish = GetComponent<Fish>();
        Fish.Initialize(new FishEntry(FishEntry.SpawnerModelId));
        Fish.SetTappable(false);
        SetCount(count);
        SyncText();
    }

    /// <summary>
    /// Ẩn / hiện toàn bộ visual (model bong bóng nhỏ + chữ) nhưng vẫn giữ chỗ và bơi trong đàn: bóng còn vỏ băng (BubbleHp) thì cá chỉ
    /// hiện bóng đen còn spawner ẩn hẳn, vỏ vỡ thì hiện lại. Container gọi theo SetSilhouette. Gọi thừa vô hại.
    /// </summary>
    public void SetHidden(bool hidden)
    {
        Hidden = hidden;
        if (Fish != null && Fish.Graphic != null && Fish.Graphic.gameObject.activeSelf == hidden) Fish.Graphic.gameObject.SetActive(!hidden);
        RefreshText();
    }

    /// <summary>
    /// Bóng còn vỏ HP (cá chỉ hiện bóng đen): quả cầu nhỏ đổi sang material bóng đen thành một đĩa tối bơi cùng đàn, chữ đếm tắt để
    /// không lộ đó là spawner; null = trả material thật và hiện lại chữ. Container gọi theo SetSilhouette. Gọi thừa vô hại.
    /// </summary>
    public void SetSilhouette(Material material)
    {
        if (silhouette == material) return;
        RestoreMaterials();
        silhouette = material;
        if (material != null) ApplySilhouette(material);
        RefreshText();
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

    /// <summary>Chữ đếm chỉ hiện khi không ẩn và không bóng đen.</summary>
    private void RefreshText()
    {
        bool show = !Hidden && silhouette == null;
        if (CountText != null && CountText.gameObject.activeSelf != show) CountText.gameObject.SetActive(show);
    }

    public void SetCount(int count)
    {
        Count = Mathf.Max(0, count);
        if (CountText != null) CountText.text = Count.ToString();
    }

    /// <summary>Đặt chữ theo vị trí hiện tại của con spawner (gọi sau FishSchool.Tick), luôn hướng thẳng về camera.</summary>
    public void SyncText()
    {
        if (CountText == null) return;
        Transform t = CountText.transform;
        t.position = transform.position + TextOffset;
        t.rotation = Quaternion.identity;
    }
}