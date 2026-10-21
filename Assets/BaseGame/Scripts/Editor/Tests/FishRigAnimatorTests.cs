using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// Test animator procedural rig cá (2026-10-02, đợt cá / prop mới):
/// - Find Bones của FishRigProceduralAnimator nhận vây trước Fin_<đốt>_F_<bên> (cá búa SK_Fish31) là vây ngực, và vây nhiều tia
///   Fin_<đốt>_<tia>_<vị trí>_<bên> (cá ngựa SK_Fish34) thành chuỗi riêng, đốt đầu của tia đánh số tiếp đốt cha;
/// - FishPoseJob: bone có YawBendUp uốn khi rẽ quanh trục up dù sóng xoay quanh trục khác (cá heo);
/// - DolphinProceduralAnimator: đuôi quẫy lên xuống, không sang ngang;
/// - CrestedSeahorseProceduralAnimator: tìm mõm (Mouth_*), mào trên đầu là sóng mềm chứ không rung như vây lưng.
/// Rig dựng bằng GameObject HideAndDontSave nên không làm bẩn scene. Chạy qua menu Tools/FishSort/Run Fish Rig Tests hoặc Test Runner (EditMode).
public class FishRigAnimatorTests
{
    private readonly List<GameObject> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    // ---------- Dựng rig giả ----------

    private GameObject NewRoot<T>() where T : FishProceduralAnimator
    {
        GameObject root = EditorUtility.CreateGameObjectWithHideFlags("Rig", HideFlags.HideAndDontSave);
        created.Add(root);
        root.AddComponent<T>();
        return root;
    }

    /// Bone tại vị trí root space (root ở gốc toạ độ, không xoay).
    private static Transform Bone(Transform parent, string name, Vector3 position)
    {
        Transform t = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave).transform;
        t.SetParent(parent, false);
        t.position = position;
        return t;
    }

    /// SkinnedMeshRenderer không mesh, chỉ để Build có danh sách bone có skin (tránh cảnh báo "chưa gán skinnedMeshes").
    private static void AddSkin(GameObject root)
    {
        Transform holder = EditorUtility.CreateGameObjectWithHideFlags("Mesh", HideFlags.HideAndDontSave).transform;
        holder.SetParent(root.transform, false);
        SkinnedMeshRenderer smr = holder.gameObject.AddComponent<SkinnedMeshRenderer>();
        var bones = new List<Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root.transform && t != holder) bones.Add(t);
        smr.bones = bones.ToArray();
    }

    /// Cá nằm ngang: đầu +Z, đuôi -Z (như SK_Fish28 / SK_Fish31).
    private static Transform HorizontalBody(Transform root, out Transform tailEnd)
    {
        Transform rig = Bone(root, "Root", Vector3.zero);
        Transform spine1 = Bone(rig, "Spine_1", new Vector3(0f, 0.4f, 0.3f));
        Bone(spine1, "Head", new Vector3(0f, 0.4f, 0.3f));
        Transform spine2 = Bone(spine1, "Spine_2", new Vector3(0f, 0.38f, 0f));
        Transform spine3 = Bone(spine2, "Spine_3", new Vector3(0f, 0.34f, -0.3f));
        Transform tail1 = Bone(spine3, "Tail_1", new Vector3(0f, 0.3f, -0.6f));
        Transform tail2 = Bone(tail1, "Tail_2", new Vector3(0f, 0.28f, -0.8f));
        tailEnd = Bone(tail2, "Tail_2_end", new Vector3(0f, 0.26f, -1f));
        return spine1;
    }

    private static void FinPair(Transform parent, string position, Vector3 rootLeft, Vector3 tipLeft)
    {
        foreach (int side in new[] { -1, 1 })
        {
            string s = side < 0 ? "L" : "R";
            Vector3 mirror = side < 0 ? Vector3.one : new Vector3(-1f, 1f, 1f);   // trái giữ x âm như tham số, phải lật dấu
            Vector3 a = Vector3.Scale(rootLeft, mirror);
            Vector3 b = Vector3.Scale(tipLeft, mirror);
            Transform f1 = Bone(parent, $"Fin_1_{position}_{s}", a);
            Transform f2 = Bone(f1, $"Fin_2_{position}_{s}", Vector3.Lerp(a, b, 0.5f));
            Bone(f2, $"Fin_2_{position}_{s}_end", b);
        }
    }

    private static List<(int role, int side, string[] segments)> ReadFins(FishRigProceduralAnimator animator)
    {
        var list = new List<(int, int, string[])>();
        var so = new SerializedObject(animator);
        SerializedProperty fins = so.FindProperty("fins");
        for (int i = 0; i < fins.arraySize; i++)
        {
            SerializedProperty fin = fins.GetArrayElementAtIndex(i);
            SerializedProperty segments = fin.FindPropertyRelative("Segments");
            var names = new string[segments.arraySize];
            for (int k = 0; k < names.Length; k++)
                names[k] = segments.GetArrayElementAtIndex(k).objectReferenceValue is Transform t ? t.name : "null";
            list.Add((fin.FindPropertyRelative("Role").enumValueIndex, fin.FindPropertyRelative("Side").intValue, names));
        }
        return list;
    }

    private static int IndexOf(FishProceduralAnimator animator, string boneName)
    {
        for (int i = 0; i < animator.BoneCount; i++)
            if (animator.BoneTransform(i).name == boneName) return i;
        Assert.Fail($"bone '{boneName}' chưa được đăng ký");
        return -1;
    }

    // ---------- FishRigProceduralAnimator: tên vây ----------

    [Test]
    public void FindBones_FrontFins_ArePectoralPair()
    {
        GameObject root = NewRoot<FishRigProceduralAnimator>();
        Transform spine1 = HorizontalBody(root.transform, out _);
        FinPair(spine1, "F", new Vector3(-0.2f, 0.15f, 0.1f), new Vector3(-0.3f, 0.05f, 0.1f));
        AddSkin(root);
        var animator = root.GetComponent<FishRigProceduralAnimator>();

        animator.FindBones();

        var fins = ReadFins(animator);
        Assert.AreEqual(2, fins.Count, "số vây");
        Assert.AreEqual((int)FishRigProceduralAnimator.FinRole.Pectoral, fins[0].role);
        Assert.AreEqual((int)FishRigProceduralAnimator.FinRole.Pectoral, fins[1].role);
        Assert.AreEqual(-1, fins[0].side);
        Assert.AreEqual(1, fins[1].side);
        CollectionAssert.AreEqual(new[] { "Fin_1_F_L", "Fin_2_F_L" }, fins[0].segments);
    }

    [Test]
    public void FindBones_BranchedRays_BecomeSeparateChains()
    {
        GameObject root = NewRoot<FishRigProceduralAnimator>();
        Transform spine1 = HorizontalBody(root.transform, out _);
        Transform fin1 = Bone(spine1, "Fin_1_M_L", new Vector3(-0.1f, 0.4f, 0.2f));
        Transform main2 = Bone(fin1, "Fin_2_M_L", new Vector3(-0.15f, 0.4f, 0.15f));
        Bone(main2, "Fin_3_M_L", new Vector3(-0.2f, 0.4f, 0.1f));
        Transform ray1 = Bone(fin1, "Fin_2_1_M_L", new Vector3(-0.15f, 0.45f, 0.15f));
        Bone(ray1, "Fin_3_1_M_L", new Vector3(-0.2f, 0.5f, 0.1f));
        Transform ray2 = Bone(fin1, "Fin_2_2_M_L", new Vector3(-0.15f, 0.35f, 0.15f));
        Bone(ray2, "Fin_3_2_M_L", new Vector3(-0.2f, 0.3f, 0.1f));
        AddSkin(root);
        var animator = root.GetComponent<FishRigProceduralAnimator>();

        animator.FindBones();

        var fins = ReadFins(animator);
        Assert.AreEqual(3, fins.Count, "một chuỗi chính + hai tia");
        foreach (var fin in fins)
        {
            Assert.AreEqual((int)FishRigProceduralAnimator.FinRole.Pectoral, fin.role);
            Assert.AreEqual(-1, fin.side);
        }
        CollectionAssert.AreEqual(new[] { "Fin_1_M_L", "Fin_2_M_L", "Fin_3_M_L" }, fins[0].segments);
        CollectionAssert.AreEqual(new[] { "Fin_2_1_M_L", "Fin_3_1_M_L" }, fins[1].segments);
        CollectionAssert.AreEqual(new[] { "Fin_2_2_M_L", "Fin_3_2_M_L" }, fins[2].segments);
    }

    [Test]
    public void Build_RaySegments_ContinueTheChainIndexOfTheirParentFin()
    {
        GameObject root = NewRoot<FishRigProceduralAnimator>();
        Transform spine1 = HorizontalBody(root.transform, out _);
        Transform fin1 = Bone(spine1, "Fin_1_M_L", new Vector3(-0.1f, 0.4f, 0.2f));
        Transform main2 = Bone(fin1, "Fin_2_M_L", new Vector3(-0.15f, 0.4f, 0.15f));
        Bone(main2, "Fin_2_M_L_end", new Vector3(-0.2f, 0.4f, 0.1f));
        Transform ray1 = Bone(fin1, "Fin_2_1_M_L", new Vector3(-0.15f, 0.45f, 0.15f));
        Bone(ray1, "Fin_2_1_M_L_end", new Vector3(-0.2f, 0.5f, 0.1f));
        AddSkin(root);
        var animator = root.GetComponent<FishRigProceduralAnimator>();
        animator.FindBones();

        animator.Build();

        FishBoneData rootSegment = animator.BakedBones[IndexOf(animator, "Fin_1_M_L")];
        FishBoneData mainSecond = animator.BakedBones[IndexOf(animator, "Fin_2_M_L")];
        FishBoneData raySecond = animator.BakedBones[IndexOf(animator, "Fin_2_1_M_L")];
        Assert.AreNotEqual(rootSegment.Amp, raySecond.Amp, "đốt đầu của tia không phải gốc vây");
        Assert.AreEqual(mainSecond.Amp, raySecond.Amp, 1e-5f, "biên độ đốt 2 của tia = đốt 2 của chuỗi chính");
        Assert.AreEqual(mainSecond.PhaseLag, raySecond.PhaseLag, 1e-5f, "trễ pha đốt 2 của tia = đốt 2 của chuỗi chính");
    }

    // ---------- FishPoseJob: uốn khi rẽ quanh trục up ----------

    /// Chạy job một bone, root không xoay, lịch sử tốc độ rẽ cố định yawRate (độ/giây), pha thân 0 (sóng = 0).
    private static Quaternion PoseSingleBone(FishBoneData bone, float yawRate)
    {
        bone.Parent = -1;
        bone.StaticChain = quaternion.identity;
        bone.BindLocal = quaternion.identity;
        bone.BindScale = new float3(1f);
        var p = new FishParams
        {
            SwayFrequency = 1f, FinFrequency = 1f, WaveK = 0f, TailSign = -1f,
            UpAxis = new float3(0f, 1f, 0f), ForwardAxis = new float3(0f, 0f, 1f), LateralAxis = new float3(1f, 0f, 0f),
            TurnBend = 10f, PitchBend = 0f, BendDelay = 0f, BendSmoothing = 10f, MaxBendPerBone = 45f, IdleSpeed = 0.6f, SwimSpeed = 1f
        };

        var inputs = new NativeArray<FishFrameInput>(1, Allocator.TempJob);
        var prms = new NativeArray<FishParams>(1, Allocator.TempJob);
        var ranges = new NativeArray<int2>(1, Allocator.TempJob);
        var states = new NativeArray<FishSimState>(1, Allocator.TempJob);
        var hist = new NativeArray<float4>(FishPoseJob.Hist, Allocator.TempJob);
        var bones = new NativeArray<FishBoneData>(1, Allocator.TempJob);
        var local = new NativeArray<quaternion>(1, Allocator.TempJob);
        var world = new NativeArray<quaternion>(1, Allocator.TempJob);
        var scale = new NativeArray<float3>(1, Allocator.TempJob);
        try
        {
            inputs[0] = new FishFrameInput { RootRotation = quaternion.identity, Speed = 1f, Dt = 0f, Active = 1, PoseOnly = 1 };
            prms[0] = p;
            ranges[0] = new int2(0, 1);
            states[0] = new FishSimState { Clock = 1f, HistHead = 0, PrevRot = quaternion.identity };
            for (int i = 0; i < hist.Length; i++) hist[i] = new float4(0f, yawRate, 0f, 0f);
            bones[0] = bone;
            new FishPoseJob
            {
                Inputs = inputs, Params = prms, Ranges = ranges, Bones = bones, States = states,
                History = hist, LocalRot = local, WorldRot = world, LocalScale = scale
            }.Run(1);
            return local[0];
        }
        finally
        {
            inputs.Dispose(); prms.Dispose(); ranges.Dispose(); states.Dispose(); hist.Dispose();
            bones.Dispose(); local.Dispose(); world.Dispose(); scale.Dispose();
        }
    }

    private static FishBoneData LateralWaveBone(int yawBendUp) => new FishBoneData
    {
        Kind = (int)FishBoneKind.Wave, Along = 1f, Amp = 0f, BendScale = 1f, Axis = 2, FinAxis = new float3(1f, 0f, 0f), YawBendUp = yawBendUp
    };

    [Test]
    public void PoseJob_YawBendUp_BendsAroundUpWhileWaveAxisIsLateral()
    {
        Quaternion rot = PoseSingleBone(LateralWaveBone(1), 90f);

        Vector3 tail = rot * Vector3.back;
        Assert.Greater(Mathf.Abs(tail.x), 0.1f, "đuôi phải lệch sang ngang khi rẽ");
        Assert.Less(Mathf.Abs(tail.y), 1e-4f, "đuôi không được ngóc lên / chúi xuống khi chỉ rẽ");
    }

    [Test]
    public void PoseJob_WithoutYawBendUp_KeepsBendingAroundTheWaveAxis()
    {
        Quaternion rot = PoseSingleBone(LateralWaveBone(0), 90f);

        Vector3 tail = rot * Vector3.back;
        Assert.Less(Mathf.Abs(tail.x), 1e-4f, "hành vi cũ: uốn rẽ cộng vào trục sóng");
        Assert.Greater(Mathf.Abs(tail.y), 0.1f);
    }

    // ---------- Cá heo ----------

    /// Đoạn đường chóp đuôi đi qua giữa hai thời điểm của Evaluate (root space).
    private static Vector3 TailTravel(FishProceduralAnimator animator, Transform tailEnd, float t0, float t1)
    {
        animator.Evaluate(t0);
        Vector3 a = tailEnd.position;
        animator.Evaluate(t1);
        return tailEnd.position - a;
    }

    [Test]
    public void Dolphin_TailBeatsUpAndDown()
    {
        GameObject root = NewRoot<DolphinProceduralAnimator>();
        HorizontalBody(root.transform, out Transform tailEnd);
        AddSkin(root);
        var animator = root.GetComponent<DolphinProceduralAnimator>();
        animator.FindBones();
        animator.Build();

        Vector3 travel = TailTravel(animator, tailEnd, 0f, 0.15f);

        Assert.Greater(Mathf.Abs(travel.y), 0.02f, "đuôi cá heo phải quẫy lên xuống");
        Assert.Less(Mathf.Abs(travel.x), Mathf.Abs(travel.y) * 0.05f, "đuôi cá heo không quẫy sang ngang");
    }

    [Test]
    public void FishRig_TailBeatsSideways()
    {
        // Đối chứng: cùng rig với animator cá thường thì đuôi quẫy sang ngang.
        GameObject root = NewRoot<FishRigProceduralAnimator>();
        HorizontalBody(root.transform, out Transform tailEnd);
        AddSkin(root);
        var animator = root.GetComponent<FishRigProceduralAnimator>();
        animator.FindBones();
        animator.Build();

        Vector3 travel = TailTravel(animator, tailEnd, 0f, 0.15f);

        Assert.Greater(Mathf.Abs(travel.x), 0.02f);
        Assert.Less(Mathf.Abs(travel.y), Mathf.Abs(travel.x) * 0.05f);
    }

    // ---------- Cá ngựa mào ----------

    private static GameObject CrestedSeahorseRig(List<GameObject> created)
    {
        GameObject root = EditorUtility.CreateGameObjectWithHideFlags("Rig", HideFlags.HideAndDontSave);
        created.Add(root);
        root.AddComponent<CrestedSeahorseProceduralAnimator>();
        Transform rig = Bone(root.transform, "Root", Vector3.zero);
        Transform spine1 = Bone(rig, "Spine_1", new Vector3(0f, 0.5f, 0f));
        Transform head = Bone(spine1, "Head", new Vector3(0f, 0.5f, 0f));
        Transform crest1 = Bone(head, "Fin_1_T_F", new Vector3(0f, 0.85f, -0.08f));
        Transform crest2 = Bone(crest1, "Fin_2_T_F", new Vector3(0f, 0.97f, -0.15f));
        Bone(crest2, "Fin_2_T_F_end", new Vector3(0f, 1.06f, -0.2f));
        Transform mouth1 = Bone(head, "Mouth_1", new Vector3(0f, 0.58f, 0.27f));
        Transform mouth2 = Bone(mouth1, "Mouth_2", new Vector3(0f, 0.57f, 0.35f));
        Bone(mouth2, "Mouth_2_end", new Vector3(0f, 0.57f, 0.41f));
        Transform spine2 = Bone(spine1, "Spine_2", new Vector3(0f, 0.38f, 0.04f));
        Transform spine3 = Bone(spine2, "Spine_3", new Vector3(0f, 0.28f, 0f));
        Transform tail1 = Bone(spine3, "Tail_1", new Vector3(0f, 0.19f, -0.03f));
        Transform tail2 = Bone(tail1, "Tail_2", new Vector3(0f, 0.11f, -0.02f));
        Bone(tail2, "Tail_2_end", new Vector3(0f, 0.01f, -0.02f));
        AddSkin(root);
        return root;
    }

    [Test]
    public void CrestedSeahorse_FindBones_FindsTheSnout()
    {
        var animator = CrestedSeahorseRig(created).GetComponent<CrestedSeahorseProceduralAnimator>();

        animator.FindBones();
        animator.Build();

        Assert.GreaterOrEqual(IndexOf(animator, "Mouth_1"), 0);
        Assert.GreaterOrEqual(IndexOf(animator, "Mouth_2"), 0);
    }

    [Test]
    public void CrestedSeahorse_CrestIsSoftWaveNotFlutter()
    {
        var animator = CrestedSeahorseRig(created).GetComponent<CrestedSeahorseProceduralAnimator>();
        animator.FindBones();

        animator.Build();

        FishBoneData crestRoot = animator.BakedBones[IndexOf(animator, "Fin_1_T_F")];
        FishBoneData crestTip = animator.BakedBones[IndexOf(animator, "Fin_2_T_F")];
        Assert.AreEqual((int)FishBoneKind.Wave, crestRoot.Kind, "mào theo nhịp thân, không rung theo nhịp vây");
        Assert.Greater(Mathf.Abs(crestRoot.Amp), 0f, "mào phải lắc");
        Assert.Greater(crestTip.PhaseLag, crestRoot.PhaseLag, "chóp mào trễ pha hơn gốc");
    }
}
