using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho cá ngựa có mào (SK_Fish34): đứng dọc như SK_Fish25 nên kế thừa SeahorseProceduralAnimator (thân cuộn chậm, ngả
/// về trước khi bơi, nhịp ổn định, vây bụng Fin_B_L/R rung nhẹ), khác rig cũ ở ba chỗ:
///  - mào trên đầu: cả Fin_*_T_F và Fin_*_T_B đều là con của Head (không có vây lưng trên lưng) → không rung theo nhịp vây mà lắc mềm trước sau
///    quanh trục ngang theo nhịp thân chậm, chóp trễ pha hơn gốc như tóc (crestSway / crestTipSway / crestLag);
///  - vây hai bên Fin_*_M_L/R là quạt ba tia (Fin_1_M_L tách Fin_2_M_L, Fin_2_1_M_L, Fin_2_2_M_L; Find Bones của FishRig tách mỗi tia một chuỗi):
///    đây là vây đẩy chính nên rung như vây lưng của cá ngựa thường (fanFlutter), tia nằm thấp trễ pha hơn tia nằm cao (fanRipple) → gợn chạy
///    từ trên xuống qua cả quạt;
///  - mõm Mouth_1 > Mouth_2 (con của Head): gật nhẹ quanh trục ngang theo nhịp thân như đang hút mồi (snoutNod), đốt chóp trễ pha.
public class CrestedSeahorseProceduralAnimator : SeahorseProceduralAnimator
{
    private class SnoutBone : BoneRef
    {
        public int chain;   // 0 = Mouth_1
    }

    [Header("Bone mõm (nút Find Bones điền cùng lúc với rig cá)")]
    [SerializeField] private BoneChain snout = new();

    [Header("Cá ngựa mào - mào trên đầu (Fin_*_T_*)")]
    [SerializeField] private float crestSway = 4f;        // độ lắc trước sau ở gốc mào theo nhịp thân
    [SerializeField] private float crestTipSway = 6f;     // độ ở các đốt sau (mào mềm, chóp lắc nhiều hơn)
    [SerializeField] private float crestLag = 0.8f;       // rad trễ pha mỗi đốt so với sóng thân

    [Header("Cá ngựa mào - quạt vây hai bên (Fin_*_M_*)")]
    [SerializeField] private float fanFlutter = 8f;       // độ quét ở gốc vây (đốt sau 0.7)
    [SerializeField] private float fanTwist = 4f;         // độ xoắn quanh trục gốc → chóp tia (đốt sau 0.5)
    [SerializeField] private float fanRipple = 0.8f;      // rad trễ pha của tia chĩa thẳng xuống so với tia chĩa thẳng lên (theo y của trục gốc → chóp tia)

    [Header("Cá ngựa mào - mõm (Mouth_*)")]
    [SerializeField] private float snoutNod = 2f;         // độ gật ở gốc mõm (đốt sau 0.6)
    [SerializeField] private float snoutLag = 0.6f;       // rad trễ pha mỗi đốt

#if UNITY_EDITOR
    public override void FindBones()
    {
        base.FindBones();
        Transform mouth = CreatureRigUtil.FindShallowest(transform, "Mouth");
        snout = new BoneChain { Segments = mouth != null ? CreatureRigUtil.Chain(mouth) : Array.Empty<Transform>() };
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"CrestedSeahorseProceduralAnimator '{name}': mõm {snout.Segments.Length} đốt.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        base.OnBuild(skinnedBones);
        if (snout?.Segments == null) return;
        for (int i = 0; i < snout.Segments.Length; i++)
            if (snout.Segments[i] != null) RegisterBone(new SnoutBone { t = snout.Segments[i], chain = i });
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        if (bone is SnoutBone s)
        {
            return new FishBoneData
            {
                Kind = (int)FishBoneKind.Wave, Along = 0f, Amp = snoutNod * (s.chain == 0 ? 1f : 0.6f),
                PhaseLag = (s.chain + 1) * snoutLag, Axis = 2, FinAxis = new float3(1f, 0f, 0f)
            };
        }

        FishBoneData d = base.BakeBone(bone);
        if (!TryGetFin(bone, out FinRole role, out int chain, out _, out Vector3 finAxis)) return d;
        switch (role)
        {
            case FinRole.Dorsal:   // mào
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = d.Along, Amp = chain == 0 ? crestSway : crestTipSway,
                    PhaseLag = (chain + 1) * crestLag, Axis = 2, FinAxis = new float3(1f, 0f, 0f)
                };
            case FinRole.Pectoral: // quạt ba tia
                d.Amp = fanFlutter * (chain == 0 ? 1f : 0.7f);
                d.TwistAmp = fanTwist * (chain == 0 ? 1f : 0.5f);
                d.PhaseLag += (1f - finAxis.y) * 0.5f * fanRipple;
                d.FoldScale = 0f;
                return d;
            default:
                return d;
        }
    }
}
