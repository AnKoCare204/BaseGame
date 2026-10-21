using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho rùa (SK_Fish27): Root > Spine_1 > Neck_2..4 > Head (cổ), Shell (mai), Spine_2 > Spine_3 > Tail (đuôi),
/// Leg_1_<F|B>_<L|R> > Leg_2 > Leg_3 (chân chèo; chóp chân sau trùng tên Leg_2_B_* nên duyệt theo cây).
/// Root space: đầu +Z, lưng +Y, phải +X (prefab xoay con FBX cho khớp); bên trái / phải lúc Build lấy theo dấu x của bone.
/// Nút Find Bones điền theo tên + cây; sửa tay trong Inspector được.
/// Hai chế độ blend theo Speed (FishParams.IdleSpeed / SwimSpeed, lớp cha làm mượt theo thời gian); trục xoay, xoắn, đồng hồ pha chung cho cả hai:
///  - IDLE (lơ lửng trong bóng): chân chèo trước vẫy lên xuống kiểu vây (SideFin quét quanh trục dọc thân + xoắn quanh trục gốc→chóp lệch pha 90°),
///    hai bên đồng pha như rùa thật, đốt sau trễ pha; chân sau bằng nửa và lệch pha; cổ gật nhẹ quanh trục ngang; đuôi vẫy nhẹ; thân lắc rất nhẹ;
///    mai gần như cứng. (Bộ tham số không có tiền tố swim.)
///  - BƠI: nhịp ổn định (FishParams.SteadyRhythm): cả pha thân lẫn pha vây chạy đúng swimFrequency và biên độ đúng tham số, không theo Speed
///    (FishSchool báo Speed ≈ 2.4 khi bơi: nếu theo Speed thì cổ gật gấp đôi ở 2 Hz còn chân bị luật vây cá bóp còn 40 %). Chân trước quạt
///    cánh rộng hơn, tâm quạt hạ dưới ngang, quạt xuống nhanh / nâng lên chậm (méo pha) để có cú đẩy rõ; chân sau đạp ngay sau cú quạt
///    trước; cổ gật nhẹ như idle và ngẩng đầu. Không gập chân khi bơi nhanh. (Bộ tham số swim*.)
public class TurtleProceduralAnimator : FishProceduralAnimator
{
    private enum Kind { Body, Shell, Neck, Tail, Flipper }

    private class Bone : BoneRef
    {
        public Kind kind;
        public int chain;      // đốt thứ mấy (0 = gốc)
        public int count;      // số đốt trong chuỗi (đuôi: tăng dần biên độ)
        public float side;     // -1 trái / +1 phải
        public bool front;
        public Vector3 finAxis; // chân chèo: gốc -> chóp (root space)
    }

    [Header("Bone (nút Find Bones)")]
    [SerializeField, PropertyOrder(-10)] private Transform body;                                   // Spine_1
    [SerializeField, PropertyOrder(-10)] private Transform shell;
    [SerializeField, PropertyOrder(-10)] private Transform[] neck = Array.Empty<Transform>();      // Neck_2..N rồi Head
    [SerializeField, PropertyOrder(-10)] private Transform[] tail = Array.Empty<Transform>();      // Spine_2..N rồi Tail
    [SerializeField, PropertyOrder(-10)] private SidedChain[] flippers = Array.Empty<SidedChain>();

    [Header("Thân / mai / cổ / đuôi")]
    [SerializeField] private float bodyFrequency = 0.9f;   // Hz sóng thân khi Speed = 1 (cổ, đuôi, thân)
    [SerializeField] private float bodySway = 1.5f;        // độ lắc thân quanh trục dọc lên
    [SerializeField] private float shellRoll = 0.5f;       // độ nghiêng mai quanh trục dọc thân
    [SerializeField] private float neckNod = 3f;           // độ gật cổ mỗi đốt quanh trục ngang (idle)
    [SerializeField] private float neckLag = 0.5f;         // rad trễ pha mỗi đốt cổ
    [SerializeField] private float neckBend = 0.8f;        // cổ dẫn hướng rẽ
    [SerializeField] private float tailSway = 4f;          // độ vẫy đuôi tại chóp
    [SerializeField] private float tailLag = 0.5f;         // rad trễ pha mỗi đốt đuôi

    [Header("Nhịp bơi")]
    [SerializeField] private float swimFrequency = 0.7f;   // Hz khi bơi cho cả cổ / đuôi / thân lẫn chân chèo (một nhịp chung, không theo Speed)

    [Header("Cổ - bơi")]
    [SerializeField] private float swimNeckNod = 2.5f;     // độ gật mỗi đốt khi bơi, không nhân sqrt(Speed) (idle nhìn thấy = neckNod × √0.6 ≈ 2.3°)
    [SerializeField] private float swimNeckLift = 2f;      // độ ngẩng mỗi đốt khi bơi (đầu ngẩng ≈ số đốt × giá trị này)

    [Header("Chân chèo (idle)")]
    [SerializeField] private float strokeFrequency = 0.7f; // Hz khi Speed = 1 (pha vây, chung cho idle / bơi)
    [SerializeField] private float flipperAngle = 22f;     // độ quét chân trước ở gốc lúc idle (đốt sau = 0.6)
    [SerializeField] private float flipperTwist = 15f;     // độ xoắn quanh trục gốc→chóp (không blend: chung cho idle / bơi)
    [SerializeField] private float flipperLag = 0.6f;      // rad trễ pha mỗi đốt
    [SerializeField] private float flipperPhaseOffset = 0f; // rad lệch pha trái / phải (0 = đồng pha như rùa thật)
    [SerializeField] private float backScale = 0.5f;       // idle: chân sau = tỉ lệ này của chân trước
    [SerializeField] private float backPhaseOffset = 1.5f; // idle: rad lệch pha chân sau so với chân trước

    [Header("Chân chèo (bơi)")]
    [SerializeField] private float swimFlipperAngle = 30f; // độ quét gốc chân trước khi bơi
    [SerializeField] private float swimFlipperDrop = 8f;   // độ hạ tâm quạt gốc chân trước xuống dưới ngang khi bơi
    [SerializeField] private float swimStrokeSkew = 0.4f;  // méo pha chân trước: quạt xuống nhanh, nâng lên chậm (0 = sin đều)
    [SerializeField] private float swimBackScale = 0.6f;   // bơi: chân sau = tỉ lệ này của chân trước
    [SerializeField] private float swimBackPhaseOffset = 0.8f; // bơi: rad lệch pha chân sau (đạp ngay sau cú quạt trước)
    [SerializeField] private float swimBackSkew = 0.3f;    // méo pha chân sau: đạp nhanh, thu chậm

#if UNITY_EDITOR
    /// Công cụ editor: body = Spine nông nhất; Neck_* / Shell / Spine_* (đuôi) / Leg_* là con của body; chuỗi theo con đầu tiên.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        body = CreatureRigUtil.FindShallowest(transform, "Spine");
        shell = CreatureRigUtil.Child(body, "Shell");
        Transform neckRoot = CreatureRigUtil.Child(body, "Neck");
        neck = neckRoot != null ? CreatureRigUtil.Chain(neckRoot) : Array.Empty<Transform>();
        Transform tailRoot = CreatureRigUtil.Child(body, "Spine");
        if (tailRoot == null) tailRoot = CreatureRigUtil.Child(body, "Tail");
        tail = tailRoot != null ? CreatureRigUtil.Chain(tailRoot) : Array.Empty<Transform>();

        var list = new List<SidedChain>();
        foreach (Transform root in CreatureRigUtil.Children(body, "Leg_"))
            list.Add(new SidedChain { Side = CreatureRigUtil.SideFromName(root), Front = CreatureRigUtil.NameHas(root, "_F_"), Segments = CreatureRigUtil.Chain(root) });
        flippers = list.ToArray();

        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"TurtleProceduralAnimator '{name}': thân {(body ? body.name : "không có")}, cổ {neck.Length} đốt, đuôi {tail.Length} đốt, {flippers.Length} chân chèo.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (body == null)
        {
            Debug.LogWarning($"TurtleProceduralAnimator '{name}' chưa gán body (nút Find Bones trên prefab) nên không animate.", this);
            return;
        }

        RegisterBone(new Bone { t = body, kind = Kind.Body });
        if (shell != null) RegisterBone(new Bone { t = shell, kind = Kind.Shell });
        for (int i = 0; i < neck.Length; i++)
            if (neck[i] != null) RegisterBone(new Bone { t = neck[i], kind = Kind.Neck, chain = i, count = neck.Length });
        for (int i = 0; i < tail.Length; i++)
            if (tail[i] != null) RegisterBone(new Bone { t = tail[i], kind = Kind.Tail, chain = i, count = tail.Length });

        foreach (SidedChain f in flippers)
        {
            Transform first = CreatureRigUtil.First(f?.Segments);
            if (first == null) continue;
            Vector3 rootPos = CreatureRigUtil.Local(transform, first);
            float side = CreatureRigUtil.SideSign(rootPos, f.Side);
            Vector3 axis = CreatureRigUtil.Dir(transform, first, CreatureRigUtil.Tip(CreatureRigUtil.Last(f.Segments)), new Vector3(side, 0f, 0f));
            for (int i = 0; i < f.Segments.Length; i++)
                if (f.Segments[i] != null) RegisterBone(new Bone { t = f.Segments[i], kind = Kind.Flipper, chain = i, side = side, front = f.Front, finAxis = axis });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        switch (b.kind)
        {
            case Kind.Shell:
                return new FishBoneData { Kind = (int)FishBoneKind.Wave, Amp = shellRoll, Axis = 1 };
            case Kind.Neck:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Amp = swimNeckNod, Bias = -swimNeckLift, PhaseLag = b.chain * neckLag,   // quay âm quanh +X = ngẩng lên
                    BendScale = neckBend, PitchBend = 1, Axis = 2, FinAxis = new float3(1f, 0f, 0f),
                    IdleVariant = 1, IdleAmp = neckNod, IdleBias = 0f, IdlePhaseLag = b.chain * neckLag
                };
            case Kind.Tail:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = 1f, Amp = tailSway * (b.chain + 1) / Mathf.Max(1, b.count), PhaseLag = b.chain * tailLag,
                    BendScale = 1f, PitchBend = 1, Axis = 0
                };
            case Kind.Flipper:
            {
                bool root = b.chain == 0;
                float seg = root ? 1f : 0.6f;
                float idleScale = b.front ? 1f : backScale;
                float swimScale = b.front ? 1f : swimBackScale;
                float sideLag = b.side < 0f ? 0f : flipperPhaseOffset;
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.SideFin,
                    Along = 0.5f,
                    Amp = swimFlipperAngle * swimScale * seg,
                    Bias = b.front && root ? -swimFlipperDrop : 0f,                       // quét dương = nâng lên, nên hạ tâm = bias âm; chỉ ở gốc
                    PhaseSkew = -(b.front ? swimStrokeSkew : swimBackSkew),               // skew âm = nửa đi xuống nhanh, nửa đi lên chậm
                    PhaseLag = b.chain * flipperLag + sideLag + (b.front ? 0f : swimBackPhaseOffset),
                    TwistAmp = flipperTwist * idleScale * (root ? 1f : 0.5f),
                    IdleVariant = 1,
                    IdleAmp = flipperAngle * idleScale * seg,
                    IdleBias = 0f,
                    IdlePhaseSkew = 0f,
                    IdlePhaseLag = b.chain * flipperLag + sideLag + (b.front ? 0f : backPhaseOffset),
                    Side = b.side,
                    FoldScale = 0f,
                    Axis = 1,
                    FinAxis = b.finAxis
                };
            }
            default:
                return new FishBoneData { Kind = (int)FishBoneKind.Wave, Amp = bodySway, BendScale = 1f, PitchBend = 1, Axis = 0 };
        }
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = bodyFrequency;
        p.FinFrequency = strokeFrequency;
        p.WaveK = 0f;
        p.FinFold = 0f;
        p.SteadyRhythm = 1;
        p.SwimSwayFrequency = swimFrequency;
        p.SwimFinFrequency = swimFrequency;
    }
}
