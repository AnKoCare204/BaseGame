using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Bể (Tank.prefab): thân bể + cá mẫu của loài đang cần + chữ x/Goal + đàn cá. Rơi từ trên xuống ô cắm (TankSlotView) khi có đơn.
/// Cá vào bể đứng theo bộ điểm trong hình cầu tâm Center bán kính SwimRadius như trong bóng (FishSchool, sức chứa = Goal): mỗi con
/// được giữ trước một điểm và bay thẳng tới đó (PrepareArrival), rồi bơi tại chỗ và thi thoảng cả đàn đổi chỗ theo cung cong;
/// đủ Goal cá thì bể mang cá bay lên biến mất. Không giữ logic: BoardModel.TankEntry quyết, bể chỉ trình diễn.
/// </summary>
public class TankView : MonoBehaviour
{
    private static readonly float PreviewScale = 0.9f;
    
    [field: SerializeField, Tooltip("Thân bể")] public SpriteRenderer Body { get; private set; }
    [field: SerializeField, Tooltip("Đỉnh cung cá bay vào bể (FishFlight.FlyIntoTank): phía trên miệng bể, Z lệch về phía camera để cá vẽ trước thân bể lúc bay lên")]
    public Transform JumpPoint { get; private set; }

    [field: SerializeField, Tooltip("Miệng bể: cá rơi từ JumpPoint qua đây rồi xuống điểm đậu; Z nằm trong lòng bể để từ đây cá vẽ sau kính")]
    public Transform DropPoint { get; private set; }
    [field: SerializeField] public Transform FishIcon { get; private set; }
    [field: SerializeField, Tooltip("Chữ x/Goal")]
    public TMP_Text CountText { get; private set; }

    [field: SerializeField, Tooltip("Tâm bể (không xoay): tâm hình cầu đặt cá, cha của cá trong bể. Trống = chính bể")]
    public Transform Center { get; private set; }

    [field: SerializeField, Min(0.05f), Tooltip("Bán kính hình cầu đặt cá quanh Center (đơn vị local của Center; gizmo cầu xanh khi chọn bể)")]
    public float SwimRadius { get; private set; } = 0.6f;

    [field: SerializeField, Tooltip("Đàn cá trong bể: bộ điểm trong hình cầu tâm Center bán kính SwimRadius, sức chứa = Goal; bơi tại chỗ và thi thoảng đổi chỗ như trong bóng (1 con cũng đổi)")]
    public FishSchool School { get; private set; } = FishSchool.ForTank();

    [field: SerializeField, Tooltip("Nhóm phóng to nhẹ khi bể đang ở trên cao; trống = chính bể")] 
    public Transform Group { get; private set; }
    [field: SerializeField] public MMF_Player RaiseEffect { get; private set; }
    [field: SerializeField] public ParticleSystem Particles { get; private set; }
    [field: SerializeField] public TankStar[] Star { get; private set; }
    public int FishId { get; private set; } = -1;
    public int Goal { get; private set; }

    /// <summary>Bể đang rơi vào hoặc bay đi: chưa nhận cá.</summary>
    public bool IsMoving { get; private set; }

    public IReadOnlyList<Fish> Fishes => fishes;

    private readonly List<Fish> fishes = new();
    private MotionHandle moveHandle;
    private Fish speciesPreview;

    private static GamePlayGlobalConfig Config => GamePlayGlobalConfig.Instance;

    /// <summary>Tâm đàn cá và cha của cá trong bể: Center, trống thì chính bể.</summary>
    private Transform SwimRoot => Center != null ? Center : transform;

    private void Awake()
    {
        School.Initialize(SwimRoot);
    }

    private void LateUpdate()
    {
        School.Tick(SwimRadius, Time.deltaTime);
    }

    private void OnDestroy()
    {
        moveHandle.TryCancel();
    }

    public void Initialize(int fishId, int goal)
    {
        FishId = fishId;
        Goal = goal;
        School.Capacity = Mathf.Max(1, goal);
        ShowSpecies(fishId);
        RefreshCount();
    }

    // ---------- Rơi vào / bay đi ----------

    /// <summary>Rơi từ TankDropHeight xuống vị trí local (0,0,0) trong ô cắm, TankDropDuration giây.</summary>
    public UniTask DropInAsync(CancellationToken ct)
    {
        float duration = Config != null ? Config.TankDropDuration : 0.5f;
        float height = Config != null ? Config.TankDropHeight : 10f;
        // Ease OutCubic: bể gần như đã chạm chỗ đậu từ khoảng 60% thời gian, tiếng đặt xuống phát ở mốc đó thay vì cuối tween.
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxTankDrop, duration * 0.6f);
        return MoveAsync(Vector3.up * height, Vector3.zero, duration, false, ct);
    }

    /// <summary>Bay lên TankDropHeight rồi tự hủy (mang theo cá là con của Center).</summary>
    public async UniTask ReleaseAsync(CancellationToken ct)
    {
        float duration = Config != null ? Config.TankReleaseDuration : 0.5f;
        float height = Config != null ? Config.TankDropHeight : 10f;
        try
        {
            await MoveAsync(Vector3.zero, Vector3.up * height, duration, true, ct);
        }
        finally
        {
            if (this != null) Destroy(gameObject);
        }
    }

    private async UniTask MoveAsync(Vector3 from, Vector3 to, float duration, bool leaving, CancellationToken ct)
    {
        IsMoving = true;
        Transform group = Group != null ? Group : transform;
        transform.localPosition = from;
        moveHandle.TryCancel();
        try
        {
            if (duration <= 0f)
            {
                transform.localPosition = to;
                return;
            }

            MotionHandle handle = LMotion.Create(0f, 1f, duration)
                .WithEase(leaving ? Ease.InCubic : Ease.OutCubic)
                .Bind(u =>
                {
                    if (this == null) return;
                    transform.localPosition = Vector3.LerpUnclamped(from, to, u);
                    float high = leaving ? u : 1f - u;   // 0 = đang đậu, 1 = trên cao
                    if (group != null) group.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, high);
                })
                .AddTo(this);
            moveHandle = handle;
            await handle.ToUniTask(cancellationToken: ct);
        }
        finally
        {
            IsMoving = false;
            if (this != null && !leaving)
            {
                transform.localPosition = to;
                if (group != null) group.localScale = Vector3.one;
            }
        }
    }

    // ---------- Cá trong bể ----------

    /// <summary>Giữ trước một điểm trong đàn cho con cá sắp bay tới và trả về anchor bám theo điểm đó (cá bay thẳng tới điểm của mình).</summary>
    public Transform PrepareArrival(Fish fish)
    {
        float scale = Config != null ? Config.TankFishScale : 1f;
        Transform anchor = School.ReserveArrival(fish, scale, SwimRadius);
        return anchor != null ? anchor : SwimRoot;
    }

    /// <summary>Cá đã bay tới anchor: vào đàn tại điểm đã giữ, làm con của Center (bể bay đi thì cá bay theo).</summary>
    public void Park(Fish fish, Transform anchor)
    {
        if (fish == null) return;
        fishes.Add(fish);
        School.Arrive(fish, anchor, SwimRadius);
        RefreshCount();
    }

    /// <summary>Lấy hết cá trong bể (booster Swap): rời đàn, bên gọi tự đưa cá đi.</summary>
    public List<Fish> TakeAll()
    {
        var result = new List<Fish>(fishes);
        fishes.Clear();
        foreach (Fish fish in result) School.Remove(fish);
        RefreshCount();
        return result;
    }

    public void RefreshCount()
    {
        if (CountText != null) CountText.text = FishId >= 0 ? $"{fishes.Count}/{Goal}" : "";
        for (int i = 0; i < Star.Length; i++)
        {
            Star[i].SetActive(i < fishes.Count);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform zone = SwimRoot;
        if (zone == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(zone.position, zone.rotation, zone.lossyScale * SwimRadius);
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
        Gizmos.matrix = Matrix4x4.identity;
        if (JumpPoint != null && DropPoint != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(JumpPoint.position, 0.1f);
            Gizmos.DrawWireSphere(DropPoint.position, 0.1f);
            Gizmos.DrawLine(JumpPoint.position, DropPoint.position);
            Gizmos.DrawLine(DropPoint.position, zone.position);
        }

        School.DrawGizmos();
    }

    private void ShowSpecies(int fishId)
    {
        if (speciesPreview != null)
        {
            Destroy(speciesPreview.gameObject);
            speciesPreview = null;
        }

        if (fishId < 0) return;
        Fish prefab = PrefabGlobalConfig.Instance != null ? PrefabGlobalConfig.Instance.fishPrefab : null;
        if (prefab == null) return;
        speciesPreview = Instantiate(prefab);
#if UNITY_EDITOR
        speciesPreview.name = $"Species_{fishId}";
#endif
        speciesPreview.SetAsIconPreview(fishId, PreviewScale, FishIcon);
    }

    public void SetTut(bool isTut)
    {
        int layer = LayerMask.NameToLayer(isTut ? "TutMesh" : "Default");
        if (layer >= 0) SetMeshLayer(layer);
    }

    private void SetMeshLayer(int layer)
    {
        foreach (Renderer renderer in transform.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            renderer.gameObject.layer = layer;
        }
    }

    public void PlayEffectRaiseProgress()
    {
        if (RaiseEffect.IsPlaying)
        {
            RaiseEffect.StopFeedbacks();
            RaiseEffect.ResetFeedbacks();
        }
        RaiseEffect.PlayFeedbacks();
        Particles.Play();
    }
}
