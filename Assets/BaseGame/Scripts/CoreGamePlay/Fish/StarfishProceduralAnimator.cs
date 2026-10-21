using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// Animator procedural cho sao biển (SK_Fish23): Root > Spine_1 (tâm sao) > 5 tay Arm_* (Arm_1_L trên, Arm_1_1_L / Arm_1_1_R hai bên,
/// Arm_2_1_L / Arm_2_1_R dưới), mỗi tay 2 đốt; sao phẳng trong mặt XY của root space, mặt sao (mắt, miệng) hướng +Z.
/// Nút Find Bones điền body / arms theo tên + cây; sửa tay trong Inspector được.
/// Hướng: prefab Fish_23 đặt FishModel.Orientation = FaceCameraHeadFirst với FaceCameraLean = 0 → mặt sao luôn về camera, đứng thẳng, không
/// quay theo hướng đi (HeadFirstOrienter); vì thế tay không uốn theo yaw / pitch rẽ hướng.
/// Chuyển động mỗi frame: thân lắc quanh pháp tuyến mặt sao; mỗi tay uốn trong mặt sao (xoay quanh trục dọc), đốt chóp trễ pha, các tay
/// lệch pha theo góc quanh sao (ringWave vòng sóng) → gợn sóng chạy vòng quanh sao.
///  - IDLE: 1 Hz × Speed, biên độ × √Speed (bộ tham số không tiền tố swim, giữ như bản đầu).
///  - BƠI: nhịp ổn định (FishParams.SteadyRhythm) swimFrequency 0.6 Hz, biên độ đúng swimArmAngle / swimArmTipAngle, không theo Speed
///    (FishSchool báo Speed ≈ 2.4 khi bơi nên nếu theo Speed thì gợn chạy 2.4 Hz, "run" quá nhanh).
public class StarfishProceduralAnimator : FishProceduralAnimator
{
    private enum Kind { Body, Arm }

    private class Bone : BoneRef
    {
        public Kind kind;
        public int chain;   // đốt thứ mấy (0 = gốc)
        public float angle; // tay: góc quanh tâm sao (rad, 0 = phía +Y, tăng về +X)
    }

    [Header("Bone (nút Find Bones)")]
    [SerializeField, PropertyOrder(-10)] private Transform body;                                 // Spine_1
    [SerializeField, PropertyOrder(-10)] private BoneChain[] arms = Array.Empty<BoneChain>();

    [Header("Thân")]
    [SerializeField] private float waveFrequency = 1f;     // Hz idle khi Speed = 1 (idle thực ≈ 0.6 Hz)
    [SerializeField] private float bodyRock = 3f;          // độ lắc thân quanh trục dọc

    [Header("Tay (idle)")]
    [SerializeField] private float armAngle = 6f;          // độ uốn ở gốc tay
    [SerializeField] private float armTipAngle = 8f;       // độ ở các đốt sau
    [SerializeField] private float armLag = 0.9f;          // rad trễ pha mỗi đốt
    [SerializeField] private float ringWave = 1f;          // số vòng sóng quanh sao (0 = mọi tay đồng pha)

    [Header("Bơi")]
    [SerializeField] private float swimFrequency = 0.6f;   // Hz gợn sóng khi bơi (nhịp ổn định, không theo Speed)
    [SerializeField] private float swimArmAngle = 6f;      // độ uốn gốc tay khi bơi (không nhân √Speed)
    [SerializeField] private float swimArmTipAngle = 10f;  // độ ở các đốt sau khi bơi

#if UNITY_EDITOR
    /// Công cụ editor: body = Spine nông nhất, arms = mỗi con Arm_* của body theo chuỗi con đầu tiên.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        body = CreatureRigUtil.FindShallowest(transform, "Spine");
        var list = new List<BoneChain>();
        foreach (Transform root in CreatureRigUtil.Children(body, "Arm_"))
            list.Add(new BoneChain { Segments = CreatureRigUtil.Chain(root) });
        arms = list.ToArray();
        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"StarfishProceduralAnimator '{name}': thân {(body ? body.name : "không có")}, {arms.Length} tay.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (body == null)
        {
            Debug.LogWarning($"StarfishProceduralAnimator '{name}' chưa gán body (nút Find Bones trên prefab) nên không animate.", this);
            return;
        }

        Vector3 center = CreatureRigUtil.Local(transform, body);
        RegisterBone(new Bone { t = body, kind = Kind.Body });
        foreach (BoneChain arm in arms)
        {
            Transform first = CreatureRigUtil.First(arm?.Segments);
            if (first == null) continue;
            Vector3 radial = Vector3.ProjectOnPlane(CreatureRigUtil.Local(transform, first) - center, Vector3.forward);
            if (radial.sqrMagnitude < 1e-8f)
                radial = Vector3.ProjectOnPlane(CreatureRigUtil.Local(transform, CreatureRigUtil.Tip(CreatureRigUtil.Last(arm.Segments))) - center, Vector3.forward);
            if (radial.sqrMagnitude < 1e-8f) radial = Vector3.up;
            float angle = Mathf.Atan2(radial.x, radial.y);
            for (int i = 0; i < arm.Segments.Length; i++)
                if (arm.Segments[i] != null) RegisterBone(new Bone { t = arm.Segments[i], kind = Kind.Arm, chain = i, angle = angle });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        if (b.kind == Kind.Arm)
        {
            bool root = b.chain == 0;
            float lag = b.chain * armLag + ringWave * b.angle;
            return new FishBoneData
            {
                Kind = (int)FishBoneKind.Wave, Along = 0.5f, Axis = 1, BendScale = 0f, PitchBend = 0,   // mặt luôn về camera nên không uốn theo rẽ hướng
                Amp = root ? swimArmAngle : swimArmTipAngle, PhaseLag = lag,
                IdleVariant = 1, IdleAmp = root ? armAngle : armTipAngle, IdlePhaseLag = lag
            };
        }

        return new FishBoneData
        {
            Kind = (int)FishBoneKind.Wave, Along = 0f, Amp = bodyRock, PhaseLag = 0f, BendScale = 1f, PitchBend = 1, Axis = 1
        };
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = waveFrequency;
        p.FinFrequency = waveFrequency;
        p.WaveK = 0f;
        p.SteadyRhythm = 1;
        p.SwimSwayFrequency = swimFrequency;
        p.SwimFinFrequency = swimFrequency;
    }
}
