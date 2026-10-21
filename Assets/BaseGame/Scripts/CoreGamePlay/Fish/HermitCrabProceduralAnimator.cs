using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho cua ẩn sĩ (SK_Fish43): rig cá chuẩn (Spine_1..3 + Tail_1..2 xuyên qua vỏ ốc, Head = thân cua, Fin_1_T_F / Fin_1_T_B)
/// nên kế thừa FishRigProceduralAnimator cho Find Bones + sóng thân + vây, thêm hai nhóm bone là con của Spine_1:
///   càng Arm_1_M_<L|R> > Arm_2_M_<bên>; chân Leg_<cặp>_1_B_<L|R> > Leg_<cặp>_2_B_<bên> (3 cặp).
/// Khác cá: vỏ ốc gần như cứng nên biên độ sóng thân kế thừa bị nhân shellSwayScale (vỏ chỉ lắc nhẹ, không uốn chữ C).
/// Find Bones: chạy bản FishRig rồi tìm thêm càng / chân theo tên + cây. Bên trái / phải lúc Build lấy theo dấu x của bone.
/// Chuyển động thêm: chân đạp trước / sau quanh trục ngang, đốt sau trễ pha, cặp sau trễ pha cặp trước (sóng lan dọc thân),
/// trái / phải lệch pha; càng đung đưa sang bên quanh trục dọc lên, đốt chóp trễ pha, bị kéo theo khi rẽ.
public class HermitCrabProceduralAnimator : FishRigProceduralAnimator
{
    private enum HermitKind { Leg, Claw }

    private class HermitBone : BoneRef
    {
        public HermitKind kind;
        public int chain;  // đốt thứ mấy (0 = gốc)
        public int pair;   // chân: số thứ tự cặp từ 1
        public float side; // -1 trái / +1 phải
    }

    [Header("Bone cua ẩn sĩ (nút Find Bones điền cùng lúc với rig cá)")]
    [SerializeField] private SidedChain[] claws = Array.Empty<SidedChain>();
    [SerializeField] private SidedChain[] legs = Array.Empty<SidedChain>();

    [Header("Vỏ ốc")]
    [SerializeField] private float shellSwayScale = 0.3f;  // nhân vào biên độ sóng thân của FishRig (vỏ gần như cứng)

    [Header("Chân")]
    [SerializeField] private float legKick = 14f;          // độ đạp ở gốc chân (đốt sau = 0.7)
    [SerializeField] private float legLag = 0.7f;          // rad trễ pha mỗi đốt
    [SerializeField] private float pairLag = 1f;           // rad trễ pha mỗi cặp so với cặp trước
    [SerializeField] private float legSidePhase = 0.6f;    // rad lệch pha trái / phải
    [SerializeField] private float legBend = 0.2f;         // chân bị kéo theo hướng rẽ

    [Header("Càng")]
    [SerializeField] private float clawSway = 8f;          // độ đung đưa ở gốc càng (đốt sau = 0.8)
    [SerializeField] private float clawLag = 0.9f;         // rad trễ pha mỗi đốt
    [SerializeField] private float clawBend = 0.6f;        // càng bị kéo theo hướng rẽ

#if UNITY_EDITOR
    public override void FindBones()
    {
        base.FindBones();
        Transform spine = BodyRoot != null ? BodyRoot : CreatureRigUtil.FindShallowest(transform, "Spine");
        var clawList = new List<SidedChain>();
        foreach (Transform root in CreatureRigUtil.Children(spine, "Arm_"))
            clawList.Add(new SidedChain { Side = CreatureRigUtil.SideFromName(root), Segments = CreatureRigUtil.Chain(root) });
        claws = clawList.ToArray();

        var legList = new List<SidedChain>();
        foreach (Transform root in CreatureRigUtil.Children(spine, "Leg_"))
            legList.Add(new SidedChain { Side = CreatureRigUtil.SideFromName(root), Pair = ParsePair(root.name), Segments = CreatureRigUtil.Chain(root) });
        legList.Sort((a, b) => a.Pair != b.Pair ? a.Pair.CompareTo(b.Pair) : a.Side.CompareTo(b.Side));
        legs = legList.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"HermitCrabProceduralAnimator '{name}': {claws.Length} càng, {legs.Length} chân.", this);
    }

    /// Leg_<cặp>_... -> cặp; không đọc được thì 1.
    private static int ParsePair(string boneName)
    {
        string[] parts = boneName.Split('_');
        return parts.Length > 1 && int.TryParse(parts[1], out int pair) ? pair : 1;
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        base.OnBuild(skinnedBones);
        foreach (SidedChain c in claws)
        {
            Transform first = CreatureRigUtil.First(c?.Segments);
            if (first == null) continue;
            float side = CreatureRigUtil.SideSign(CreatureRigUtil.Local(transform, first), c.Side);
            for (int i = 0; i < c.Segments.Length; i++)
                if (c.Segments[i] != null) RegisterBone(new HermitBone { t = c.Segments[i], kind = HermitKind.Claw, chain = i, side = side });
        }

        foreach (SidedChain leg in legs)
        {
            Transform first = CreatureRigUtil.First(leg?.Segments);
            if (first == null) continue;
            float side = CreatureRigUtil.SideSign(CreatureRigUtil.Local(transform, first), leg.Side);
            for (int i = 0; i < leg.Segments.Length; i++)
                if (leg.Segments[i] != null) RegisterBone(new HermitBone { t = leg.Segments[i], kind = HermitKind.Leg, chain = i, pair = Mathf.Max(1, leg.Pair), side = side });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        if (bone is not HermitBone b)
        {
            FishBoneData d = base.BakeBone(bone);
            if (IsBodyBone(bone)) d.Amp *= shellSwayScale;   // vỏ ốc gần như cứng: chỉ lắc nhẹ, không uốn như thân cá
            return d;
        }

        if (b.kind == HermitKind.Leg)
        {
            return new FishBoneData
            {
                Kind = (int)FishBoneKind.Wave, Along = AlongOf(b.t), Amp = legKick * (b.chain == 0 ? 1f : 0.7f),
                PhaseLag = b.chain * legLag + (b.pair - 1) * pairLag + (b.side < 0f ? 0f : legSidePhase),
                BendScale = legBend, Axis = 2, FinAxis = new float3(1f, 0f, 0f)
            };
        }

        return new FishBoneData
        {
            Kind = (int)FishBoneKind.Wave, Along = 0f, Amp = clawSway * (b.chain == 0 ? 1f : 0.8f),
            PhaseLag = b.chain * clawLag + (b.side < 0f ? 0f : Mathf.PI), BendScale = clawBend, Axis = 0
        };
    }
}
