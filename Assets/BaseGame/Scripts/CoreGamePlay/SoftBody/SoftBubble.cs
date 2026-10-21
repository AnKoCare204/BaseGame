using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Một bong bóng soft body, mô phỏng bởi SoftBubbleWorld. Lưới lấy từ SoftBubbleMesh trong Config (bán kính 1, nhân theo Radius);
/// mỗi bóng tạo một Mesh render riêng lúc Awake và solver ghi lại đỉnh mỗi frame.
/// Transform của object được đặt theo tâm bóng mỗi frame; mesh vertex nằm trong không gian world
/// (không xoay theo transform), nên để scale = 1 và không parent dưới object có scale/rotation.
/// Kéo transform trong Scene view (hoặc gán transform.position từ code) khi đang Play sẽ dịch cả bóng theo.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class SoftBubble : MonoBehaviour
{
    [field: SerializeField] public SoftBubbleConfig Config { get; private set; }

    [field: SerializeField, Tooltip("MeshFilter cùng GameObject (gán sẵn trên prefab) nhận mesh render dựng từ SoftBubbleMesh lúc Awake")]
    public MeshFilter MeshFilter { get; private set; }

    [field: SerializeField, Tooltip("MeshRenderer cùng GameObject (gán sẵn trên prefab) để gán seed shader")]
    public MeshRenderer MeshRenderer { get; private set; }
    [field: SerializeField, Min(0.01f), Tooltip("Bán kính hiển thị ban đầu. Ở runtime đổi bằng SetRadius")]
    public float Radius { get; private set; } = 0.5f;

    /// <summary>Bán kính đích của SetRadius. Bằng Radius khi không tween.</summary>
    public float TargetRadius { get; private set; }

    /// <summary>Đang tween bán kính về TargetRadius.</summary>
    public bool IsResizing => resizeDuration > 0f;

    /// <summary>Index trong SoftBubbleWorld, -1 nếu chưa đăng ký.</summary>
    public int Index { get; internal set; } = -1;

    internal bool LayoutValid { get; set; }

    public Vector3 Center { get; private set; }
    public Vector3 Velocity { get; private set; }

    /// <summary>Bán kính hiển thị hiện tại (trung bình khoảng cách particle tới tâm cộng ParticleRadius).</summary>
    public float CurrentRadius { get; private set; }

    /// <summary>Bán kính mỗi particle, do world tính từ config khi đăng ký.</summary>
    public float ParticleRadius { get; internal set; }

    public int ParticleCount => MeshData != null ? MeshData.Vertices.Length : 0;

    /// <summary>Số particle đang bị ghim.</summary>
    public int PinCount => PinMap.Count;

    internal Dictionary<int, ParticlePin> PinMap { get; } = new();

    /// <summary>Thể tích hiện tại so với lúc nghỉ. Nhỏ hơn 1 = đang bị ép.</summary>
    public float VolumeRatio { get; private set; } = 1f;

    /// <summary>Độ giãn lớn nhất của một cạnh (0 = không giãn, 0.2 = giãn 20%).</summary>
    public float MaxStrain { get; private set; }

    /// <summary>Đóng băng bóng: bỏ qua mô phỏng nhưng vẫn là vật cản cho bóng khác.</summary>
    public bool Frozen { get; set; }

    /// <summary>
    /// Tọa độ Z mà tâm bóng bị giữ khi SoftBubbleWorld bật Lock Z.
    /// Mặc định lấy từ vị trí lúc Awake, Teleport hoặc kéo transform sẽ cập nhật lại.
    /// </summary>
    public float PlaneZ { get; set; }

    /// <summary>Bóng đang ngủ (đứng yên, bỏ qua mô phỏng cho tới khi bị tác động).</summary>
    public bool IsSleeping { get; private set; }

    public Icosphere.Data MeshData { get; private set; }

    /// <summary>Lưới hiển thị, cùng mức hoặc mịn hơn lưới mô phỏng; N đỉnh đầu trùng N particle bề mặt.</summary>
    public Icosphere.Data VisualData { get; private set; }

    public int VisualVertexCount => VisualData != null ? VisualData.Vertices.Length : 0;

    public event Action<SoftBubble> OnContactBegin;
    public event Action<SoftBubble> OnContactEnd;

    internal bool TeleportRequested { get; private set; }
    internal Vector3 TeleportPosition { get; private set; }

    /// <summary>GetInstanceID cache (world dùng làm khoá cặp chạm mỗi frame, tránh gọi native).</summary>
    internal int CachedId => cachedId != 0 ? cachedId : cachedId = GetInstanceID();

    /// <summary>Upload đỉnh mỗi frame: bounds đặt tay ngay sau, index không đổi nên bỏ các bước kiểm tra / tính lại của Unity.</summary>
    private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

    private Mesh mesh;
    private int cachedId;
    private Vector3 lastAppliedPosition;
    private Vector3 pendingVelocity;
    private bool hasTarget;
    private Vector3 target;
    private float targetStiffness;
    private float targetDamping;
    private float3[] backupPos;
    private float3[] backupPrev;
    private float3[] backupVel;
    private float resizeStart;
    private float resizeDuration;
    private float resizeElapsed;
    private bool radiusDirty;
    private bool wakeRequested;

    private void Awake()
    {
        if (Config == null)
        {
            Debug.LogWarning($"SoftBubble '{name}' chưa gán Config, dùng giá trị mặc định.", this);
        }

        TargetRadius = Radius;
        PlaneZ = transform.position.z;
        lastAppliedPosition = transform.position;
        BuildMesh();
        ApplyShaderSeed();
    }

    /// <summary>Gán _Seed ngẫu nhiên cho shader FishSort/Soft Bubble để mỗi bóng lắc và ánh màu khác nhau.</summary>
    private void ApplyShaderSeed()
    {
        MeshRenderer renderer = MeshRenderer;
        if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(ShaderSeedId)) return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetFloat(ShaderSeedId, UnityEngine.Random.Range(0f, 100f));
        renderer.SetPropertyBlock(block);
    }

    private static readonly int ShaderSeedId = Shader.PropertyToID("_Seed");

    private void OnEnable()
    {
        // Sau domain reload (sửa script khi đang Play) mesh và dữ liệu lưới bị mất, dựng lại trước khi đăng ký.
        if (MeshData == null || VisualData == null || mesh == null)
        {
            PlaneZ = transform.position.z;
            lastAppliedPosition = transform.position;
            BuildMesh();
        }

        SoftBubbleWorld world = SoftBubbleWorld.Get();
        if (world != null) world.Register(this);
    }

    private void OnDisable()
    {
        PinMap.Clear();
        if (SoftBubbleWorld.Instance != null) SoftBubbleWorld.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    /// <summary>Mức chia icosphere sinh tạm khi Config chưa có SoftBubbleMesh hợp lệ.</summary>
    private const int FallbackSubdivisions = 2;

    private const int FallbackVisualSubdivisions = 4;

    private void BuildMesh()
    {
        SoftBubbleMesh source = Config != null ? Config.MeshAsset : null;
        if (source != null && source.IsValid)
        {
            MeshData = source.Sim;
            VisualData = source.Visual;
        }
        else
        {
            Debug.LogWarning($"SoftBubble '{name}' chưa có SoftBubbleMesh hợp lệ trong Config, sinh tạm icosphere lúc runtime.", this);
            MeshData = Icosphere.Get(FallbackSubdivisions);
            VisualData = Icosphere.Get(FallbackVisualSubdivisions);
        }

        int vertexCount = VisualData.Vertices.Length;
        var indices = new int[VisualData.Triangles.Length * 3];
        for (int i = 0; i < VisualData.Triangles.Length; i++)
        {
            int3 tri = VisualData.Triangles[i];
            indices[i * 3] = tri.x;
            indices[i * 3 + 1] = tri.y;
            indices[i * 3 + 2] = tri.z;
        }

        // Stream 0 = vị trí + pháp tuyến (SoftVisualVertex, solver ghi lại mỗi frame bằng một lần SetVertexBufferData), stream 1 = uv cố định.
        mesh = new Mesh { name = "SoftBubble" };
        mesh.MarkDynamic();
        mesh.SetVertexBufferParams(vertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
        var initial = new NativeArray<SoftVisualVertex>(vertexCount, Allocator.Temp);
        for (int i = 0; i < vertexCount; i++)
        {
            float3 v = VisualData.Vertices[i];
            initial[i] = new SoftVisualVertex { Position = v * Radius, Normal = v };
        }

        mesh.SetVertexBufferData(initial, 0, 0, vertexCount, 0);
        initial.Dispose();
        mesh.SetVertexBufferData(VisualData.Uvs, 0, 0, vertexCount, 1);
        mesh.SetTriangles(indices, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (Radius * 2.5f));

        if (MeshFilter != null) MeshFilter.sharedMesh = mesh;
        else Debug.LogError($"SoftBubble '{name}' chưa gán MeshFilter nên không hiển thị được.", this);
        Center = transform.position;
        CurrentRadius = Radius;
    }

    /// <summary>Cộng thêm vận tốc vào toàn bộ bóng ở step tiếp theo.</summary>
    public void AddVelocity(Vector3 velocity) => pendingVelocity += velocity;

    /// <summary>Kéo tâm bóng về một vị trí bằng lò xo. Dùng cho drag/drop, xếp vào ô.</summary>
    public void SetTarget(Vector3 worldPosition, float stiffness = 60f, float damping = 10f)
    {
        hasTarget = true;
        target = worldPosition;
        targetStiffness = stiffness;
        targetDamping = damping;
    }

    public void ClearTarget() => hasTarget = false;
    [Button]
    public void SetRadius(float radius, float duration = 0f)
    {
        radius = Mathf.Max(0.01f, radius);
        TargetRadius = radius;
        wakeRequested = true;

        if (duration <= 0f)
        {
            resizeDuration = 0f;
            if (Mathf.Approximately(Radius, radius)) return;
            Radius = radius;
            radiusDirty = true;
            return;
        }

        resizeStart = Radius;
        resizeDuration = duration;
        resizeElapsed = 0f;
    }

    /// <summary>Đánh thức bóng đang ngủ ở step tiếp theo mà không tác động lực.</summary>
    public void Wake() => wakeRequested = true;

    /// <summary>
    /// Đổi config lúc chạy (IceBubble vỡ băng chuyển về config thường). Lưới mô phỏng giữ nguyên (dựng từ config cũ lúc Awake), world
    /// dựng lại layout để lấy params và luật va chạm mới nhưng giữ trạng thái particle. Config có MeshAsset khác thì chỉ áp được params.
    /// </summary>
    public void SetConfig(SoftBubbleConfig config)
    {
        if (config == null || config == Config) return;
        Config = config;
        wakeRequested = true;
        if (Index >= 0 && SoftBubbleWorld.Instance != null) SoftBubbleWorld.Instance.MarkLayoutDirty();
    }

    /// <summary>World gọi mỗi step: tiến tween bán kính. Trả về true khi Radius đổi và world cần cập nhật rest của bóng.</summary>
    internal bool TryConsumeRadiusChange(float dt)
    {
        if (resizeDuration > 0f)
        {
            resizeElapsed += dt;
            float t = Mathf.Clamp01(resizeElapsed / resizeDuration);
            float r = Mathf.SmoothStep(resizeStart, TargetRadius, t);
            if (t >= 1f)
            {
                resizeDuration = 0f;
                r = TargetRadius;
            }

            if (!Mathf.Approximately(r, Radius))
            {
                Radius = r;
                radiusDirty = true;
            }
        }

        if (!radiusDirty) return false;
        radiusDirty = false;
        wakeRequested = true;
        return true;
    }

    /// <summary>Đặt lại toàn bộ particle về hình cầu nghỉ tại vị trí mới, vận tốc bằng 0.</summary>
    public void Teleport(Vector3 worldPosition)
    {
        TeleportRequested = true;
        TeleportPosition = worldPosition;
        PlaneZ = worldPosition.z;
    }

    /// <summary>
    /// Ghim một particle về vị trí world. Ghim tồn tại cho tới khi ClearPin. Stiffness 1 = đi theo tuyệt đối,
    /// nhỏ hơn thì particle bị kéo mềm hơn. Bóng bị ghim luôn thức.
    /// </summary>
    public void SetPin(int particle, Vector3 worldTarget, float stiffness = 1f)
    {
        if (particle < 0 || particle >= ParticleCount) return;
        PinMap[particle] = new ParticlePin { Particle = particle, Target = worldTarget, Stiffness = Mathf.Clamp01(stiffness) };
    }

    public void ClearPin(int particle) => PinMap.Remove(particle);

    public void ClearPins() => PinMap.Clear();

    /// <summary>Vị trí world của particle mô phỏng (chưa cộng ParticleRadius).</summary>
    public bool TryGetParticlePosition(int particle, out Vector3 position)
    {
        position = default;
        SoftBubbleWorld world = SoftBubbleWorld.Instance;
        return world != null && world.TryGetParticlePosition(this, particle, out position);
    }

    /// <summary>Thêm vào results index các particle nằm trong bán kính quanh point. Trả về số particle tìm được.</summary>
    public int FindParticlesNear(Vector3 point, float radius, List<int> results)
    {
        SoftBubbleWorld world = SoftBubbleWorld.Instance;
        return world != null ? world.FindParticlesNear(this, point, radius, results) : 0;
    }

    /// <summary>Index particle gần point nhất, hoặc -1.</summary>
    public int FindNearestParticle(Vector3 point)
    {
        SoftBubbleWorld world = SoftBubbleWorld.Instance;
        return world != null ? world.FindNearestParticle(this, point) : -1;
    }

    internal BubbleCommand BuildCommand()
    {
        return new BubbleCommand
        {
            VelocityDelta = pendingVelocity,
            Target = target,
            TargetStiffness = targetStiffness,
            TargetDamping = targetDamping,
            HasTarget = hasTarget ? 1 : 0,
            Frozen = Frozen ? 1 : 0,
            PlaneZ = PlaneZ,
            Wake = wakeRequested ? 1 : 0
        };
    }

    internal void ClearOneShot()
    {
        pendingVelocity = Vector3.zero;
        TeleportRequested = false;
        wakeRequested = false;
    }

    /// <summary>
    /// Transform bị di chuyển từ bên ngoài (kéo trong Scene view, gán transform.position) kể từ lần solver ghi gần nhất?
    /// Trả về độ dịch và cập nhật PlaneZ theo vị trí mới.
    /// </summary>
    internal bool TryConsumeExternalMove(out Vector3 delta)
    {
        Vector3 current = transform.position;
        delta = current - lastAppliedPosition;
        if (delta.sqrMagnitude <= 1e-10f) return false;

        lastAppliedPosition = current;
        PlaneZ = current.z;
        return true;
    }

    internal void SyncAppliedPosition() => lastAppliedPosition = transform.position;

    /// <summary>World gọi sau mỗi step có mô phỏng bóng này: trạng thái tổng hợp, transform theo tâm, upload đỉnh lưới hiển thị (vị trí so với tâm + pháp tuyến).</summary>
    internal void ApplyState(in BubbleState state, float restVolume, NativeArray<SoftVisualVertex> vertices)
    {
        Center = state.Center;
        Velocity = state.Velocity;
        CurrentRadius = state.Radius + ParticleRadius;
        VolumeRatio = restVolume > 1e-8f ? state.Volume / restVolume : 1f;
        MaxStrain = state.MaxStrain;
        IsSleeping = state.Sleeping != 0;

        transform.SetPositionAndRotation(Center, Quaternion.identity);
        lastAppliedPosition = Center;
        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, UploadFlags);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * ((state.MaxRadius + ParticleRadius) * 2.2f));
    }

    private void OnDrawGizmos()
    {
        // Ở edit mode chưa có mesh, vẽ khung cầu để thấy và chọn được bóng trong Scene view.
        if (Application.isPlaying) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }

    internal void Backup(NativeArray<float3> pos, NativeArray<float3> prev, NativeArray<float3> vel, in BubbleDesc desc)
    {
        int count = desc.ParticleCount;
        backupPos = new float3[count];
        backupPrev = new float3[count];
        backupVel = new float3[count];
        NativeArray<float3>.Copy(pos, desc.ParticleStart, backupPos, 0, count);
        NativeArray<float3>.Copy(prev, desc.ParticleStart, backupPrev, 0, count);
        NativeArray<float3>.Copy(vel, desc.ParticleStart, backupVel, 0, count);
    }

    internal bool TryRestore(NativeArray<float3> pos, NativeArray<float3> prev, NativeArray<float3> vel, in BubbleDesc desc)
    {
        if (backupPos == null || backupPos.Length != desc.ParticleCount) return false;
        NativeArray<float3>.Copy(backupPos, 0, pos, desc.ParticleStart, desc.ParticleCount);
        NativeArray<float3>.Copy(backupPrev, 0, prev, desc.ParticleStart, desc.ParticleCount);
        NativeArray<float3>.Copy(backupVel, 0, vel, desc.ParticleStart, desc.ParticleCount);
        backupPos = null;
        backupPrev = null;
        backupVel = null;
        return true;
    }

    internal void RaiseContactBegin(SoftBubble other) => OnContactBegin?.Invoke(other);
    internal void RaiseContactEnd(SoftBubble other) => OnContactEnd?.Invoke(other);
}
