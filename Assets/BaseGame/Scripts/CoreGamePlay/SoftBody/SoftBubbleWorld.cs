using Core.Manager;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Quản lý mọi SoftBubble và SoftBodyShape trong scene, chạy solver XPBD bằng Jobs + Burst.
/// Phải đặt sẵn một GameObject SoftBubbleWorld trong scene (không tự tạo lúc chạy); bóng / shape tìm qua Get().
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class SoftBubbleWorld : MonoBehaviour
{
    public static SoftBubbleWorld Instance { get; private set; }
    private static bool isQuitting;

    [SerializeField] private SoftSimulationMode Mode = SoftSimulationMode.Update;

    [SerializeField, Range(60f, 1200f), Tooltip("Tần số substep cố định (Hz). Số substep mỗi frame tự tính theo deltaTime nên độ cứng, độ mềm " +
                                                 "không đổi giữa Editor 200 fps và điện thoại 60 fps. 360 = 6 substep ở 60 fps")]
    private float SubstepHz = 360f;

    [SerializeField, Range(1, 24), Tooltip("Trần số substep mỗi frame khi tụt fps, tránh tốn CPU đột biến")]
    private int MaxSubstepsPerStep = 12;

    [SerializeField, Tooltip("Chặn deltaTime lớn để không nổ khi tụt fps (chỉ Mode = Update)")]
    private float MaxDeltaTime = 1f / 60f;

    [SerializeField] private Vector3 Gravity = new(0f, -9.81f, 0f);

    [SerializeField, Tooltip("Giữ tâm mỗi bóng cố định tại SoftBubble.PlaneZ, bóng chỉ di chuyển trong mặt phẳng XY")]
    private bool LockZ = true;

    // Giới hạn cứng theo cạnh (SetRigidBounds): bit 1 trái, 2 phải, 4 dưới, 8 trên; bóng vượt cạnh bật thì bị dịch nguyên khối
    // vào trong, không bẹt. GamePlayRectManager đặt cho cạnh trên của vùng chơi.
    private int rigidMask;
    private Vector4 rigidRect;

    [SerializeField, Tooltip("Khoảng hở tối đa giữa hai mặt bóng để vẫn tính là đang chạm (cho event)")]
    private float ContactEventSlack = 0.03f;

    [SerializeField, Tooltip("Layer của collider được bọc khi bấm nút Wrap Scene Colliders (công cụ editor, thêm sẵn SoftBodyShape vào scene)")]
    private LayerMask WrapLayers = ~0;

    [SerializeField] private bool DrawGizmos = true;

    /// <summary>Hook tùy biến luật tiếp xúc cho từng cặp bóng. Null = trung bình config của hai bóng.
    /// Gọi MarkContactRulesDirty() sau khi đổi để áp dụng.</summary>
    public Func<SoftBubble, SoftBubble, ContactRule> ContactRuleResolver { get; set; }

    public event Action<SoftBubble, SoftBubble> OnContactBegin;
    public event Action<SoftBubble, SoftBubble> OnContactEnd;

    public IReadOnlyList<SoftBubble> Bubbles => bubbles;
    public int ContactCount => touching.Count;

    /// <summary>Bật/tắt khóa trục Z lúc runtime.</summary>
    public bool LockZAxis
    {
        get => LockZ;
        set => LockZ = value;
    }

    private readonly List<SoftBubble> bubbles = new();
    private readonly List<SoftBodyShape> shapes = new();
    private readonly List<SoftBubbleConfig> configs = new();
    private readonly Dictionary<long, (SoftBubble A, SoftBubble B)> touching = new();
    private readonly Dictionary<long, (SoftBubble A, SoftBubble B)> touchingNow = new();
    private readonly List<(SoftBubble A, SoftBubble B)> beganPairs = new();
    private readonly List<long> endedKeys = new();
    private readonly List<ParticlePin> pinBuffer = new();
    private readonly List<int> resizedBuffer = new();
    private SoftBubbleConfig fallbackConfig;

    private bool layoutDirty;
    private bool rulesDirty;
    private bool allocated;
    private bool statesInA;
    private bool wakeAllNextStep;
    private SoftShapeData[] lastShapeData = Array.Empty<SoftShapeData>();

    private NativeArray<BubbleDesc> descs;
    private NativeArray<SoftBubbleParams> paramsArray;
    private NativeArray<BubbleCommand> commands;
    private NativeArray<BubbleState> statesA;
    private NativeArray<BubbleState> statesB;
    private NativeArray<ContactRule> pairRules;
    private NativeArray<SoftShapeData> shapeData;
    private NativeArray<ParticlePin> pins;
    private NativeArray<float3> pos;
    private NativeArray<float3> prevPos;
    private NativeArray<float3> vel;
    private NativeArray<float3> posSnapshot;
    private NativeArray<float3> restPos;
    private NativeArray<float3> scratch;
    private NativeArray<SoftVisualVertex> visual;   // đỉnh lưới hiển thị (vị trí so với tâm + pháp tuyến) của mọi bóng
    private NativeArray<float3> visualNormals;      // scratch gom pháp tuyến cho ComputeMeshJob
    private NativeArray<int> untouched;             // 1 = bóng không được mô phỏng trong step (ngủ / frozen): bỏ qua dựng lưới, upload
    private NativeArray<int2> visualParents;
    private NativeArray<int3> visualTris;
    private NativeArray<int2> edges;
    private NativeArray<float> restLength;
    private NativeArray<int3> tris;

    // Step đang chạy trên worker: schedule ở Update, hoàn tất ở LateUpdate (main thread làm việc khác trong lúc solver chạy).
    private JobHandle stepHandle;
    private bool stepPending;

    private static readonly ProfilerMarker ScheduleMarker = new("SoftBubbleWorld.Schedule");
    private static readonly ProfilerMarker WaitMarker = new("SoftBubbleWorld.Wait");
    private static readonly ProfilerMarker ApplyMarker = new("SoftBubbleWorld.Apply");

    /// <summary>
    /// World đặt sẵn trong scene. Trước khi Awake của world chạy (thứ tự OnEnable giữa các object lúc load scene) thì tìm
    /// một lần trong scene; không có thì báo lỗi, không tự tạo.
    /// </summary>
    public static SoftBubbleWorld Get()
    {
        if (Instance != null) return Instance;
        if (isQuitting) return null;
        Instance = FindFirstObjectByType<SoftBubbleWorld>();
        if (Instance == null) Debug.LogError("Scene chưa có SoftBubbleWorld: hãy đặt sẵn một GameObject có SoftBubbleWorld trong scene.");
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Đã có SoftBubbleWorld khác trong scene, hủy bản này.", this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

#if UNITY_EDITOR
    /// <summary>Công cụ dựng scene: bọc mọi collider Unity (theo WrapLayers) chưa có SoftBodyShape thành SoftBodyShape gán sẵn AttachedCollider.</summary>
    [ContextMenu("Wrap Scene Colliders")]
    private void WrapSceneCollidersInEditor()
    {
        int added = SoftBodyShape.WrapSceneColliders(WrapLayers, true);
        Debug.Log($"SoftBubbleWorld: đã bọc {added} collider Unity thành SoftBodyShape (đã gán AttachedCollider).", this);
    }
#endif

    private void OnApplicationQuit() => isQuitting = true;

    private void OnDestroy()
    {
        if (stepPending)
        {
            stepPending = false;
            stepHandle.Complete();
        }

        DisposeNative();
        if (shapeData.IsCreated) shapeData.Dispose();
        if (pins.IsCreated) pins.Dispose();
        if (Instance == this) Instance = null;
    }

    public void Register(SoftBubble bubble)
    {
        if (bubble == null || bubbles.Contains(bubble)) return;
        CompleteStep(); // step đang chạy dùng danh sách bóng hiện tại, áp kết quả xong mới đổi danh sách
        bubbles.Add(bubble);
        layoutDirty = true;
    }

    public void Unregister(SoftBubble bubble)
    {
        if (!bubbles.Contains(bubble)) return;
        CompleteStep();
        bubbles.Remove(bubble);
        bubble.Index = -1;
        bubble.LayoutValid = false;
        layoutDirty = true;
        RemoveContactsOf(bubble);
    }

    public void Register(SoftBodyShape shape)
    {
        if (shape != null && !shapes.Contains(shape)) shapes.Add(shape);
    }

    public void Unregister(SoftBodyShape shape) => shapes.Remove(shape);

    /// <summary>Gọi sau khi đổi ContactRuleResolver hoặc Contact trong config để tính lại bảng luật.</summary>
    public void MarkContactRulesDirty() => rulesDirty = true;

    /// <summary>
    /// Gọi sau khi một bóng đổi Config lúc chạy (SoftBubble.SetConfig): dựng lại layout để gán lại params và bảng luật; trạng thái
    /// particle của bóng đang có được giữ qua Backup / TryRestore trong Rebuild.
    /// </summary>
    public void MarkLayoutDirty()
    {
        layoutDirty = true;
        rulesDirty = true;
    }

    /// <summary>Đánh thức mọi bóng đang ngủ ở step tiếp theo.</summary>
    public void WakeAllBubbles() => wakeAllNextStep = true;

    /// <summary>Mặt nạ cạnh cứng đang bật (bit 1 trái, 2 phải, 4 dưới, 8 trên) và vùng (xMin, xMax, yMin, yMax).</summary>
    public int RigidMask => rigidMask;
    public Vector4 RigidRect => rigidRect;

    /// <summary>
    /// Đặt giới hạn cứng cho các cạnh chọn của rect (world, mặt phẳng XY): mặt bóng chạm tới cạnh đó thì cả bóng dừng nguyên
    /// khối, giữ hình cầu thay vì bẹt như chạm SoftBodyShape. Cạnh không chọn vẫn dùng tường shape như thường. Đổi thì đánh thức bóng.
    /// </summary>
    public void SetRigidBounds(Rect rect, bool left, bool right, bool bottom, bool top)
    {
        int mask = (left ? 1 : 0) | (right ? 2 : 0) | (bottom ? 4 : 0) | (top ? 8 : 0);
        var r = new Vector4(rect.xMin, rect.xMax, rect.yMin, rect.yMax);
        if (mask == rigidMask && (r - rigidRect).sqrMagnitude < 1e-8f) return;
        rigidMask = mask;
        rigidRect = r;
        wakeAllNextStep = true;
    }

    public void ClearRigidBounds()
    {
        if (rigidMask == 0) return;
        rigidMask = 0;
        wakeAllNextStep = true;
    }

    /// <summary>Số particle bề mặt (đỉnh mesh) của một bóng đã đăng ký.</summary>
    public int GetParticleCount(SoftBubble bubble)
    {
        return allocated && bubble != null && bubble.LayoutValid ? descs[bubble.Index].SurfaceCount : 0;
    }

    /// <summary>Vị trí world của một particle bề mặt (chưa cộng ParticleRadius). Đợi step đang chạy xong nếu có.</summary>
    public bool TryGetParticlePosition(SoftBubble bubble, int particle, out Vector3 position)
    {
        position = default;
        if (!allocated || bubble == null || !bubble.LayoutValid) return false;
        CompleteStep();
        BubbleDesc d = descs[bubble.Index];
        if (particle < 0 || particle >= d.SurfaceCount) return false;
        position = pos[d.ParticleStart + particle];
        return true;
    }

    /// <summary>Thêm vào results index các particle bề mặt nằm trong bán kính quanh point. Trả về số particle tìm được.</summary>
    public int FindParticlesNear(SoftBubble bubble, Vector3 point, float radius, List<int> results)
    {
        if (!allocated || bubble == null || !bubble.LayoutValid) return 0;
        CompleteStep();
        BubbleDesc d = descs[bubble.Index];
        float3 p = point;
        float r2 = radius * radius;
        int found = 0;
        for (int k = 0; k < d.SurfaceCount; k++)
        {
            if (math.distancesq(pos[d.ParticleStart + k], p) > r2) continue;
            results.Add(k);
            found++;
        }

        return found;
    }

    /// <summary>Index particle bề mặt gần point nhất, hoặc -1.</summary>
    public int FindNearestParticle(SoftBubble bubble, Vector3 point)
    {
        if (!allocated || bubble == null || !bubble.LayoutValid) return -1;
        CompleteStep();
        BubbleDesc d = descs[bubble.Index];
        float3 p = point;
        int best = -1;
        float bestSq = float.MaxValue;
        for (int k = 0; k < d.SurfaceCount; k++)
        {
            float sq = math.distancesq(pos[d.ParticleStart + k], p);
            if (sq >= bestSq) continue;
            bestSq = sq;
            best = k;
        }

        return best;
    }

    /// <summary>
    /// Mô phỏng tiến trước `seconds` giây ngay trong frame này (đồng bộ, từng bước stepDt), bỏ qua IsSimulationFrozen. beforeStep(dt)
    /// gọi trước mỗi bước để bên ngoài cấp lực như lúc chơi (Level.PushRising). Level dùng lúc loading để bóng đầu màn xếp xong chỗ
    /// trước khi màn loading đóng, không phụ thuộc fps (Update chỉ tiến tối đa MaxDeltaTime mỗi frame).
    /// </summary>
    public void Simulate(float seconds, float stepDt, Action<float> beforeStep = null)
    {
        if (seconds <= 0f) return;
        stepDt = Mathf.Clamp(stepDt, 1f / 600f, 1f / 30f);
        int steps = Mathf.CeilToInt(seconds / stepDt);
        for (int i = 0; i < steps; i++)
        {
            beforeStep?.Invoke(stepDt);
            ScheduleStep(stepDt);
            CompleteStep();
        }
    }

    /// <summary>
    /// Step schedule ở Update (order -50) và hoàn tất ở LateUpdate (cũng -50, trước BubbleFishContainer đặt cá theo bóng): solver chạy
    /// trên worker trong lúc main thread chạy Update của script khác. Vì vậy Center / transform bóng đọc trong Update là của step
    /// frame trước (trễ một frame); đọc trong LateUpdate hoặc lúc render thì là của frame này.
    /// </summary>
    private void Update()
    {
        if (Mode == SoftSimulationMode.Update) Step(Mathf.Min(Time.deltaTime, MaxDeltaTime));
    }

    private void FixedUpdate()
    {
        if (Mode == SoftSimulationMode.FixedUpdate) Step(Time.fixedDeltaTime);
    }

    private void LateUpdate() => CompleteStep();

    private void Step(float dt)
    {
        if (LevelManager.Instance != null && LevelManager.Instance.IsSimulationFrozen) return;
        ScheduleStep(dt);
    }

    /// <summary>Đọc lệnh từ gameplay, dựng lại layout nếu cần rồi schedule các substep + dựng lưới. Kết quả áp ở CompleteStep.</summary>
    private void ScheduleStep(float dt)
    {
        if (dt <= 0f) return;
        CompleteStep(); // FixedUpdate nhiều lần một frame / Simulate: step trước phải xong trước khi ghi lại mảng
        using (ScheduleMarker.Auto()) ScheduleStepCore(dt);
    }

    private void ScheduleStepCore(float dt)
    {
        if (layoutDirty)
        {
            Rebuild();
            wakeAllNextStep = true;
        }

        if (!allocated || bubbles.Count == 0) return;
        if (rulesDirty)
        {
            FillRules();
            rulesDirty = false;
            wakeAllNextStep = true;
        }

        UpdateParams();
        if (UpdateShapes()) wakeAllNextStep = true;
        if (UpdateCommands(dt)) wakeAllNextStep = true;

        int wakeAll = wakeAllNextStep ? 1 : 0;
        wakeAllNextStep = false;

        int n = bubbles.Count;
        for (int i = 0; i < n; i++) untouched[i] = 1;

        int substeps = Mathf.Clamp(Mathf.RoundToInt(dt * SubstepHz), 1, MaxSubstepsPerStep);
        float substepDt = dt / substeps;
        JobHandle handle = default;
        for (int s = 0; s < substeps; s++)
        {
            var snapshot = new SnapshotPositionsJob { Pos = pos, Snapshot = posSnapshot };
            handle = snapshot.Schedule(pos.Length, 256, handle);

            var job = new SolveSubstepJob
            {
                Dt = substepDt,
                Gravity = Gravity,
                BubbleCount = n,
                ShapeCount = shapes.Count,
                ApplyOneShot = s == 0 ? 1 : 0,
                LockZ = LockZ ? 1 : 0,
                RigidMask = rigidMask,
                RigidRect = new float4(rigidRect.x, rigidRect.y, rigidRect.z, rigidRect.w),
                WakeAll = wakeAll,
                Descs = descs,
                Params = paramsArray,
                Commands = commands,
                StatesRead = statesInA ? statesA : statesB,
                StatesWrite = statesInA ? statesB : statesA,
                PairRules = pairRules,
                Shapes = shapeData,
                Pins = pins,
                PosSnapshot = posSnapshot,
                Pos = pos,
                PrevPos = prevPos,
                Vel = vel,
                Scratch = scratch,
                RestPos = restPos,
                Edges = edges,
                RestLength = restLength,
                Tris = tris,
                Untouched = untouched
            };
            handle = job.Schedule(n, 1, handle);
            statesInA = !statesInA;
        }

        var meshJob = new ComputeMeshJob
        {
            Descs = descs,
            States = statesInA ? statesA : statesB,
            Untouched = untouched,
            Pos = pos,
            Tris = tris,
            VisualParents = visualParents,
            VisualTris = visualTris,
            Visual = visual,
            Normals = visualNormals
        };
        stepHandle = meshJob.Schedule(n, 1, handle);
        stepPending = true;
        JobHandle.ScheduleBatchedJobs(); // cho worker chạy ngay, không đợi tới Complete
    }

    /// <summary>Đợi step đang chạy xong rồi áp kết quả: trạng thái + mesh cho bóng có mô phỏng, event chạm. Không có step thì không làm gì.</summary>
    private void CompleteStep()
    {
        if (!stepPending) return;
        stepPending = false;
        using (WaitMarker.Auto()) stepHandle.Complete();

        using (ApplyMarker.Auto())
        {
            int n = bubbles.Count;
            NativeArray<BubbleState> current = statesInA ? statesA : statesB;
            for (int i = 0; i < n; i++)
            {
                SoftBubble bubble = bubbles[i];
                bubble.ClearOneShot();
                if (untouched[i] != 0) continue; // ngủ / frozen suốt step: trạng thái, transform và mesh không đổi
                BubbleDesc d = descs[i];
                bubble.ApplyState(current[i], d.RestVolume, visual.GetSubArray(d.VisualStart, d.VisualCount));
            }

            DetectContacts(current);
        }
    }

    private SoftBubbleConfig ConfigOf(SoftBubble bubble)
    {
        if (bubble.Config != null) return bubble.Config;
        if (fallbackConfig == null) fallbackConfig = ScriptableObject.CreateInstance<SoftBubbleConfig>();
        return fallbackConfig;
    }

    private void Rebuild()
    {
        if (allocated)
        {
            foreach (SoftBubble bubble in bubbles)
            {
                if (bubble.LayoutValid && bubble.Index >= 0 && bubble.Index < descs.Length)
                {
                    bubble.Backup(pos, prevPos, vel, descs[bubble.Index]);
                }
            }
        }

        DisposeNative();
        layoutDirty = false;
        rulesDirty = false;

        int n = bubbles.Count;
        if (n == 0) return;

        configs.Clear();
        int particleTotal = 0;
        int edgeTotal = 0;
        int triTotal = 0;
        int visualTotal = 0;
        int visualTriTotal = 0;
        foreach (SoftBubble bubble in bubbles)
        {
            SoftBubbleConfig cfg = ConfigOf(bubble);
            if (!configs.Contains(cfg)) configs.Add(cfg);
            Icosphere.Data data = bubble.MeshData;
            // Mỗi bóng thêm một particle tâm và một nan hoa cho mỗi đỉnh.
            particleTotal += data.Vertices.Length + 1;
            edgeTotal += data.Edges.Length + data.Vertices.Length;
            triTotal += data.Triangles.Length;
            visualTotal += bubble.VisualData.Vertices.Length;
            visualTriTotal += bubble.VisualData.Triangles.Length;
        }

        Allocate(n, particleTotal, edgeTotal, triTotal, visualTotal, visualTriTotal);

        int pOff = 0;
        int eOff = 0;
        int tOff = 0;
        int vOff = 0;
        int vtOff = 0;
        for (int i = 0; i < n; i++)
        {
            SoftBubble bubble = bubbles[i];
            SoftBubbleConfig cfg = ConfigOf(bubble);
            Icosphere.Data data = bubble.MeshData;
            Icosphere.Data visual = bubble.VisualData;
            int vc = data.Vertices.Length;
            int visualCount = visual.Vertices.Length;

            // Particle nằm trên mặt cầu nhỏ hơn bán kính hiển thị đúng bằng ParticleRadius; mesh được đẩy ra lại khi render.
            float visualRadius = bubble.Radius;
            float particleRadius = cfg.ResolveParticleRadius(visualRadius, data);
            float simRadius = Mathf.Max(visualRadius - particleRadius, visualRadius * 0.25f);

            int surfaceEdges = data.Edges.Length;
            var desc = new BubbleDesc
            {
                ParticleStart = pOff,
                ParticleCount = vc + 1,
                SurfaceCount = vc,
                EdgeStart = eOff,
                EdgeCount = surfaceEdges,
                RadialEdgeStart = eOff + surfaceEdges,
                RadialEdgeCount = vc,
                TriStart = tOff,
                TriCount = data.Triangles.Length,
                RestRadius = simRadius,
                ParticleRadius = particleRadius,
                EdgeLength = data.MeanEdgeLength * simRadius,
                VisualStart = vOff,
                VisualCount = visualCount,
                VisualTriStart = vtOff,
                VisualTriCount = visual.Triangles.Length,
                ParamsIndex = configs.IndexOf(cfg)
            };

            // Lưới hiển thị: visualCount đỉnh đầu trùng vc particle bề mặt, phần sau là trung điểm với cha ở index nhỏ hơn.
            for (int k = 0; k < visualCount; k++)
            {
                int2 parents = visual.Parents[k];
                visualParents[vOff + k] = parents.x >= 0 ? parents + vOff : new int2(-1, -1);
            }

            for (int k = 0; k < visual.Triangles.Length; k++) visualTris[vtOff + k] = visual.Triangles[k] + vOff;

            for (int k = 0; k < vc; k++) restPos[pOff + k] = data.Vertices[k] * simRadius;
            int hub = pOff + vc;
            restPos[hub] = float3.zero;

            for (int k = 0; k < surfaceEdges; k++)
            {
                int2 e = data.Edges[k] + pOff;
                edges[eOff + k] = e;
                restLength[eOff + k] = math.distance(restPos[e.x], restPos[e.y]);
            }

            for (int k = 0; k < vc; k++)
            {
                edges[eOff + surfaceEdges + k] = new int2(hub, pOff + k);
                restLength[eOff + surfaceEdges + k] = simRadius;
            }

            float vol = 0f;
            for (int k = 0; k < data.Triangles.Length; k++)
            {
                int3 tr = data.Triangles[k] + pOff;
                tris[tOff + k] = tr;
                vol += math.dot(restPos[tr.x], math.cross(restPos[tr.y], restPos[tr.z]));
            }

            desc.RestVolume = vol / 6f;
            descs[i] = desc;

            if (!bubble.TryRestore(pos, prevPos, vel, desc))
            {
                InitParticles(desc, bubble.transform.position);
                bubble.SyncAppliedPosition();
            }

            BubbleState state = MakeState(desc);
            statesA[i] = state;
            statesB[i] = state;

            bubble.Index = i;
            bubble.LayoutValid = true;
            bubble.ParticleRadius = particleRadius;
            pOff += vc + 1;
            eOff += surfaceEdges + vc;
            tOff += data.Triangles.Length;
            vOff += visualCount;
            vtOff += visual.Triangles.Length;
        }

        UpdateParams();

        statesInA = true;
        allocated = true;
        FillRules();
    }

    /// <summary>Copy lại tham số từ config mỗi step để chỉnh Inspector khi Play có tác dụng ngay.</summary>
    private void UpdateParams()
    {
        for (int c = 0; c < configs.Count; c++) paramsArray[c] = configs[c].ToParams();
    }

    private void Allocate(int bubbleCount, int particleTotal, int edgeTotal, int triTotal, int visualTotal, int visualTriTotal)
    {
        visual = new NativeArray<SoftVisualVertex>(visualTotal, Allocator.Persistent);
        visualNormals = new NativeArray<float3>(visualTotal, Allocator.Persistent);
        untouched = new NativeArray<int>(bubbleCount, Allocator.Persistent);
        visualParents = new NativeArray<int2>(visualTotal, Allocator.Persistent);
        visualTris = new NativeArray<int3>(math.max(1, visualTriTotal), Allocator.Persistent);
        descs = new NativeArray<BubbleDesc>(bubbleCount, Allocator.Persistent);
        paramsArray = new NativeArray<SoftBubbleParams>(math.max(1, configs.Count), Allocator.Persistent);
        commands = new NativeArray<BubbleCommand>(bubbleCount, Allocator.Persistent);
        statesA = new NativeArray<BubbleState>(bubbleCount, Allocator.Persistent);
        statesB = new NativeArray<BubbleState>(bubbleCount, Allocator.Persistent);
        pairRules = new NativeArray<ContactRule>(bubbleCount * bubbleCount, Allocator.Persistent);
        pos = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        prevPos = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        vel = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        posSnapshot = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        restPos = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        scratch = new NativeArray<float3>(particleTotal, Allocator.Persistent);
        edges = new NativeArray<int2>(edgeTotal, Allocator.Persistent);
        restLength = new NativeArray<float>(edgeTotal, Allocator.Persistent);
        tris = new NativeArray<int3>(triTotal, Allocator.Persistent);
    }

    private void DisposeNative()
    {
        allocated = false;
        if (descs.IsCreated) descs.Dispose();
        if (paramsArray.IsCreated) paramsArray.Dispose();
        if (commands.IsCreated) commands.Dispose();
        if (statesA.IsCreated) statesA.Dispose();
        if (statesB.IsCreated) statesB.Dispose();
        if (pairRules.IsCreated) pairRules.Dispose();
        if (pos.IsCreated) pos.Dispose();
        if (prevPos.IsCreated) prevPos.Dispose();
        if (vel.IsCreated) vel.Dispose();
        if (posSnapshot.IsCreated) posSnapshot.Dispose();
        if (restPos.IsCreated) restPos.Dispose();
        if (scratch.IsCreated) scratch.Dispose();
        if (visual.IsCreated) visual.Dispose();
        if (visualNormals.IsCreated) visualNormals.Dispose();
        if (untouched.IsCreated) untouched.Dispose();
        if (visualParents.IsCreated) visualParents.Dispose();
        if (visualTris.IsCreated) visualTris.Dispose();
        if (edges.IsCreated) edges.Dispose();
        if (restLength.IsCreated) restLength.Dispose();
        if (tris.IsCreated) tris.Dispose();
    }

    private void InitParticles(in BubbleDesc desc, float3 center)
    {
        int end = desc.ParticleStart + desc.ParticleCount;
        for (int i = desc.ParticleStart; i < end; i++)
        {
            float3 x = center + restPos[i];
            pos[i] = x;
            prevPos[i] = x;
            vel[i] = float3.zero;
        }
    }

    /// <summary>Dịch cả bóng (giữ nguyên biến dạng), đặt vận tốc về 0. Dùng khi transform bị kéo từ ngoài.</summary>
    private void ShiftParticles(in BubbleDesc desc, float3 delta)
    {
        int end = desc.ParticleStart + desc.ParticleCount;
        for (int i = desc.ParticleStart; i < end; i++)
        {
            float3 x = pos[i] + delta;
            pos[i] = x;
            prevPos[i] = x;
            vel[i] = float3.zero;
        }
    }

    private BubbleState MakeState(in BubbleDesc desc)
    {
        int end = desc.ParticleStart + desc.ParticleCount;
        float3 sum = float3.zero;
        for (int i = desc.ParticleStart; i < end; i++) sum += pos[i];
        float3 center = sum / desc.ParticleCount;

        // Bán kính chỉ tính trên particle bề mặt (particle tâm nằm ở giữa).
        int surfaceEnd = desc.ParticleStart + desc.SurfaceCount;
        float radius = 0f;
        float maxRadius = 0f;
        for (int i = desc.ParticleStart; i < surfaceEnd; i++)
        {
            float dist = math.length(pos[i] - center);
            radius += dist;
            maxRadius = math.max(maxRadius, dist);
        }

        radius /= desc.SurfaceCount;

        return new BubbleState
        {
            Center = center,
            Velocity = float3.zero,
            Rotation = quaternion.identity,
            Radius = radius,
            MaxRadius = maxRadius,
            Volume = desc.RestVolume,
            MaxStrain = 0f,
            SleepTimer = 0f,
            Sleeping = 0,
            Active = 1
        };
    }

    private void FillRules()
    {
        int n = bubbles.Count;
        for (int i = 0; i < n; i++)
        {
            SoftBubble a = bubbles[i];
            ContactRule ruleA = ConfigOf(a).Contact.ToRule();
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    pairRules[i * n + j] = ContactRule.Disabled;
                    continue;
                }

                SoftBubble b = bubbles[j];
                pairRules[i * n + j] = ContactRuleResolver != null
                    ? ContactRuleResolver(a, b)
                    : ContactRule.Average(ruleA, ConfigOf(b).Contact.ToRule());
            }
        }
    }

    /// <summary>Copy dữ liệu shape vào native array. Trả về true nếu có shape thay đổi (để đánh thức bóng đang ngủ).</summary>
    private bool UpdateShapes()
    {
        int count = shapes.Count;
        int needed = math.max(1, count);
        if (!shapeData.IsCreated || shapeData.Length < needed)
        {
            if (shapeData.IsCreated) shapeData.Dispose();
            shapeData = new NativeArray<SoftShapeData>(needed, Allocator.Persistent);
        }

        bool changed = lastShapeData.Length != count;
        if (changed) lastShapeData = new SoftShapeData[count];

        for (int i = 0; i < count; i++)
        {
            SoftShapeData data = shapes[i].ToData();
            if (!changed && ShapeChanged(in lastShapeData[i], in data)) changed = true;
            lastShapeData[i] = data;
            shapeData[i] = data;
        }

        return changed;
    }

    private static bool ShapeChanged(in SoftShapeData a, in SoftShapeData b)
    {
        return a.Type != b.Type
               || a.Inverted != b.Inverted
               || math.distancesq(a.Position, b.Position) > 1e-10f
               || math.lengthsq(a.Rotation.value - b.Rotation.value) > 1e-10f
               || math.distancesq(a.Size, b.Size) > 1e-10f;
    }

    /// <summary>
    /// Đọc lệnh và danh sách ghim từ từng bóng, tiến tween bán kính. Trả về true nếu có bóng bị teleport hoặc kéo
    /// (để đánh thức mọi bóng đang ngủ). Đổi bán kính chỉ đánh thức bóng đó và các bóng trong tầm chạm.
    /// </summary>
    private bool UpdateCommands(float dt)
    {
        bool moved = false;
        pinBuffer.Clear();
        resizedBuffer.Clear();
        for (int i = 0; i < bubbles.Count; i++)
        {
            SoftBubble bubble = bubbles[i];
            if (bubble.TeleportRequested)
            {
                InitParticles(descs[i], bubble.TeleportPosition);
                BubbleState state = MakeState(descs[i]);
                statesA[i] = state;
                statesB[i] = state;
                moved = true;
            }
            else if (bubble.TryConsumeExternalMove(out Vector3 delta))
            {
                ShiftParticles(descs[i], delta);
                BubbleState state = MakeState(descs[i]);
                statesA[i] = state;
                statesB[i] = state;
                moved = true;
            }

            if (bubble.TryConsumeRadiusChange(dt) && ResizeBubble(i, bubble)) resizedBuffer.Add(i);

            BubbleCommand command = bubble.BuildCommand();
            command.PinStart = pinBuffer.Count;
            foreach (ParticlePin pin in bubble.PinMap.Values) pinBuffer.Add(pin);
            command.PinCount = pinBuffer.Count - command.PinStart;
            commands[i] = command;
        }

        if (resizedBuffer.Count > 0) WakeNeighbors(resizedBuffer);

        int needed = math.max(1, pinBuffer.Count);
        if (!pins.IsCreated || pins.Length < needed)
        {
            if (pins.IsCreated) pins.Dispose();
            pins = new NativeArray<ParticlePin>(math.max(64, needed * 2), Allocator.Persistent);
        }

        for (int k = 0; k < pinBuffer.Count; k++) pins[k] = pinBuffer[k];
        return moved;
    }

    /// <summary>
    /// Cập nhật rest của một bóng theo bubble.Radius mới: vị trí nghỉ, độ dài nghỉ cạnh và nan hoa, thể tích nghỉ, bán kính particle.
    /// Không đụng vị trí particle: ràng buộc kéo bóng về kích thước mới trong vài substep. Trả về true nếu có thay đổi.
    /// Tính lại từ lưới gốc thay vì nhân dồn để tween nhiều frame không bị trôi số.
    /// </summary>
    private bool ResizeBubble(int i, SoftBubble bubble)
    {
        BubbleDesc d = descs[i];
        SoftBubbleConfig cfg = ConfigOf(bubble);
        Icosphere.Data data = bubble.MeshData;
        float visualRadius = bubble.Radius;
        float particleRadius = cfg.ResolveParticleRadius(visualRadius, data);
        float simRadius = Mathf.Max(visualRadius - particleRadius, visualRadius * 0.25f);
        if (Mathf.Abs(simRadius - d.RestRadius) < 1e-7f && Mathf.Abs(particleRadius - d.ParticleRadius) < 1e-7f) return false;

        int start = d.ParticleStart;
        for (int k = 0; k < d.SurfaceCount; k++) restPos[start + k] = data.Vertices[k] * simRadius;

        int edgeEnd = d.EdgeStart + d.EdgeCount;
        for (int e = d.EdgeStart; e < edgeEnd; e++)
        {
            int2 edge = edges[e];
            restLength[e] = math.distance(restPos[edge.x], restPos[edge.y]);
        }

        int radialEnd = d.RadialEdgeStart + d.RadialEdgeCount;
        for (int e = d.RadialEdgeStart; e < radialEnd; e++) restLength[e] = simRadius;

        float vol = 0f;
        int triEnd = d.TriStart + d.TriCount;
        for (int t = d.TriStart; t < triEnd; t++)
        {
            int3 tr = tris[t];
            vol += math.dot(restPos[tr.x], math.cross(restPos[tr.y], restPos[tr.z]));
        }

        d.RestVolume = vol / 6f;
        d.RestRadius = simRadius;
        d.ParticleRadius = particleRadius;
        d.EdgeLength = data.MeanEdgeLength * simRadius;
        descs[i] = d;
        bubble.ParticleRadius = particleRadius;
        return true;
    }

    /// <summary>
    /// Đánh thức các bóng trong tầm chạm của bóng vừa đổi kích thước. Bóng phình đối xứng gần như không có vận tốc tâm
    /// nên HasMovingNeighbor trong job không bắt được, hàng xóm đang ngủ sẽ bị lấn mà không phản ứng.
    /// </summary>
    private void WakeNeighbors(List<int> resized)
    {
        NativeArray<BubbleState> states = statesInA ? statesA : statesB;
        int n = bubbles.Count;
        foreach (int i in resized)
        {
            BubbleState si = states[i];
            float reachI = math.max(si.MaxRadius, descs[i].RestRadius) + descs[i].ParticleRadius;
            for (int j = 0; j < n; j++)
            {
                if (j == i || pairRules[i * n + j].Enabled == 0) continue;

                BubbleState sj = states[j];
                float reach = reachI + sj.MaxRadius + descs[j].ParticleRadius + 0.2f;
                if (math.distancesq(si.Center, sj.Center) > reach * reach) continue;

                BubbleCommand c = commands[j];
                c.Wake = 1;
                commands[j] = c;
            }
        }
    }

    private static long PairKey(SoftBubble a, SoftBubble b)
    {
        int ia = a.CachedId;
        int ib = b.CachedId;
        return ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
    }

    private void DetectContacts(NativeArray<BubbleState> states)
    {
        touchingNow.Clear();
        beganPairs.Clear();
        int n = bubbles.Count;
        for (int i = 0; i < n; i++)
        {
            BubbleState si = states[i];
            float ri = si.Radius + descs[i].ParticleRadius;
            for (int j = i + 1; j < n; j++)
            {
                ContactRule rule = pairRules[i * n + j];
                if (rule.Enabled == 0) continue;

                BubbleState sj = states[j];
                float rj = sj.Radius + descs[j].ParticleRadius;
                float gap = math.distance(si.Center, sj.Center) - (ri + rj + rule.Margin);
                if (gap > ContactEventSlack) continue;

                SoftBubble a = bubbles[i];
                SoftBubble b = bubbles[j];
                long key = PairKey(a, b);
                touchingNow[key] = (a, b);
                if (!touching.ContainsKey(key)) beganPairs.Add((a, b));
            }
        }

        endedKeys.Clear();
        foreach (KeyValuePair<long, (SoftBubble A, SoftBubble B)> kv in touching)
        {
            if (!touchingNow.ContainsKey(kv.Key)) endedKeys.Add(kv.Key);
        }

        foreach (long key in endedKeys)
        {
            (SoftBubble a, SoftBubble b) = touching[key];
            touching.Remove(key);
            if (a == null || b == null) continue;
            OnContactEnd?.Invoke(a, b);
            a.RaiseContactEnd(b);
            b.RaiseContactEnd(a);
        }

        foreach (KeyValuePair<long, (SoftBubble A, SoftBubble B)> kv in touchingNow) touching[kv.Key] = kv.Value;

        foreach ((SoftBubble a, SoftBubble b) in beganPairs)
        {
            OnContactBegin?.Invoke(a, b);
            a.RaiseContactBegin(b);
            b.RaiseContactBegin(a);
        }
    }

    private void RemoveContactsOf(SoftBubble bubble)
    {
        endedKeys.Clear();
        foreach (KeyValuePair<long, (SoftBubble A, SoftBubble B)> kv in touching)
        {
            if (kv.Value.A == bubble || kv.Value.B == bubble) endedKeys.Add(kv.Key);
        }

        foreach (long key in endedKeys) touching.Remove(key);
    }

    private void OnDrawGizmos()
    {
        if (!DrawGizmos || !allocated) return;

        foreach (SoftBubble b in bubbles)
        {
            if (b == null) continue;
            Gizmos.color = b.IsSleeping ? new Color(0.6f, 0.6f, 0.6f, 0.5f) : new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(b.Center, b.CurrentRadius);
        }

        Gizmos.color = Color.yellow;
        foreach (KeyValuePair<long, (SoftBubble A, SoftBubble B)> kv in touching)
        {
            if (kv.Value.A != null && kv.Value.B != null) Gizmos.DrawLine(kv.Value.A.Center, kv.Value.B.Center);
        }

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        foreach (SoftBubble b in bubbles)
        {
            if (b == null || b.PinMap.Count == 0) continue;
            foreach (ParticlePin pin in b.PinMap.Values) Gizmos.DrawSphere(pin.Target, 0.03f);
        }
    }
}
