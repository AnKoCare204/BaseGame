using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho cua (SK_Fish24): Root > Spine_1 > Head; Eye_1_<L|R> > Eye_2 (cuống mắt); Arm_<L|R> > Claw > ClawUpper_1..2 /
/// ClawLower_1..2 (càng); Leg_1_<F|B>_<L|R> > Leg_2 > Leg_3 (4 chân 3 đốt; chóp chân sau trùng tên Leg_2_B_* nên duyệt theo cây).
/// Root space: mặt +Z, lưng +Y, phải +X (prefab xoay con FBX cho khớp); bên trái / phải lúc Build lấy theo dấu x của bone.
/// Nút Find Bones điền theo tên + cây; sửa tay trong Inspector được.
/// Chuyển động mỗi frame (xoay so với bind pose):
///  - thân lắc nhẹ quanh trục dọc lên + nghiêng theo hướng rẽ; đầu gật nhẹ;
///  - cuống mắt nghiêng sang bên quanh trục dọc thân, hai mắt ngược chiều, đốt chóp trễ pha;
///  - càng: cả cánh tay nhấc lên xuống quanh trục vuông góc tay, hai bên ngược pha; càng trên / dưới mở khép ngược chiều quanh bản lề
///    (pháp tuyến mặt phẳng hai nửa càng), đốt chóp trễ pha;
///  - chân đạp lên xuống quanh trục nằm ngang vuông góc chân, đốt sau trễ pha, chân trước / sau ngược pha, trái / phải lệch 1/4 chu kỳ.
public class CrabProceduralAnimator : FishProceduralAnimator
{
    [Serializable]
    public class ClawChain
    {
        [Range(-1, 1)] public int Side;
        public Transform Arm;
        public Transform Claw;
        public Transform[] Upper = Array.Empty<Transform>();
        public Transform[] Lower = Array.Empty<Transform>();
    }

    private enum Kind { Body, Head, Eye, Arm, ClawUpper, ClawLower, Leg }

    private class Bone : BoneRef
    {
        public Kind kind;
        public int chain;    // đốt thứ mấy (0 = gốc)
        public float side;   // -1 trái / +1 phải
        public bool front;   // chân trước
        public Vector3 axis; // trục xoay root space (tay, càng, chân)
    }

    [Header("Bone (nút Find Bones)")]
    [SerializeField, PropertyOrder(-10)] private Transform body;                                    // Spine_1
    [SerializeField, PropertyOrder(-10)] private Transform head;
    [SerializeField, PropertyOrder(-10)] private SidedChain[] eyes = Array.Empty<SidedChain>();
    [SerializeField, PropertyOrder(-10)] private ClawChain[] claws = Array.Empty<ClawChain>();
    [SerializeField, PropertyOrder(-10)] private SidedChain[] legs = Array.Empty<SidedChain>();

    [Header("Thân / đầu / mắt")]
    [SerializeField] private float kickFrequency = 1.6f;   // Hz khi Speed = 1
    [SerializeField] private float bodySway = 2f;          // độ lắc thân quanh trục dọc lên
    [SerializeField] private float headNod = 2f;           // độ gật đầu quanh trục ngang
    [SerializeField] private float eyeWobble = 5f;         // độ nghiêng cuống mắt (gốc), hai mắt ngược chiều
    [SerializeField] private float eyeLag = 0.7f;          // rad trễ pha đốt chóp mắt

    [Header("Càng")]
    [SerializeField] private float armLift = 6f;           // độ nhấc cánh tay lên xuống
    [SerializeField] private float clawOpen = 10f;         // độ mở / khép mỗi nửa càng
    [SerializeField] private float clawLag = 0.6f;         // rad trễ pha đốt chóp càng

    [Header("Chân")]
    [SerializeField] private float legKick = 12f;          // độ đạp ở gốc chân (đốt sau giảm dần)
    [SerializeField] private float legLag = 0.7f;          // rad trễ pha mỗi đốt
    [SerializeField] private float legSidePhase = Mathf.PI * 0.5f;  // rad lệch pha trái / phải
    [SerializeField] private float legFrontBackPhase = Mathf.PI;    // rad lệch pha chân trước / sau
    [SerializeField] private float legBend = 0.2f;         // chân bị kéo theo hướng rẽ

#if UNITY_EDITOR
    /// Công cụ editor: body = Spine nông nhất; Head, Eye_*, Arm_* (> Claw > ClawUpper / ClawLower), Leg_* là con của body; chuỗi theo con đầu tiên.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        body = CreatureRigUtil.FindShallowest(transform, "Spine");
        head = CreatureRigUtil.Child(body, "Head");

        var eyeList = new List<SidedChain>();
        foreach (Transform root in CreatureRigUtil.Children(body, "Eye_"))
            eyeList.Add(new SidedChain { Side = CreatureRigUtil.SideFromName(root), Segments = CreatureRigUtil.Chain(root) });
        eyes = eyeList.ToArray();

        var clawList = new List<ClawChain>();
        foreach (Transform arm in CreatureRigUtil.Children(body, "Arm_"))
        {
            Transform claw = CreatureRigUtil.Child(arm, "Claw");
            Transform upper = CreatureRigUtil.Child(claw, "ClawUpper");
            Transform lower = CreatureRigUtil.Child(claw, "ClawLower");
            if (claw == null || upper == null || lower == null)
                Debug.LogWarning($"CrabProceduralAnimator '{name}': càng '{arm.name}' thiếu Claw / ClawUpper / ClawLower.", arm);
            clawList.Add(new ClawChain
            {
                Side = CreatureRigUtil.SideFromName(arm), Arm = arm, Claw = claw,
                Upper = upper != null ? CreatureRigUtil.Chain(upper) : Array.Empty<Transform>(),
                Lower = lower != null ? CreatureRigUtil.Chain(lower) : Array.Empty<Transform>(),
            });
        }
        claws = clawList.ToArray();

        var legList = new List<SidedChain>();
        foreach (Transform root in CreatureRigUtil.Children(body, "Leg_"))
            legList.Add(new SidedChain { Side = CreatureRigUtil.SideFromName(root), Front = CreatureRigUtil.NameHas(root, "_F_"), Segments = CreatureRigUtil.Chain(root) });
        legs = legList.ToArray();

        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"CrabProceduralAnimator '{name}': thân {(body ? body.name : "không có")}, {eyes.Length} mắt, {claws.Length} càng, {legs.Length} chân.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (body == null)
        {
            Debug.LogWarning($"CrabProceduralAnimator '{name}' chưa gán body (nút Find Bones trên prefab) nên không animate.", this);
            return;
        }

        RegisterBone(new Bone { t = body, kind = Kind.Body });
        if (head != null) RegisterBone(new Bone { t = head, kind = Kind.Head });

        foreach (SidedChain eye in eyes)
        {
            Transform first = CreatureRigUtil.First(eye?.Segments);
            if (first == null) continue;
            float side = CreatureRigUtil.SideSign(CreatureRigUtil.Local(transform, first), eye.Side);
            for (int i = 0; i < eye.Segments.Length; i++)
                if (eye.Segments[i] != null) RegisterBone(new Bone { t = eye.Segments[i], kind = Kind.Eye, chain = i, side = side });
        }

        foreach (ClawChain c in claws)
        {
            if (c == null || c.Arm == null) continue;
            Vector3 armPos = CreatureRigUtil.Local(transform, c.Arm);
            float side = CreatureRigUtil.SideSign(armPos, c.Side);
            Transform reach = c.Claw != null ? c.Claw : CreatureRigUtil.First(c.Upper);
            Vector3 armDir = reach != null ? CreatureRigUtil.Dir(transform, c.Arm, reach, Vector3.forward) : Vector3.forward;
            Vector3 liftAxis = CreatureRigUtil.SwingAxis(armDir, Vector3.right);
            RegisterBone(new Bone { t = c.Arm, kind = Kind.Arm, chain = 0, side = side, axis = liftAxis });
            if (c.Claw != null) RegisterBone(new Bone { t = c.Claw, kind = Kind.Arm, chain = 1, side = side, axis = liftAxis });

            Transform upper = CreatureRigUtil.First(c.Upper), lower = CreatureRigUtil.First(c.Lower);
            Transform pivot = c.Claw != null ? c.Claw : c.Arm;
            Vector3 hinge = Vector3.right;
            if (upper != null && lower != null)
            {
                Vector3 u = CreatureRigUtil.Dir(transform, pivot, upper, Vector3.forward);
                Vector3 l = CreatureRigUtil.Dir(transform, pivot, lower, Vector3.forward);
                Vector3 h = Vector3.Cross(u, l);
                if (h.sqrMagnitude > 1e-8f) hinge = h.normalized;
            }
            for (int i = 0; i < c.Upper.Length; i++)
                if (c.Upper[i] != null) RegisterBone(new Bone { t = c.Upper[i], kind = Kind.ClawUpper, chain = i, side = side, axis = hinge });
            for (int i = 0; i < c.Lower.Length; i++)
                if (c.Lower[i] != null) RegisterBone(new Bone { t = c.Lower[i], kind = Kind.ClawLower, chain = i, side = side, axis = hinge });
        }

        foreach (SidedChain leg in legs)
        {
            Transform first = CreatureRigUtil.First(leg?.Segments);
            if (first == null) continue;
            Vector3 rootPos = CreatureRigUtil.Local(transform, first);
            float side = CreatureRigUtil.SideSign(rootPos, leg.Side);
            Transform next = leg.Segments.Length > 1 && leg.Segments[1] != null ? leg.Segments[1] : CreatureRigUtil.Tip(CreatureRigUtil.Last(leg.Segments));
            Vector3 legDir = CreatureRigUtil.Dir(transform, first, next, new Vector3(side, 0f, 0f));
            Vector3 kickAxis = CreatureRigUtil.SwingAxis(legDir, Vector3.forward);
            for (int i = 0; i < leg.Segments.Length; i++)
                if (leg.Segments[i] != null) RegisterBone(new Bone { t = leg.Segments[i], kind = Kind.Leg, chain = i, side = side, front = leg.Front, axis = kickAxis });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        switch (b.kind)
        {
            case Kind.Head:
                return new FishBoneData { Kind = (int)FishBoneKind.Wave, Amp = headNod, BendScale = 0.3f, Axis = 2, FinAxis = new float3(1f, 0f, 0f) };
            case Kind.Eye:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Amp = eyeWobble * b.side * (b.chain == 0 ? 1f : 0.7f), PhaseLag = b.chain * eyeLag, Axis = 1
                };
            case Kind.Arm:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Amp = armLift * (b.chain == 0 ? 1f : 0.5f), PhaseLag = b.chain * 0.5f + (b.side < 0f ? 0f : Mathf.PI),
                    BendScale = 0.3f, Axis = 2, FinAxis = b.axis
                };
            case Kind.ClawUpper:
            case Kind.ClawLower:
            {
                float sign = b.kind == Kind.ClawUpper ? 1f : -1f;
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Amp = sign * clawOpen * (b.chain == 0 ? 1f : 0.5f),
                    PhaseLag = b.chain * clawLag + (b.side < 0f ? 0f : Mathf.PI * 0.5f), Axis = 2, FinAxis = b.axis
                };
            }
            case Kind.Leg:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = 0.5f, Amp = legKick * Mathf.Max(0.2f, 1f - 0.2f * b.chain),
                    PhaseLag = b.chain * legLag + (b.front ? 0f : legFrontBackPhase) + (b.side < 0f ? 0f : legSidePhase),
                    BendScale = legBend, Axis = 2, FinAxis = b.axis
                };
            default:
                return new FishBoneData { Kind = (int)FishBoneKind.Wave, Amp = bodySway, BendScale = 1f, PitchBend = 1, Axis = 0 };
        }
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = kickFrequency;
        p.FinFrequency = kickFrequency;
        p.WaveK = 0f;
    }
}
