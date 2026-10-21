using System.Collections.Generic;
using Core.Manager;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

/// Chạy animation procedural của mọi FishProceduralAnimator đang bật trong một cặp job Burst mỗi frame (FishPoseJob tính pose,
/// FishApplyJob ghi Transform) thay vì mỗi con tự Update trên main thread. Tự tạo lúc runtime khi con cá đầu tiên đăng ký,
/// sống qua các scene. Chạy ở LateUpdate sau khi FishSchool / FishSwimmer đã đặt vị trí, hướng cá trong frame.
/// Trạng thái mô phỏng (pha, lịch sử tốc độ góc) giữ trong system và được giữ nguyên khi cá khác vào / ra (dựng lại layout).
[DefaultExecutionOrder(200)]
public class FishAnimationSystem : MonoBehaviour
{
    public static FishAnimationSystem Instance { get; private set; }
    private static bool isQuitting;

    /// Số cá đang đăng ký (Inspector).
    public int FishCount => fishes.Count;
    public int BoneCount => boneCount;

    private readonly List<FishProceduralAnimator> fishes = new();
    private FishProceduralAnimator[] layoutFishes = System.Array.Empty<FishProceduralAnimator>(); // thứ tự cá của layout đang cấp phát
    private bool layoutDirty;
    private bool allocated;
    private int boneCount;

    private NativeArray<FishFrameInput> inputs;
    private NativeArray<FishParams> prms;
    private NativeArray<int2> ranges;
    private NativeArray<FishSimState> states;
    private NativeArray<float4> history;
    private NativeArray<FishBoneData> bones;
    private NativeArray<int> boneFish;
    private NativeArray<quaternion> localRot;
    private NativeArray<quaternion> worldRot;
    private NativeArray<float3> localScale;
    private TransformAccessArray transforms;

    /// Lấy system đang có hoặc tạo mới (chỉ lúc Play). Null khi đang thoát hoặc ở edit mode.
    public static FishAnimationSystem GetOrCreate()
    {
        if (Instance != null) return Instance;
        if (isQuitting || !Application.isPlaying) return null;
        var go = new GameObject("FishAnimationSystem");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FishAnimationSystem>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnApplicationQuit() => isQuitting = true;

    private void OnDestroy()
    {
        Dispose();
        if (Instance == this) Instance = null;
    }

    public void Register(FishProceduralAnimator animator)
    {
        if (animator == null || fishes.Contains(animator)) return;
        fishes.Add(animator);
        layoutDirty = true;
    }

    public void Unregister(FishProceduralAnimator animator)
    {
        if (!fishes.Remove(animator)) return;
        animator.SystemIndex = -1;
        layoutDirty = true;
    }

    /// Số bone của một con đổi (Build lại) thì layout phải dựng lại.
    public void MarkLayoutDirty() => layoutDirty = true;

    /// Đặt pha và xoá lịch sử uốn của một con (Evaluate). Không đụng Clock.
    public void OverridePhase(FishProceduralAnimator animator, float bodyPhase, float finPhase)
    {
        int f = animator.SystemIndex;
        if (!allocated || f < 0 || f >= layoutFishes.Length || layoutFishes[f] != animator) return;
        FishSimState st = states[f];
        st.BodyPhase = bodyPhase;
        st.FinPhase = finPhase;
        st.YawRate = st.PitchRate = 0f;
        states[f] = st;
        for (int k = 0; k < FishPoseJob.Hist; k++) history[f * FishPoseJob.Hist + k] = float4.zero;
    }

    private static readonly ProfilerMarker TickMarker = new("FishAnimationSystem.Tick");
    private static readonly ProfilerMarker WaitMarker = new("FishAnimationSystem.Wait");

    private void LateUpdate()
    {
        using (TickMarker.Auto()) Tick();
    }

    private void Tick()
    {
        if (layoutDirty) RebuildLayout();
        if (!allocated || layoutFishes.Length == 0) return;

        bool frozen = LevelManager.Instance != null && LevelManager.Instance.IsSimulationFrozen;
        float dt = Time.deltaTime;
        bool lost = false;
        for (int f = 0; f < layoutFishes.Length; f++)
        {
            FishProceduralAnimator a = layoutFishes[f];
            if (a == null)
            {
                inputs[f] = default; // bị hủy mà chưa Unregister: bỏ qua, dựng lại layout ở frame sau
                lost = true;
                continue;
            }

            if (a.BakeDirty) UploadBake(f, a);
            if (a.StateResetRequested) ResetState(f, a);
            inputs[f] = new FishFrameInput
            {
                RootRotation = a.CachedTransform.rotation,
                Speed = a.Speed,
                Dt = frozen && !a.IgnoreSimulationFrozen ? 0f : dt,
                Active = 1,
                PoseOnly = 0
            };
        }

        if (lost) layoutDirty = true;

        var pose = new FishPoseJob
        {
            Inputs = inputs,
            Params = prms,
            Ranges = ranges,
            Bones = bones,
            States = states,
            History = history,
            LocalRot = localRot,
            WorldRot = worldRot,
            LocalScale = localScale
        };
        JobHandle handle = pose.Schedule(layoutFishes.Length, 1);
        var apply = new FishApplyJob { LocalRot = localRot, LocalScale = localScale, Bones = bones, BoneFish = boneFish, Inputs = inputs };
        handle = apply.Schedule(transforms, handle);
        using (WaitMarker.Auto()) handle.Complete();

        for (int f = 0; f < layoutFishes.Length; f++)   // pha thân về main thread: loài theo nhịp (bạch tuộc) suy tốc độ di chuyển từ đây
            if (layoutFishes[f] != null)
            {
                layoutFishes[f].CurrentBodyPhase = states[f].BodyPhase;
                layoutFishes[f].CurrentIdleBlend = states[f].IdleBlend;
            }
    }

    private void RebuildLayout()
    {
        layoutDirty = false;
        for (int i = fishes.Count - 1; i >= 0; i--)
        {
            if (fishes[i] == null) fishes.RemoveAt(i);
        }

        // Giữ trạng thái mô phỏng của cá đã có trong layout cũ (không giật pha khi cá khác vào / ra).
        NativeArray<FishSimState> oldStates = states;
        NativeArray<float4> oldHistory = history;
        bool hadOld = allocated;
        var oldIndex = new Dictionary<FishProceduralAnimator, int>(layoutFishes.Length);
        if (hadOld)
        {
            for (int f = 0; f < layoutFishes.Length; f++)
            {
                if (layoutFishes[f] != null) oldIndex[layoutFishes[f]] = f;
            }
        }

        DisposeExceptState();

        int n = fishes.Count;
        layoutFishes = fishes.ToArray();
        boneCount = 0;
        foreach (FishProceduralAnimator a in layoutFishes) boneCount += a.BoneCount;

        if (n == 0)
        {
            if (hadOld)
            {
                oldStates.Dispose();
                oldHistory.Dispose();
            }

            return;
        }

        inputs = new NativeArray<FishFrameInput>(n, Allocator.Persistent);
        prms = new NativeArray<FishParams>(n, Allocator.Persistent);
        ranges = new NativeArray<int2>(n, Allocator.Persistent);
        states = new NativeArray<FishSimState>(n, Allocator.Persistent);
        history = new NativeArray<float4>(n * FishPoseJob.Hist, Allocator.Persistent);
        bones = new NativeArray<FishBoneData>(math.max(1, boneCount), Allocator.Persistent);
        boneFish = new NativeArray<int>(math.max(1, boneCount), Allocator.Persistent);
        localRot = new NativeArray<quaternion>(math.max(1, boneCount), Allocator.Persistent);
        worldRot = new NativeArray<quaternion>(math.max(1, boneCount), Allocator.Persistent);
        localScale = new NativeArray<float3>(math.max(1, boneCount), Allocator.Persistent);
        var transformList = new Transform[boneCount];

        int start = 0;
        for (int f = 0; f < n; f++)
        {
            FishProceduralAnimator a = layoutFishes[f];
            int count = a.BoneCount;
            ranges[f] = new int2(start, count);
            for (int k = 0; k < count; k++)
            {
                boneFish[start + k] = f;
                transformList[start + k] = a.BoneTransform(k);
            }

            a.SystemIndex = f;
            UploadBake(f, a);

            if (hadOld && !a.StateResetRequested && oldIndex.TryGetValue(a, out int old))
            {
                states[f] = oldStates[old];
                NativeArray<float4>.Copy(oldHistory, old * FishPoseJob.Hist, history, f * FishPoseJob.Hist, FishPoseJob.Hist);
            }
            else
            {
                ResetState(f, a);
            }

            start += count;
        }

        if (hadOld)
        {
            oldStates.Dispose();
            oldHistory.Dispose();
        }

        transforms = new TransformAccessArray(transformList);
        allocated = true;
    }

    /// Copy bone và tham số đã bake của một con vào mảng job (Parent đổi từ index trong cá sang index toàn cục).
    private void UploadBake(int f, FishProceduralAnimator a)
    {
        int2 range = ranges[f];
        FishBoneData[] baked = a.BakedBones;
        int count = math.min(range.y, baked.Length);
        for (int k = 0; k < count; k++)
        {
            FishBoneData b = baked[k];
            if (b.Parent >= 0) b.Parent += range.x;
            bones[range.x + k] = b;
        }

        prms[f] = a.BakedParams;
        a.BakeDirty = false;
    }

    private void ResetState(int f, FishProceduralAnimator a)
    {
        states[f] = a.InitialState();
        for (int k = 0; k < FishPoseJob.Hist; k++) history[f * FishPoseJob.Hist + k] = float4.zero;
        a.StateResetRequested = false;
    }

    private void DisposeExceptState()
    {
        if (!allocated) return;
        allocated = false;
        inputs.Dispose();
        prms.Dispose();
        ranges.Dispose();
        bones.Dispose();
        boneFish.Dispose();
        localRot.Dispose();
        worldRot.Dispose();
        localScale.Dispose();
        if (transforms.isCreated) transforms.Dispose();
    }

    private void Dispose()
    {
        if (!allocated) return;
        DisposeExceptState();
        states.Dispose();
        history.Dispose();
        layoutFishes = System.Array.Empty<FishProceduralAnimator>();
    }
}
