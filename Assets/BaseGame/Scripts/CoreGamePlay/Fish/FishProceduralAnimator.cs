using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// Lớp cha của mọi animator procedural cho sinh vật (cá, và sau này cả loài không phải cá): skeleton import từ FBX,
/// không có clip animation, mỗi frame bone được xoay so với bind pose.
/// Phần tính pose chạy trong Burst job của FishAnimationSystem (FishPoseJob + FishApplyJob) cho mọi con cùng lúc; lớp này chỉ:
///  - Speed / TimeOffset; gom SkinnedMeshRenderer + nới bounds để phần vẫy không bị cull;
///  - Build: class con tìm + RegisterBone, lớp cha sắp cha trước con, tính bone cha đã đăng ký gần nhất và chuỗi xoay tĩnh
///    của các node trung gian, rồi bake mọi tham số Inspector thành FishBoneData / FishParams (OnValidate bake lại khi chỉnh lúc Play);
///  - đăng ký với FishAnimationSystem ở OnEnable, rút ở OnDisable (ResetPose).
/// Class con cài: OnBuild (tìm + RegisterBone), BakeBone (bone -> FishBoneData), OnBakeParams (tham số riêng -> FishParams).
/// Quy ước: local space của transform này là root space của model. Trục xoay lấy theo world rồi đổi sang parent space trong job
/// nên không phụ thuộc quy ước trục local của từng rig; các node không đăng ký giữa hai bone được coi là tĩnh.
/// Class con hiện có: FishdomProceduralAnimator (bộ cá Fishdom, nhận diện bone theo từ khoá), FishRigProceduralAnimator (rig Fish_Rig).
public abstract class FishProceduralAnimator : MonoBehaviour
{
    /// Bone đã đăng ký: transform + bind pose. Class con kế thừa để thêm dữ liệu riêng (loại, vị trí trên thân, đốt...).
    protected class BoneRef
    {
        public Transform t;
        public Quaternion bindLocal;
        public Vector3 bindScale;   // localScale lúc bind (kênh scale của job nhân vào đây; ResetPose trả về)
    }

    [Header("Uốn thân theo đường bơi (đo từ chuyển động của transform)")]
    [SerializeField, PropertyOrder(90)] private float turnBend = 7f;          // độ mỗi đốt khi rẽ 90°/s
    [SerializeField, PropertyOrder(90)] private float pitchBend = 5f;         // độ mỗi đốt khi ngóc / chúi 90°/s
    [SerializeField, PropertyOrder(90)] private float bendDelay = 0.3f;       // giây trễ tại chóp đuôi so với đầu (uốn lan từ đầu xuống)
    [SerializeField, PropertyOrder(90)] private float bendSmoothing = 10f;    // lọc rung tốc độ góc (càng lớn càng nhạy)
    [SerializeField, PropertyOrder(90)] private float maxBendPerBone = 14f;

    [Header("Chung")]
    [SerializeField, PropertyOrder(100)] private float speed = 1f;            // 1 = bơi bình thường; FishSwimmer set theo vận tốc thực
    [SerializeField, PropertyOrder(100)] private bool randomizeTimeOffset = true;
    [SerializeField, PropertyOrder(100)] private float timeOffset;
    [SerializeField, PropertyOrder(100)] private float boundsPadding = 0.3f;  // nới bounds SkinnedMeshRenderer để đuôi vẫy không bị cull
    [SerializeField, PropertyOrder(100)] private float idleSpeed = 0.6f;      // Speed ≤ đây = idle (FishSchool AnimSpeedIdle 0.6 trong bóng): bone có bộ idle riêng dùng bộ đó
    [SerializeField, PropertyOrder(100)] private float swimSpeed = 1f;        // Speed ≥ đây = bơi hẳn; ở giữa lerp idle -> bơi
    [SerializeField, PropertyOrder(100)] private float blendSmoothing = 4f;   // 1/s: blend idle -> bơi đuổi theo Speed mượt (~0.25 s), tránh giật khi Speed nhấp nhô
    [SerializeField, PropertyOrder(100), Tooltip("SkinnedMeshRenderer của model, gán sẵn trên prefab (nút Collect Skinned Meshes). Bone có skin lấy từ đây")]
    private SkinnedMeshRenderer[] skinnedMeshes = System.Array.Empty<SkinnedMeshRenderer>();

    public float Speed { get => speed; set => speed = Mathf.Max(0f, value); }
    /// Pha thân hiện tại (rad) sau frame job gần nhất / Evaluate; class con dùng để suy tốc độ theo nhịp (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed).
    public float CurrentBodyPhase { get; set; }
    /// Blend idle (0) → bơi (1) sau frame job gần nhất / Evaluate (lúc chạy thật đã làm mượt theo BlendSmoothing); loài theo nhịp dùng để tắt nhịp khi đứng yên.
    public float CurrentIdleBlend { get; set; }
    /// true = loài di chuyển theo nhịp (pha chạy đều, MoveSpeedMultiplier khác 1): hệ di chuyển đặt tốc độ = gốc × MoveSpeedMultiplier và báo Speed gốc.
    public virtual bool IsPulseDriven => false;
    /// Hệ số nhân tốc độ di chuyển theo nhịp animation (1 = như cũ).
    public virtual float MoveSpeedMultiplier => 1f;
    public float TimeOffset { get => timeOffset; set => timeOffset = value; }
    public bool IsBuilt => built;

    /// Bone đã đăng ký, cha trước con (sau OnBuild). Class con ép kiểu về lớp bone riêng của mình.
    protected List<BoneRef> Bones => bones;
    protected SkinnedMeshRenderer[] SkinnedMeshes => skinnedMeshes;

    // ---------- Dùng bởi FishAnimationSystem ----------

    /// Vị trí trong layout của system, -1 khi chưa đăng ký.
    public int SystemIndex { get; set; } = -1;
    /// Bone / tham số vừa bake lại, system cần copy vào mảng job.
    public bool BakeDirty { get; set; }
    /// Vừa Build lại: system đặt lại trạng thái mô phỏng (pha theo TimeOffset, xoá lịch sử uốn).
    public bool StateResetRequested { get; set; }
    public bool IgnoreSimulationFrozen => ignoreSimulationFrozen;
    public Transform CachedTransform => cachedTransform != null ? cachedTransform : cachedTransform = transform;
    public int BoneCount => bones.Count;
    public Transform BoneTransform(int i) => bones[i].t;
    /// Bone đã bake, Parent là index trong Bones của cá này (-1 = root của animator).
    public FishBoneData[] BakedBones => bakedBones;
    public FishParams BakedParams => bakedParams;

    private readonly List<BoneRef> bones = new();
    private FishBoneData[] bakedBones = System.Array.Empty<FishBoneData>();
    private int[] bakedParent = System.Array.Empty<int>();
    private quaternion[] bakedChain = System.Array.Empty<quaternion>();
    private FishParams bakedParams;
    private Bounds[] originalBounds;     // localBounds gốc của skinnedMeshes, để Rebuild không nới cộng dồn
    private bool built;
    private bool ignoreSimulationFrozen;
    private Transform cachedTransform;

    private void Awake() => Build();

    private void OnEnable()
    {
        if (!built) Build();
        FishAnimationSystem system = FishAnimationSystem.GetOrCreate();
        if (system != null) system.Register(this);
    }

    private void OnDisable()
    {
        if (FishAnimationSystem.Instance != null) FishAnimationSystem.Instance.Unregister(this);
        ResetPose();
    }

#if UNITY_EDITOR
    /// Chỉnh tham số trong Inspector lúc Play: bake lại bone / tham số, system copy vào job ở frame sau.
    private void OnValidate()
    {
        if (!built || bones.Count == 0 || !Application.isPlaying) return;
        Rebake();
    }
#endif

    // ---------- Class con cài ----------

    /// Tìm bone của rig rồi RegisterBone từng cái. skinnedBones = mọi bone có skin weight trong skinnedMeshes (rỗng nếu chưa gán).
    protected abstract void OnBuild(HashSet<Transform> skinnedBones);
    /// Bake một bone đã đăng ký thành dữ liệu job (Kind, Along, Amp, TwistAmp, PhaseLag, BendScale, PitchBend, Side, FoldScale, Axis, FinAxis).
    /// Parent, StaticChain, BindLocal do lớp cha điền sau.
    protected abstract FishBoneData BakeBone(BoneRef bone);
    /// Điền tham số riêng của rig (tần số, WaveK, FinFold, TailSign, các trục root space). Phần uốn theo đường bơi lớp cha điền sẵn.
    protected abstract void OnBakeParams(ref FishParams p);

    // ---------- Vòng đời ----------

#if UNITY_EDITOR
    /// Công cụ editor: gom SkinnedMeshRenderer trong model vào skinnedMeshes (chạy trên prefab, không chạy runtime).
    [ContextMenu("Collect Skinned Meshes")]
    [Button("Collect Skinned Meshes"), PropertyOrder(101)]
    public void CollectSkinnedMeshes()
    {
        skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalBounds = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"{GetType().Name} '{name}': đã gán {skinnedMeshes.Length} SkinnedMeshRenderer.", this);
    }
#endif

    [ContextMenu("Rebuild")]
    public void Build()
    {
        ResetPose();
        bones.Clear();
        if (randomizeTimeOffset && Application.isPlaying) timeOffset = UnityEngine.Random.Range(0f, 100f);

        // chỉ lấy bone thật sự có skin weight (SkinnedMeshRenderer gán sẵn trên prefab)
        var skinnedBones = new HashSet<Transform>();
        if (skinnedMeshes == null || skinnedMeshes.Length == 0)
        {
            Debug.LogWarning($"{GetType().Name} '{name}' chưa gán skinnedMeshes (nút Collect Skinned Meshes trên prefab) nên không animate.", this);
        }
        else
        {
            if (originalBounds == null || originalBounds.Length != skinnedMeshes.Length)
            {
                originalBounds = new Bounds[skinnedMeshes.Length];
                for (int i = 0; i < skinnedMeshes.Length; i++)
                    if (skinnedMeshes[i] != null) originalBounds[i] = skinnedMeshes[i].localBounds;
            }
            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                var smr = skinnedMeshes[i];
                if (smr == null) continue;
                var lb = originalBounds[i]; lb.Expand(lb.size * boundsPadding); smr.localBounds = lb;
                foreach (var b in smr.bones) if (b != null) skinnedBones.Add(b);
            }
        }
        built = true;
        OnBuild(skinnedBones);
        bones.Sort((x, y) => Depth(x.t).CompareTo(Depth(y.t))); // cha trước con
        BuildParentChain();
        Rebake();
        StateResetRequested = true;
        if (FishAnimationSystem.Instance != null && SystemIndex >= 0) FishAnimationSystem.Instance.MarkLayoutDirty();
    }

    public void ResetPose()
    {
        foreach (var b in bones)
        {
            if (b.t == null) continue;
            b.t.localRotation = b.bindLocal;
            b.t.localScale = b.bindScale;
        }
    }

    /// Pose tĩnh tại thời điểm time (giây) với Speed cho trước (1 = bơi, ~0.6 = idle trong bóng), không uốn theo đường bơi.
    /// Dùng để preview trong editor; chạy đồng bộ cùng job với runtime. Đang đăng ký thì pha của system cũng được đặt theo để Play tiếp từ pose này.
    public void Evaluate(float time, float speed = 1f)
    {
        if (!built) Build();
        int n = bones.Count;
        if (n == 0) return;
        float t = time + timeOffset;
        FishSimState st = PhaseState(t);

        var inputs = new NativeArray<FishFrameInput>(1, Allocator.TempJob);
        var prms = new NativeArray<FishParams>(1, Allocator.TempJob);
        var ranges = new NativeArray<int2>(1, Allocator.TempJob);
        var states = new NativeArray<FishSimState>(1, Allocator.TempJob);
        var hist = new NativeArray<float4>(FishPoseJob.Hist, Allocator.TempJob);
        var boneData = new NativeArray<FishBoneData>(bakedBones, Allocator.TempJob);
        var local = new NativeArray<quaternion>(n, Allocator.TempJob);
        var world = new NativeArray<quaternion>(n, Allocator.TempJob);
        var localScale = new NativeArray<float3>(n, Allocator.TempJob);
        try
        {
            inputs[0] = new FishFrameInput { RootRotation = transform.rotation, Speed = speed, Dt = 0f, Active = 1, PoseOnly = 1 };
            prms[0] = bakedParams;
            ranges[0] = new int2(0, n);
            states[0] = st;
            new FishPoseJob
            {
                Inputs = inputs, Params = prms, Ranges = ranges, Bones = boneData,
                States = states, History = hist, LocalRot = local, WorldRot = world, LocalScale = localScale
            }.Run(1);
            CurrentBodyPhase = st.BodyPhase;
            CurrentIdleBlend = Mathf.Clamp01((speed - bakedParams.IdleSpeed) / Mathf.Max(bakedParams.SwimSpeed - bakedParams.IdleSpeed, 1e-3f));
            for (int i = 0; i < n; i++)
            {
                if (bones[i].t == null) continue;
                bones[i].t.localRotation = local[i];
                FishBoneData d = bakedBones[i];
                if (math.any(d.ScaleAmp != 0f) || math.any(d.IdleScaleAmp != 0f)) bones[i].t.localScale = localScale[i];
            }
        }
        finally
        {
            inputs.Dispose(); prms.Dispose(); ranges.Dispose(); states.Dispose();
            hist.Dispose(); boneData.Dispose(); local.Dispose(); world.Dispose(); localScale.Dispose();
        }

        if (FishAnimationSystem.Instance != null && SystemIndex >= 0) FishAnimationSystem.Instance.OverridePhase(this, st.BodyPhase, st.FinPhase);
    }

    /// Trạng thái ban đầu sau Build: pha đặt theo TimeOffset (tương đương pose tĩnh tại time = TimeOffset), chưa có lịch sử uốn.
    public FishSimState InitialState() => PhaseState(timeOffset);

    private FishSimState PhaseState(float time)
    {
        return new FishSimState
        {
            Clock = 0f,
            PrevRot = quaternion.identity,
            HasPrev = 0,
            YawRate = 0f,
            PitchRate = 0f,
            HistHead = 0,
            BodyPhase = time * bakedParams.SwayFrequency * Mathf.PI * 2f,
            FinPhase = time * bakedParams.FinFrequency * Mathf.PI * 2f,
            IdleBlend = Mathf.Clamp01((speed - bakedParams.IdleSpeed) / Mathf.Max(bakedParams.SwimSpeed - bakedParams.IdleSpeed, 1e-3f))   // bắt đầu đúng chế độ hiện tại, không ramp
        };
    }

    // ---------- Tiện ích cho class con ----------

    /// Đăng ký bone: lưu bind pose (localRotation lúc gọi) để ResetPose / job dùng.
    protected void RegisterBone(BoneRef b)
    {
        b.bindLocal = b.t.localRotation;
        b.bindScale = b.t.localScale;
        bones.Add(b);
    }

    protected static int Depth(Transform t) { int d = 0; while (t.parent != null) { d++; t = t.parent; } return d; }

    /// <summary>Bypass <see cref="Core.Manager.LevelManager.IsSimulationFrozen"/> so this fish can still wag (tutorial flight).</summary>
    public void SetIgnoreSimulationFrozen(bool ignore) => ignoreSimulationFrozen = ignore;

    // ---------- Bake ----------

    /// Với mỗi bone: bone đã đăng ký gần nhất phía trên (index trong Bones, -1 = root) và tích localRotation của các node trung gian
    /// không đăng ký từ ngay dưới bone cha xuống tới cha trực tiếp (coi là tĩnh). Job nhân dồn world rotation từ root theo chuỗi này
    /// thay vì đọc Transform.
    private void BuildParentChain()
    {
        int n = bones.Count;
        bakedParent = new int[n];
        bakedChain = new quaternion[n];
        var indexOf = new Dictionary<Transform, int>(n);
        for (int i = 0; i < n; i++) indexOf[bones[i].t] = i;

        var between = new List<Transform>();
        Transform root = transform;
        for (int i = 0; i < n; i++)
        {
            between.Clear();
            int parent = -1;
            Transform p = bones[i].t.parent;
            while (p != null && p != root && !indexOf.TryGetValue(p, out parent))
            {
                between.Add(p);
                p = p.parent;
            }
            if (p == null)
            {
                // bone không nằm dưới animator: coi cha trực tiếp là tĩnh so với root
                parent = -1;
                between.Clear();
                Transform direct = bones[i].t.parent;
                bakedChain[i] = direct != null ? (quaternion)(Quaternion.Inverse(root.rotation) * direct.rotation) : quaternion.identity;
                bakedParent[i] = -1;
                continue;
            }
            if (p == root) parent = -1;

            quaternion chain = quaternion.identity;
            for (int k = between.Count - 1; k >= 0; k--) chain = math.mul(chain, between[k].localRotation);
            bakedParent[i] = parent;
            bakedChain[i] = chain;
        }
    }

    /// Bake lại toàn bộ bone và tham số từ Inspector (không đổi cấu trúc bone).
    private void Rebake()
    {
        int n = bones.Count;
        if (bakedBones.Length != n) bakedBones = new FishBoneData[n];
        for (int i = 0; i < n; i++)
        {
            FishBoneData d = BakeBone(bones[i]);
            d.Parent = bakedParent[i];
            d.StaticChain = bakedChain[i];
            d.BindLocal = bones[i].bindLocal;
            d.BindScale = bones[i].bindScale;
            if (d.IdleVariant == 0)
            {
                // class không đặt bộ idle riêng: idle = bơi để blend theo Speed không đổi gì
                d.IdleAmp = d.Amp;
                d.IdlePhaseLag = d.PhaseLag;
                d.IdleBias = d.Bias;
                d.IdleScaleAmp = d.ScaleAmp;
                d.IdlePhaseSkew = d.PhaseSkew;
                d.IdlePulseSharpen = d.PulseSharpen;
                d.IdlePitchBias = d.PitchBias;
            }
            bakedBones[i] = d;
        }

        var p = new FishParams
        {
            TurnBend = turnBend,
            PitchBend = pitchBend,
            BendDelay = bendDelay,
            BendSmoothing = bendSmoothing,
            MaxBendPerBone = maxBendPerBone,
            IdleSpeed = idleSpeed,
            SwimSpeed = Mathf.Max(swimSpeed, idleSpeed + 0.01f),
            BlendSmoothing = blendSmoothing,
            UpAxis = new float3(0f, 1f, 0f),
            ForwardAxis = new float3(0f, 0f, 1f),
            LateralAxis = new float3(1f, 0f, 0f),
            TailSign = -1f
        };
        OnBakeParams(ref p);
        bakedParams = p;
        BakeDirty = true;
    }
}
