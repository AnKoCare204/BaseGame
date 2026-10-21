using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho bộ model cá Fishdom (Graphics/FishdomFish): nhận diện bone theo tên
/// (plv = plavnik = vây, side_plv = vây ngực, head/neck = đầu, còn lại = thân/đuôi) và tự đoán trục dài / hướng đầu từ vị trí bone.
/// Mỗi frame xoay bone so với bind pose:
///  - thân/đuôi: sóng chạy từ đầu tới đuôi (bước sóng ≈ 1 thân), biên độ + tần số theo Speed, cộng uốn theo đường bơi (lớp cha đo);
///  - vây ngực: quét tới lui + xoắn quanh trục gốc->chóp lệch pha 90° (đẩy nước), gợn lan ra chóp,
///    trái/phải so le, gập ra sau khi bơi nhanh, vẫy nhiều hơn khi bơi chậm;
///  - vây lưng/bụng/đuôi: rung theo sóng thân và uốn theo hướng rẽ.
/// Rig mới đặt tên xương theo quy ước riêng thì dùng class khác (FishRigProceduralAnimator); phần dùng chung ở FishProceduralAnimator.
public class FishdomProceduralAnimator : FishProceduralAnimator
{
    public enum SideFinAxis { Up, Forward } // Up: quét tới lui quanh trục dọc; Forward: vỗ lên xuống quanh trục thân (cá đuối)

    private enum Kind { Body, CenterFin, SideFin }

    private class Bone : BoneRef
    {
        public Kind kind;
        public float along;      // 0 = đầu, 1 = đuôi
        public int chain;        // đốt thứ mấy trong chuỗi vây (0 = gốc vây)
        public float side;       // -1 / +1 vây trái / phải
        public Vector3 finAxis;  // trục gốc -> chóp vây (local root), chỉ dùng cho vây ngực
    }

    [Header("Sóng thân / đuôi")]
    [SerializeField]
    private float swayFrequency = 1.8f;   // Hz khi Speed = 1
    [SerializeField] private float swayAngle = 9f;         // độ mỗi bone khi Speed = 1, nhân với profile; cộng dồn ra chóp đuôi
    [SerializeField] private float waveLength = 1.1f;      // theo chiều dài cá; ≈1 = đuôi trễ pha một chu kỳ, uốn chữ S như cá thật
    [SerializeField] private AnimationCurve swayProfile = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.5f, 0.3f), new Keyframe(1f, 1f)); // biên độ đầu(0) -> đuôi(1)

    [Header("Vây ngực")]
    [SerializeField]
    private SideFinAxis sideFinAxis = SideFinAxis.Up;
    [SerializeField] private float finFrequency = 2.2f;    // Hz khi Speed = 1 (nhanh hơn đuôi)
    [SerializeField] private float finAngle = 14f;         // quét tới lui ở gốc vây
    [SerializeField] private float finTwist = 18f;         // xoắn quanh trục gốc->chóp, lệch pha 90° so với quét
    [SerializeField] private float finChainAngle = 6f;     // gợn sóng lan ra các đốt sau
    [SerializeField] private float finChainLag = 0.9f;     // trễ pha (rad) giữa các đốt
    [SerializeField] private float finPhaseOffset = Mathf.PI; // lệch pha trái / phải (π = so le, 0 = đồng bộ)
    [SerializeField] private float finFold = 20f;          // gập vây ra sau khi Speed > 1 (độ tại Speed = 2)

    [Header("Vây lưng / bụng / đuôi")]
    [SerializeField]
    private float centerFinAngle = 4f;
    [SerializeField] private float centerFinChainLag = 0.6f;

    // Nhận diện bone theo tên (không phân biệt hoa thường). Các rig Fishdom: plv = plavnik = vây, veko = mí mắt.
    private static readonly string[] sideFinKeywords = { "side_plv", "wing", "leg", "shoulder", "elbow", "wrist", "arm" };
    private static readonly string[] finKeywords = { "plv", "fin", "ear", "moustache", "hair", "bow", "tongue" };
    private static readonly string[] headKeywords = { "head", "neck", "jaw" };

    private static readonly string[] ignoreKeywords =
    {
        "eye", "veko", "lid", "brow", "lip", "cheek", "cheec", "jaw", "teeth", "nose", "face", "beak", "mouth", // mặt
        "ctrl", "cntl", "cc_", "fcl", "fcrg", "group", "grp", "root", "joints", "mesh", "geometry", // control / group ("rig" bị trùng với "right")
        "blow", "hat", "bubble", "hip", "knee", "ankle", "toes"                                    // linh tinh
    };

    public SideFinAxis FinAxis { get => sideFinAxis; set => sideFinAxis = value; }

    private Vector3 upAxis = Vector3.up, lengthAxis = Vector3.forward; // local root
    private float tailSign = -1f;        // +1 nếu đuôi nằm phía +lengthAxis

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (skinnedBones.Count == 0) return;

        var list = new List<Transform>(skinnedBones);
        var pos = new Dictionary<Transform, Vector3>();
        foreach (var t in list) pos[t] = transform.InverseTransformPoint(t.position); // bind pose, local root

        var kindOf = new Dictionary<Transform, Kind>();
        foreach (var t in list) kindOf[t] = Classify(t.name);

        // trục dài: Y hay Z (trục ngang luôn là X vì cá đối xứng trái/phải)
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var t in list)
        {
            if (kindOf[t] != Kind.Body || IsIgnored(t.name)) continue;
            min = Vector3.Min(min, pos[t]); max = Vector3.Max(max, pos[t]);
        }
        if (float.IsInfinity(min.x)) foreach (var t in list) { min = Vector3.Min(min, pos[t]); max = Vector3.Max(max, pos[t]); }
        Vector3 ext = max - min;
        bool alongZ = ext.z >= ext.y;
        lengthAxis = alongZ ? Vector3.forward : Vector3.up;
        upAxis = alongZ ? Vector3.up : Vector3.forward;
        float lo = Vector3.Dot(min, lengthAxis), hi = Vector3.Dot(max, lengthAxis);
        if (hi - lo < 1e-5f) hi = lo + 1e-5f;

        // đầu nằm ở phía nào của trục dài
        float headSum = 0f; int headN = 0;
        foreach (var t in list) if (Match(t.name, headKeywords)) { headSum += Vector3.Dot(pos[t], lengthAxis); headN++; }
        bool headAtMin = headN == 0 || headSum / headN < (lo + hi) * 0.5f;
        tailSign = headAtMin ? 1f : -1f;

        // vây tên chung nhưng lệch hẳn sang một bên -> coi như vây ngực
        float width = Mathf.Max(ext.x, 1e-4f);
        foreach (var t in list)
            if (kindOf[t] == Kind.CenterFin && Mathf.Abs(pos[t].x) > width * 0.25f) kindOf[t] = Kind.SideFin;

        var bones = new List<Bone>();
        foreach (var t in list)
        {
            if (IsIgnored(t.name)) continue;
            float a = Mathf.InverseLerp(lo, hi, Vector3.Dot(pos[t], lengthAxis));
            var b = new Bone
            {
                t = t, kind = kindOf[t],
                along = headAtMin ? a : 1f - a,
                side = pos[t].x >= 0f ? 1f : -1f,
            };
            if (b.kind != Kind.Body)
            {
                var p = t.parent;
                while (p != null && kindOf.TryGetValue(p, out var pk) && pk == b.kind) { b.chain++; p = p.parent; }
            }
            RegisterBone(b);
            bones.Add(b);
        }

        // trục gốc->chóp cho từng vây ngực (chóp = đốt sâu nhất cùng chuỗi)
        foreach (var b in bones)
        {
            if (b.kind != Kind.SideFin || b.chain != 0) continue;
            Transform tip = b.t; int best = 0;
            foreach (var o in bones)
            {
                if (o.kind != Kind.SideFin || o.chain <= best) continue;
                var p = o.t; while (p != null && p != b.t) p = p.parent;
                if (p == b.t) { tip = o.t; best = o.chain; }
            }
            Vector3 axis = tip != b.t ? pos[tip] - pos[b.t] : new Vector3(b.side, 0f, 0f);
            b.finAxis = axis.sqrMagnitude > 1e-8f ? axis.normalized : new Vector3(b.side, 0f, 0f);
            foreach (var o in bones)
            {
                if (o.kind != Kind.SideFin || o.chain == 0) continue;
                var p = o.t; while (p != null && p != b.t) p = p.parent;
                if (p == b.t) o.finAxis = b.finAxis;
            }
        }
    }

    /// Bake bone thành dữ liệu job. Sóng thân / vây giữa (Wave): góc = Amp * sqrt(Speed) * sin(bodyPhase - k * along - PhaseLag) + uốn theo đường bơi;
    /// vây ngực (SideFin): quét tới lui quanh trục up (hoặc trục thân với cá đuối) + xoắn quanh trục gốc->chóp lệch pha 90°, trái/phải so le.
    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        switch (b.kind)
        {
            case Kind.SideFin:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.SideFin,
                    Along = b.along,
                    Amp = b.chain == 0 ? finAngle : finChainAngle,
                    TwistAmp = finTwist * (b.chain == 0 ? 1f : 0.5f),
                    PhaseLag = b.chain * finChainLag - (b.side < 0f ? finPhaseOffset : 0f),
                    Side = b.side,
                    FoldScale = 1f,
                    Axis = sideFinAxis == SideFinAxis.Forward ? 1 : 0,
                    FinAxis = b.finAxis
                };
            case Kind.CenterFin:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave,
                    Along = b.along,
                    Amp = centerFinAngle,
                    PhaseLag = b.chain * centerFinChainLag,
                    BendScale = 0.5f,
                    PitchBend = 0,
                    Axis = 0
                };
            default:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave,
                    Along = b.along,
                    Amp = swayAngle * swayProfile.Evaluate(b.along),
                    PhaseLag = 0f,
                    BendScale = Mathf.Lerp(0.6f, 1.2f, b.along),
                    PitchBend = 1,
                    Axis = 0
                };
        }
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = swayFrequency;
        p.FinFrequency = finFrequency;
        p.WaveK = Mathf.PI * 2f / Mathf.Max(waveLength, 0.05f);
        p.FinFold = finFold;
        p.TailSign = tailSign;
        p.UpAxis = upAxis;
        p.ForwardAxis = lengthAxis;
        p.LateralAxis = new float3(1f, 0f, 0f);
    }

    private Kind Classify(string n)
    {
        if (Match(n, sideFinKeywords)) return Kind.SideFin;
        if (Match(n, finKeywords)) return Kind.CenterFin;
        return Kind.Body;
    }

    private bool IsIgnored(string n) => Match(n, ignoreKeywords);

    private static bool Match(string n, string[] keys)
    {
        n = n.ToLowerInvariant();
        foreach (var k in keys) if (n.Contains(k)) return true;
        return false;
    }
}
