using System.Collections.Generic;
using Core.Manager;
using LitMotion;
using UnityEngine;

/// <summary>
/// Nuôi cá bên trong một SoftBubble: spawn Fish theo FishEntry của BubbleEntry (giữ kích thước prefab, hoặc scale theo bóng nếu bật ScaleToRadius
/// trong School). Cá là con của bóng nên đi theo bóng. Cách đặt chỗ, bơi tại chỗ và đổi chỗ nằm trong FishSchool (dùng chung với
/// bể), bán kính = Bubble.Radius; container chỉ spawn, giữ thứ tự cá khớp BubbleEntry.Fishes và bật collider bấm.
/// Mỗi con bấm chọn qua Fish.TapCollider (MeshCollider của model, bật bằng Fish.SetTappable khi AddTapCollider) để raycast;
/// con gần camera hơn thắng.
/// Spawner (BubbleEntry.SpawnerFishes): con spawner (SpawnerPrefab) vào đàn ở cuối, chiếm một điểm và bơi như cá nhưng không nằm trong
/// Fishes nên không bấm được. Mỗi con cá rời bóng, BoardManager gọi SpawnFishFromSpawner: cá mới sinh tại chỗ con spawner, bay tới đúng
/// điểm vừa trống và lớn từ 0 (FishSchool.AddFlyIn), collider bật khi tới. Hàng đợi hết thì UpdateSpawner co con spawner về 0, hủy và
/// ép đàn dựng lại bộ điểm.
/// </summary>
[RequireComponent(typeof(SoftBubble))]
public class BubbleFishContainer : MonoBehaviour
{
    [field: SerializeField, Tooltip("Prefab Fish (có Fish + FishSwimmer + GraphicContainer). Model lấy theo FishEntry.ModelId từ FishModelGlobalConfig")]
    public Fish FishPrefab { get; private set; }

    [field: SerializeField, Tooltip("Prefab con spawner (variant Fish + SpawnerView + chữ đếm). Bóng có spawner thì sinh một con vào đàn, không bấm được")]
    public SpawnerView SpawnerPrefab { get; private set; }

    [field: SerializeField, Tooltip("Prefab token chìa (variant Fish + KeyView + icon chìa). Bóng chìa (Lock & Key) thì sinh một token vào đàn, không bấm được; bóng nổ thì token bay sang ổ khoá")]
    public KeyView KeyPrefab { get; private set; }

    [field: SerializeField, Tooltip("Bật collider bấm của cá (Fish.SetTappable) để raycast bấm chọn")]
    public bool AddTapCollider { get; private set; } = true;

    [field: SerializeField, Tooltip("Đàn cá: bộ điểm trong khối cầu, bơi tại chỗ, đổi chỗ (bán kính = bóng)")]
    public FishSchool School { get; private set; } = new();

    [field: SerializeField, Tooltip("SoftBubble cùng GameObject (gán sẵn trên prefab)")]
    public SoftBubble Bubble { get; private set; }

    public IReadOnlyList<Fish> Fishes => fishes;

    /// <summary>Con spawner đang bơi cùng đàn (null = bóng không có spawner hoặc đã hết).</summary>
    public SpawnerView Spawner { get; private set; }

    /// <summary>Token chìa đang bơi cùng đàn (null = bóng không phải bóng chìa, hoặc token đã rút đi bay sang ổ).</summary>
    public KeyView Key { get; private set; }

    private readonly List<Fish> fishes = new();
    private readonly List<Fish> arriving = new();   // cá spawner / lớp mới đang bay vào: collider bật khi tới điểm
    private Transform zoneRoot;
    private MotionHandle spawnerHideHandle;
    private Material silhouette;   // khác null = mọi cá trong bóng (cả con spawner và cá vào sau) chỉ hiện bóng đen

    private float Radius => Bubble != null ? Bubble.Radius : 1f;

    private void Awake()
    {
        if (Bubble == null) Debug.LogError("BubbleFishContainer: chưa gán Bubble (SoftBubble cùng GameObject) trên prefab.", this);
        EnsureZoneRoot();
    }

    private void LateUpdate()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.IsSimulationFrozen) return;
        School.Tick(Radius, Time.deltaTime);
        if (Spawner != null) Spawner.SyncText();
        if (Key != null) Key.SyncIcon();
        for (int i = arriving.Count - 1; i >= 0; i--)
        {
            Fish fish = arriving[i];
            if (fish != null && School.IsFlyingIn(fish)) continue;
            arriving.RemoveAt(i);
            if (fish != null && AddTapCollider && fishes.Contains(fish)) fish.SetTappable(true);
        }
    }

    private void OnDestroy()
    {
        spawnerHideHandle.TryCancel();
    }

    /// <summary>Xóa đàn cá cũ (giữ con spawner) rồi spawn theo BubbleEntry.Fishes (cùng thứ tự, cùng tham chiếu FishEntry). Null hoặc rỗng thì bóng không có cá.</summary>
    public void Spawn(IReadOnlyList<FishEntry> entries)
    {
        ClearFish();
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null) continue;
            SpawnFish(entries[i]);
        }
    }

    /// <summary>Thêm một con cá vào cuối đàn theo entry (Fish giữ chung tham chiếu này với BoardModel). Con spawner (nếu có) vẫn đứng sau cùng trong đàn.</summary>
    public Fish SpawnFish(FishEntry entry)
    {
        Fish fish = CreateFish(entry);
        if (fish == null) return null;
        RegisterFish(fish, fishes.Count, false);
        return fish;
    }

    /// <summary>
    /// Spawner vừa chèn một con vào model ở index (BoardModel.RemoveFishAt): sinh cá tại chỗ con spawner, bay tới đúng điểm pointIndex vừa
    /// trống (FishSchool.Remove trả về) và lớn từ 0 trong duration giây. Vào Fishes ngay để chỉ số khớp model; collider bật khi tới.
    /// </summary>
    public Fish SpawnFishFromSpawner(FishEntry entry, int index, int pointIndex, float duration)
    {
        Fish fish = CreateFish(entry);
        if (fish == null) return null;
        Vector3 start = Spawner != null ? Spawner.transform.position : zoneRoot.position;
        fish.transform.position = start;
        if (AddTapCollider) fish.SetTappable(false);

        index = Mathf.Clamp(index, 0, fishes.Count);
        fishes.Insert(index, fish);
        School.AddFlyIn(fish, index, pointIndex, start, duration, Radius);
        arriving.Add(fish);
        return fish;
    }

    /// <summary>
    /// MultilayerBubble vừa đổi lớp trong model (BoardModel.RemoveFishAt, newLayer): sinh cả lớp mới tại tâm bóng, từng con bay ra điểm
    /// của mình và lớn từ 0 (con sau chậm hơn con trước stagger giây). Vào Fishes ngay theo đúng thứ tự entries để chỉ số khớp model;
    /// collider bật khi tới nơi. Bóng giữ nguyên cỡ, bộ điểm dựng lại theo số cá của lớp mới.
    /// </summary>
    public void SpawnLayer(IReadOnlyList<FishEntry> entries, float duration, float stagger)
    {
        if (entries == null) return;
        EnsureZoneRoot();
        Vector3 start = zoneRoot.position;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null) continue;
            Fish fish = CreateFish(entries[i]);
            if (fish == null) continue;
            fish.transform.position = start;
            if (AddTapCollider && fish.TapCollider != null)
            {
                fish.TapCollider.enabled = false;
            }

            int index = fishes.Count;
            fishes.Add(fish);
            School.AddFlyIn(fish, index, -1, start, duration + stagger * i, Radius);   // −1: bộ điểm đánh lại theo chỉ số như lúc spawn bóng
            arriving.Add(fish);
        }
    }

    /// <summary>
    /// Bóng còn vỏ HP (BubbleHp): mọi cá trong bóng, kể cả cá vào sau (spawner sinh, lớp mới, Shuffle tới), chỉ hiện bóng đen bằng
    /// material này; con spawner và token chìa cũng ra bóng đen (đĩa tối, tắt số / icon) nhưng vẫn giữ chỗ trong đàn; null = trả material thật.
    /// Cá rời bóng (Detach) luôn được trả material thật để bay đi đúng hình.
    /// </summary>
    public void SetSilhouette(Material material)
    {
        silhouette = material;
        foreach (Fish fish in fishes)
        {
            if (fish != null) fish.SetSilhouette(material);
        }

        if (Spawner != null) Spawner.SetSilhouette(material);
        if (Key != null) Key.SetSilhouette(material);
    }

    /// <summary>Dựng con spawner với count cá trong hàng đợi và đưa vào cuối đàn (chiếm một điểm, bơi và đổi chỗ như cá, không bấm được).</summary>
    public void ShowSpawner(int count)
    {
        if (count <= 0) return;
        if (SpawnerPrefab == null)
        {
            Debug.LogError("BubbleFishContainer: chưa gán SpawnerPrefab.", this);
            return;
        }

        EnsureZoneRoot();
        spawnerHideHandle.TryCancel();
        if (Spawner != null)
        {
            School.Remove(Spawner.Fish);
            Destroy(Spawner.gameObject);
        }

        Spawner = Instantiate(SpawnerPrefab, zoneRoot);
        Spawner.name = "Spawner";
        Spawner.transform.localPosition = Vector3.zero;
        Spawner.transform.localRotation = Quaternion.identity;
        Spawner.transform.localScale = Vector3.one;
        Spawner.Setup(count);
        Spawner.SetSilhouette(silhouette);   // bóng đang còn vỏ HP thì spawner ra bóng đen ngay từ đầu
        SetLayerRecursively(Spawner.gameObject, LayerMask.NameToLayer(GamePlayGlobalConfig.Instance.FishLayerName));
        Spawner.Fish.SetTappable(false);
        School.Add(Spawner.Fish, School.Count, false, Radius);
    }

    /// <summary>
    /// Spawner vừa sinh một con: đổi số còn lại. Về 0 thì con spawner co về 0 trong hideDuration giây, rời đàn, hủy, và đàn dựng lại
    /// bộ điểm ngay theo số cá mới (FishSchool.RequestRelayout).
    /// </summary>
    public void UpdateSpawner(int remaining, float hideDuration)
    {
        if (Spawner == null) return;
        Spawner.SetCount(remaining);
        if (remaining > 0) return;

        SpawnerView view = Spawner;
        Spawner = null;
        Vector3 from = view.transform.localScale;
        spawnerHideHandle.TryCancel();
        spawnerHideHandle = LMotion.Create(1f, 0f, Mathf.Max(0.01f, hideDuration))
            .WithEase(Ease.InQuad)
            .WithOnComplete(() =>
            {
                if (this == null) return;
                if (view != null)
                {
                    School.Remove(view.Fish);
                    Destroy(view.gameObject);
                }

                School.RequestRelayout();
            })
            .Bind(u =>
            {
                if (view != null) view.transform.localScale = from * u;
            })
            .AddTo(this);
    }

    /// <summary>
    /// Bóng chìa (Lock & Key): dựng token chìa vào đàn (chiếm một điểm, bơi và đổi chỗ như cá, không bấm được, không nằm trong Fishes).
    /// Có spawner thì đứng ngay trước nó để spawner vẫn cuối đàn. Bóng đang còn vỏ HP thì token ra bóng đen ngay từ đầu (SetSilhouette).
    /// keyMaterial = màu của cặp khoá / chìa cho model chìa (KeyView.SetPairMaterial); null = giữ material trên prefab.
    /// </summary>
    public void ShowKey(Material keyMaterial = null)
    {
        if (KeyPrefab == null)
        {
            Debug.LogError("BubbleFishContainer: chưa gán KeyPrefab.", this);
            return;
        }

        EnsureZoneRoot();
        if (Key != null)
        {
            School.Remove(Key.Fish);
            Destroy(Key.gameObject);
        }

        Key = Instantiate(KeyPrefab, zoneRoot);
        Key.name = "Key";
        Key.transform.localPosition = Vector3.zero;
        Key.transform.localRotation = Quaternion.identity;
        Key.transform.localScale = Vector3.one;
        Key.Setup();
        Key.SetPairMaterial(keyMaterial);
        Key.SetSilhouette(silhouette);   // bóng đang còn vỏ HP thì token ra bóng đen ngay từ đầu
        SetLayerRecursively(Key.gameObject, LayerMask.NameToLayer(GamePlayGlobalConfig.Instance.FishLayerName));
        Key.Fish.SetTappable(false);
        School.Add(Key.Fish, Spawner != null ? Mathf.Max(0, School.Count - 1) : School.Count, false, Radius);
    }

    /// <summary>
    /// Bóng chìa vừa hết cá: rút token chìa khỏi đàn (giữ GameObject, hiện lại nếu đang ẩn) cho BoardManager bay sang ổ khoá. Null nếu
    /// bóng không có token.
    /// </summary>
    public KeyView DetachKey()
    {
        KeyView key = Key;
        if (key == null) return null;
        Key = null;
        School.Remove(key.Fish);
        key.BeginFlight();
        return key;
    }

    /// <summary>Vị trí (index) của cá trong bóng, khớp thứ tự BubbleEntry.Fishes. −1 nếu không thuộc bóng này (con spawner cũng −1).</summary>
    public int IndexOf(Fish fish) => fishes.IndexOf(fish);

    /// <summary>
    /// Cá rời bóng (bay tới bể / ô chờ): bỏ khỏi đàn, giữ nguyên GameObject cho bên gọi bay đi. Trả về index cũ.
    /// Các con còn lại giữ nguyên điểm (FishSchool.Remove), không bị kéo giật.
    /// </summary>
    public int Detach(Fish fish) => Detach(fish, out _);

    /// <summary>Như trên, kèm điểm vừa để trống trong đàn (−1 nếu không có) để cá spawner sinh ra vào đúng chỗ đó.</summary>
    public int Detach(Fish fish, out int pointIndex)
    {
        pointIndex = -1;
        int index = fishes.IndexOf(fish);
        if (index < 0) return -1;
        fishes.RemoveAt(index);
        arriving.Remove(fish);
        School.Remove(fish, out pointIndex);
        fish.SetSilhouette(null);   // rời bóng là hiện hình thật (AutoFill lấy cá trong bóng còn vỏ băng)
        return index;
    }

    /// <summary>
    /// Shuffle: cá sắp bay đi nhưng chưa tới lượt cất cánh. Đàn không tính nó vào số điểm và nhường điểm của nó cho cá sắp tới
    /// (FishSchool.MarkLeaving); cất cánh thì Detach như thường.
    /// </summary>
    public void MarkLeaving(Fish fish) => School.MarkLeaving(fish);

    /// <summary>Cá quay về bóng ở vị trí index: đưa vào đàn, giữ vị trí world hiện tại rồi trôi về điểm của mình.</summary>
    public void Attach(Fish fish, int index)
    {
        if (fish == null || fishes.Contains(fish)) return;
        EnsureZoneRoot();
        fish.transform.SetParent(zoneRoot, true);
        fish.transform.localScale = Vector3.one;
        fish.SetSilhouette(silhouette);
        SetLayerRecursively(fish.gameObject, LayerMask.NameToLayer(GamePlayGlobalConfig.Instance.FishLayerName));
        RegisterFish(fish, Mathf.Clamp(index, 0, fishes.Count), true);
    }

    /// <summary>Shuffle: giữ trước một điểm trong đàn cho cá sắp bay tới và trả anchor bám theo điểm đó (FishSchool.ReserveArrival).</summary>
    public Transform ReserveArrival(Fish fish)
    {
        EnsureZoneRoot();
        return School.ReserveArrival(fish, 1f, Radius);
    }

    /// <summary>
    /// Shuffle: cá đã bay tới anchor đã giữ, vào đàn tại điểm đó và thêm vào cuối Fishes. Cả đợt tới xong thì gọi ReorderToMatch để
    /// thứ tự khớp BubbleEntry.Fishes của model.
    /// </summary>
    public void Arrive(Fish fish, Transform anchor)
    {
        if (fish == null || fishes.Contains(fish)) return;
        EnsureZoneRoot();
        fish.transform.localScale = Vector3.one;
        SetLayerRecursively(fish.gameObject, LayerMask.NameToLayer(GamePlayGlobalConfig.Instance.FishLayerName));
        if (AddTapCollider) fish.SetTappable(true);

        fish.SetSilhouette(silhouette);
        fishes.Add(fish);
        School.Arrive(fish, anchor, Radius);
    }

    /// <summary>Sắp lại Fishes theo thứ tự entries của model (cá Shuffle tới không theo thứ tự). Cá không có trong entries xếp cuối.</summary>
    public void ReorderToMatch(IReadOnlyList<FishEntry> entries)
    {
        int IndexOfEntry(Fish fish)
        {
            if (fish == null || fish.Entry == null) return int.MaxValue;
            for (int i = 0; i < entries.Count; i++) if (entries[i] == fish.Entry) return i;
            return int.MaxValue;
        }

        fishes.Sort((a, b) => IndexOfEntry(a).CompareTo(IndexOfEntry(b)));
    }

    private Fish CreateFish(FishEntry entry)
    {
        if (FishPrefab == null)
        {
            Debug.LogError("BubbleFishContainer: chưa gán FishPrefab.", this);
            return null;
        }

        if (entry == null)
        {
            Debug.LogError("BubbleFishContainer: entry null.", this);
            return null;
        }

        EnsureZoneRoot();
        Fish fish = Instantiate(FishPrefab, zoneRoot);
        fish.name = $"Fish_{entry.FishId}";
        fish.transform.localPosition = Vector3.zero;
        fish.transform.localRotation = Quaternion.identity;
        fish.Initialize(entry);
        fish.SetSilhouette(silhouette);
        SetLayerRecursively(fish.gameObject, LayerMask.NameToLayer(GamePlayGlobalConfig.Instance.FishLayerName));
        return fish;
    }

    private void RegisterFish(Fish fish, int index, bool keepWorldPosition)
    {
        if (AddTapCollider) fish.SetTappable(true);

        fishes.Insert(index, fish);
        School.Add(fish, index, keepWorldPosition, Radius);   // index <= số cá nên luôn chèn trước con spawner (đứng cuối đàn)
    }

    /// <summary>Ẩn / hiện toàn bộ đàn cá và con spawner (gốc "Fishes"): bóng băng che nội dung. Đàn vẫn được tick, collider cá tắt theo nên không bấm được.</summary>
    public void SetVisible(bool visible)
    {
        EnsureZoneRoot();
        if (zoneRoot.gameObject.activeSelf != visible) zoneRoot.gameObject.SetActive(visible);
    }

    /// <summary>Xóa đàn cá, giữ con spawner (Shuffle dựng lại đàn theo model).</summary>
    public void ClearFish()
    {
        foreach (Fish fish in fishes)
        {
            if (fish == null) continue;
            School.Remove(fish);
            Destroy(fish.gameObject);
        }

        fishes.Clear();
        arriving.Clear();
    }

    /// <summary>Xóa tất cả: đàn cá, con spawner, token chìa và trạng thái đàn.</summary>
    public void Clear()
    {
        ClearFish();
        spawnerHideHandle.TryCancel();
        if (Spawner != null)
        {
            Destroy(Spawner.gameObject);
            Spawner = null;
        }

        if (Key != null)
        {
            Destroy(Key.gameObject);
            Key = null;
        }

        School.Clear();
    }

    /// <summary>Bán kính bóng nhỏ nhất để mọi bộ điểm của đàn hiện tại đạt (xem FishSchool.RequiredRadius).</summary>
    public float RequiredRadius() => School.RequiredRadius(Bubble != null ? Bubble.Radius : 0f);

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>Tâm đàn: con của bóng, luôn ở tâm bóng, không xoay.</summary>
    private void EnsureZoneRoot()
    {
        if (zoneRoot != null) return;
        var go = new GameObject("Fishes");
        zoneRoot = go.transform;
        zoneRoot.SetParent(transform, false);
        zoneRoot.localPosition = Vector3.zero;
        zoneRoot.localRotation = Quaternion.identity;
        zoneRoot.localScale = Vector3.one;
        School.Initialize(zoneRoot);
    }

    private void OnDrawGizmosSelected()
    {
        School.DrawGizmos();
    }
}