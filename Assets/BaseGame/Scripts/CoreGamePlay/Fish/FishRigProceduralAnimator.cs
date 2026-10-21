using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho rig cá mới (Graphics/Models/Fish/Fish_Rig.fbx và các cá cùng quy ước đặt tên xương):
///   Root > Spine1..N > Tail1..N: chuỗi thân đầu -> đuôi; Head: con của Spine1;
///   Fin_<vị trí>_<bên>_<đốt>: T_F / T_B = vây lưng trước / sau, B_M = vây hậu môn, B_L / B_R = vây bụng, M_L / M_R = vây ngực;
///   bone *_end là chóp, không có skin, bỏ qua.
///   Biến thể Fish_1.fbx cũng được nhận: Spine_01..N (không có Tail), Fin_<vị trí>_<đốt>_<bên>, Fin_<đốt>_B = vây hậu môn.
///   Bộ SK_Fish* (Graphics/Models/FishFbx): Spine_1..N, Tail_1..N, Fin_<đốt>_<vị trí>_<bên> (Fin_1_M_L), Fin_<đốt>_B = vây hậu môn.
///   Vị trí F = vây trước hai bên, cũng là vây ngực (cá búa SK_Fish31: Fin_1_F_L). Vây nhiều tia (cá ngựa SK_Fish34): Fin_<đốt>_<tia>_<vị trí>_<bên>
///   (Fin_2_1_M_L > Fin_3_1_M_L tách từ Fin_1_M_L) thành chuỗi riêng, đốt đầu của tia đánh số tiếp đốt cha (biên độ / trễ pha như đốt 2).
///   Bone khác tên quy ước (Eyes_*, Mouth_*) bỏ qua, đi theo bone cha; class con muốn animate thì tự tìm.
/// Trục trong root space: đầu +Z, lưng +Y, phải +X (khớp quy ước FishSwimmer nên prefab không cần xoay model).
/// Class con: HermitCrabProceduralAnimator (thêm chân / càng, giảm sóng thân), SeahorseProceduralAnimator (đứng dọc, đổi trục qua hook AlongCoord / BodyAxis / CenterFinAxis),
/// DolphinProceduralAnimator (quẫy lên xuống), CrestedSeahorseProceduralAnimator (cá ngựa có mào + mõm).
/// Gán bone: nút Find Bones (tìm theo quy ước tên, kèm gom SkinnedMeshRenderer nếu chưa có); sửa tay trong Inspector được.
/// Chuyển động mỗi frame (xoay so với bind pose):
///  - thân: sóng đầu -> đuôi theo swayProfile + uốn theo đường bơi (lớp cha đo); đầu lắc ngược pha nhẹ để mắt cá ổn định;
///  - vây ngực: quét tới lui quanh trục dọc + xoắn quanh trục gốc->chóp lệch pha 90°, đốt sau trễ pha, trái/phải so le,
///    gập ra sau khi bơi nhanh, vẫy nhiều hơn khi bơi chậm;
///  - vây bụng: như vây ngực, biên độ nhỏ hơn và lệch pha riêng;
///  - vây lưng / hậu môn: nghiêng sang hai bên quanh trục thân theo sóng thân tại gốc vây, đốt sau trễ pha.
public class FishRigProceduralAnimator : FishProceduralAnimator
{
    public enum FinRole { Pectoral, Pelvic, Dorsal, Anal } // vây ngực, vây bụng, vây lưng, vây hậu môn

    [Serializable]
    public class FinChain
    {
        public FinRole Role;
        [Range(-1, 1)] public int Side;                         // -1 trái, +1 phải, 0 giữa
        public Transform[] Segments = Array.Empty<Transform>(); // gốc -> chóp
    }

    private enum Kind { Body, Head, SideFin, CenterFin }

    private class Bone : BoneRef
    {
        public Kind kind;
        public FinRole role;
        public float along;      // 0 = gốc chuỗi thân (Spine1), 1 = chóp đuôi
        public int chain;        // đốt thứ mấy trong vây (0 = gốc)
        public float side;       // -1 / +1 vây trái / phải
        public float tipSign;    // vây giữa: +1 chóp hướng lên (vây lưng), -1 hướng xuống (vây hậu môn)
        public Vector3 finAxis;  // trục gốc -> chóp vây (root space), vây bên
    }

    [Header("Bone (nút Find Bones tìm theo quy ước tên)")]
    [SerializeField, PropertyOrder(-10)] private Transform[] bodyChain = Array.Empty<Transform>(); // Spine1..N rồi Tail1..N, đầu -> đuôi
    [SerializeField, PropertyOrder(-10)] private Transform head;
    [SerializeField, PropertyOrder(-10)] private FinChain[] fins = Array.Empty<FinChain>();

    [Header("Sóng thân / đuôi")]
    [SerializeField]
    private float swayFrequency = 1.8f;   // Hz khi Speed = 1
    [SerializeField] private float swayAngle = 12f;        // độ mỗi đốt khi Speed = 1, nhân với profile; cộng dồn ra chóp đuôi
    [SerializeField] private float waveLength = 2f;        // theo chiều dài cá; thân ngắn ít đốt nên để dài (uốn chữ C), ~1 mới ra chữ S
    [SerializeField] private AnimationCurve swayProfile = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.5f, 0.3f), new Keyframe(1f, 1f)); // biên độ đầu(0) -> đuôi(1)
    [SerializeField] private float headSway = 2f;          // độ: đầu lắc ngược pha với sóng thân

    [Header("Vây ngực (Fin_M_L / Fin_M_R)")]
    [SerializeField]
    private float finFrequency = 2.2f;    // Hz khi Speed = 1 (nhanh hơn đuôi)
    [SerializeField] private float finAngle = 14f;         // quét tới lui ở gốc vây
    [SerializeField] private float finTwist = 18f;         // xoắn quanh trục gốc->chóp, lệch pha 90° so với quét
    [SerializeField] private float finChainAngle = 6f;     // gợn sóng lan ra các đốt sau
    [SerializeField] private float finChainLag = 0.9f;     // trễ pha (rad) giữa các đốt
    [SerializeField] private float finPhaseOffset = Mathf.PI; // lệch pha trái / phải (π = so le, 0 = đồng bộ)
    [SerializeField] private float finFold = 20f;          // gập vây ra sau khi Speed > 1 (độ tại Speed = 2)

    [Header("Vây bụng (Fin_B_L / Fin_B_R)")]
    [SerializeField]
    private float pelvicAngle = 6f;       // quét ở gốc vây (đốt sau = một nửa)
    [SerializeField] private float pelvicTwist = 8f;
    [SerializeField] private float pelvicPhaseOffset = 1.2f; // rad, lệch pha so với vây ngực cùng bên

    [Header("Vây lưng / hậu môn (Fin_T_*, Fin_B_M)")]
    [SerializeField]
    private float centerFinAngle = 5f;    // độ nghiêng sang bên tại gốc vây khi Speed = 1
    [SerializeField] private float centerFinChainLag = 0.6f; // trễ pha (rad) mỗi đốt so với sóng thân tại gốc vây

    private static readonly Regex BodyBoneRegex = new(@"^(Spine|Tail)_?(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);           // Spine1 / Spine_01
    private static readonly Regex FinBoneRegex = new(@"^Fin_([TBMF])_([FBLRM])_(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);      // Fin_M_L_1 (Fish_Rig)
    private static readonly Regex FinBoneIndexFirstRegex = new(@"^Fin_([TBMF])_(\d+)_([FBLRM])$", RegexOptions.IgnoreCase | RegexOptions.Compiled); // Fin_M_01_L (Fish_1)
    private static readonly Regex FinBoneIndexLeadRegex = new(@"^Fin_(\d+)_([TBMF])_([FBLRM])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);  // Fin_1_M_L (SK_Fish*), Fin_1_F_L (SK_Fish31)
    private static readonly Regex FinBoneRayRegex = new(@"^Fin_(\d+)_(\d+)_([TBMF])_([FBLRM])$", RegexOptions.IgnoreCase | RegexOptions.Compiled); // Fin_2_1_M_L: đốt 2 của tia 1 (SK_Fish34)
    private static readonly Regex FinBoneCenterRegex = new(@"^Fin_(\d+)_([TB])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);           // Fin_01_B: thiếu <bên> = vây giữa

    private float zStart, zTip;          // tọa độ dọc thân (AlongCoord) của gốc chuỗi thân và chóp đuôi (along = InverseLerp)
    private float tailSign = -1f;        // +1 nếu đuôi nằm phía + của AlongCoord

    /// Gốc chuỗi thân (Spine_1) sau Find Bones; class con (cua ẩn sĩ) tìm chân / càng từ đây.
    protected Transform BodyRoot => bodyChain != null && bodyChain.Length > 0 ? bodyChain[0] : null;

#if UNITY_EDITOR
    /// Công cụ editor: tìm bone theo quy ước tên (Spine/Tail/Head/Fin_*), điền bodyChain / head / fins; gom SkinnedMeshRenderer nếu chưa có.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        var spines = new List<(int index, Transform t)>();
        var tails = new List<(int index, Transform t)>();
        var finGroups = new Dictionary<string, List<(int index, Transform t)>>();
        Transform foundHead = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.EndsWith("_end", StringComparison.OrdinalIgnoreCase)) continue; // chóp, không có skin
            var m = BodyBoneRegex.Match(n);
            if (m.Success)
            {
                var chain = m.Groups[1].Value.Equals("Spine", StringComparison.OrdinalIgnoreCase) ? spines : tails;
                chain.Add((int.Parse(m.Groups[2].Value), t));
                continue;
            }
            if (n.Equals("Head", StringComparison.OrdinalIgnoreCase)) { foundHead = t; continue; }
            if (!TryParseFinName(n, out string key, out int finIndex))
            {
                if (n.StartsWith("Fin_", StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"FishRigProceduralAnimator '{name}': bone '{n}' không đúng quy ước tên vây, bỏ qua.", t);
                continue;
            }
            if (!finGroups.TryGetValue(key, out var group)) finGroups[key] = group = new List<(int, Transform)>();
            group.Add((finIndex, t));
        }

        spines.Sort((a, b) => a.index.CompareTo(b.index));
        tails.Sort((a, b) => a.index.CompareTo(b.index));
        var body = new List<Transform>(spines.Count + tails.Count);
        foreach (var s in spines) body.Add(s.t);
        foreach (var s in tails) body.Add(s.t);
        bodyChain = body.ToArray();
        head = foundHead;

        var finList = new List<FinChain>();
        var keys = new List<string>(finGroups.Keys);
        keys.Sort();
        foreach (var key in keys)
        {
            if (!TryGetFinRole(key, out FinRole role, out int side))
            {
                Debug.LogWarning($"FishRigProceduralAnimator '{name}': không biết vây 'Fin_{key}', bỏ qua.", this);
                continue;
            }
            var group = finGroups[key];
            group.Sort((a, b) => a.index.CompareTo(b.index));
            var segments = new Transform[group.Count];
            for (int i = 0; i < segments.Length; i++) segments[i] = group[i].t;
            for (int i = 1; i < segments.Length; i++) // đốt sau phải là con của đốt trước, nếu không thì artist đặt nhầm tên bone
                if (segments[i].parent != segments[i - 1])
                    Debug.LogWarning($"FishRigProceduralAnimator '{name}': vây 'Fin_{key}' không liền mạch ('{segments[i].name}' không phải con của '{segments[i - 1].name}'), kiểm tra tên bone.", segments[i]);
            finList.Add(new FinChain { Role = role, Side = side, Segments = segments });
        }
        // List.Sort không ổn định: cùng vai + bên (chuỗi chính và các tia, vây lưng trước / sau) thì giữ thứ tự key đã sắp
        var order = new Dictionary<FinChain, int>(finList.Count);
        for (int i = 0; i < finList.Count; i++) order[finList[i]] = i;
        finList.Sort((a, b) => a.Role != b.Role ? a.Role.CompareTo(b.Role) : a.Side != b.Side ? a.Side.CompareTo(b.Side) : order[a].CompareTo(order[b]));
        fins = finList.ToArray();

        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"FishRigProceduralAnimator '{name}': thân {bodyChain.Length} đốt, đầu {(head ? head.name : "không có")}, {fins.Length} vây.", this);
    }

    /// Tên vây -> key "<vị trí>_<bên>" + số đốt. Nhận cả hai thứ tự: Fin_M_L_1 (Fish_Rig) và Fin_M_01_L (Fish_1);
    /// thiếu <bên> (Fin_01_B / Fin_01_T) coi là vây giữa: B -> B_M (hậu môn), T -> T_M (lưng).
    /// Tia vây Fin_<đốt>_<tia>_<vị trí>_<bên> -> key "<vị trí>_<bên>_<tia>" (chuỗi riêng; TryGetFinRole chỉ đọc hai ký tự đầu).
    private static bool TryParseFinName(string n, out string key, out int index)
    {
        key = null;
        index = 0;
        Match m;
        if ((m = FinBoneRegex.Match(n)).Success) { key = m.Groups[1].Value + "_" + m.Groups[2].Value; index = int.Parse(m.Groups[3].Value); }
        else if ((m = FinBoneIndexFirstRegex.Match(n)).Success) { key = m.Groups[1].Value + "_" + m.Groups[3].Value; index = int.Parse(m.Groups[2].Value); }
        else if ((m = FinBoneIndexLeadRegex.Match(n)).Success) { key = m.Groups[2].Value + "_" + m.Groups[3].Value; index = int.Parse(m.Groups[1].Value); }
        else if ((m = FinBoneRayRegex.Match(n)).Success) { key = m.Groups[3].Value + "_" + m.Groups[4].Value + "_" + m.Groups[2].Value; index = int.Parse(m.Groups[1].Value); }
        else if ((m = FinBoneCenterRegex.Match(n)).Success) { key = m.Groups[2].Value + "_M"; index = int.Parse(m.Groups[1].Value); }
        else return false;
        key = key.ToUpperInvariant();
        return true;
    }

    /// Fin_<vị trí>_<bên>: M_L / M_R và F_L / F_R = vây ngực, B_L / B_R = vây bụng, B_M = vây hậu môn, T_* = vây lưng.
    private static bool TryGetFinRole(string key, out FinRole role, out int side)
    {
        char pos = key[0], sd = key[2];
        side = sd == 'L' ? -1 : sd == 'R' ? 1 : 0;
        switch (pos)
        {
            case 'M':
            case 'F': role = FinRole.Pectoral; return side != 0;
            case 'B': role = side != 0 ? FinRole.Pelvic : FinRole.Anal; return true;
            case 'T': role = FinRole.Dorsal; return true;
            default: role = FinRole.Dorsal; return false;
        }
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (bodyChain == null || bodyChain.Length == 0 || bodyChain[0] == null)
        {
            Debug.LogWarning($"FishRigProceduralAnimator '{name}' chưa gán bodyChain (nút Find Bones trên prefab) nên không animate.", this);
            return;
        }

        // vị trí trên thân theo tọa độ dọc thân (AlongCoord, mặc định z root space): gốc chuỗi thân = 0, chóp đuôi (bone *_end nếu có) = 1
        Transform last = bodyChain[bodyChain.Length - 1];
        zStart = AlongCoord(bodyChain[0]);
        zTip = AlongCoord(last.childCount > 0 ? last.GetChild(0) : last);
        tailSign = zTip >= zStart ? 1f : -1f;
        if (Mathf.Abs(zTip - zStart) < 1e-5f) zTip = zStart - 1e-5f;

        foreach (var t in bodyChain)
            if (t != null) RegisterBone(new Bone { t = t, kind = Kind.Body, along = AlongOf(t) });
        if (head != null) RegisterBone(new Bone { t = head, kind = Kind.Head, along = AlongOf(head) });

        if (fins == null) return;
        var finBones = new HashSet<Transform>();
        foreach (var fin in fins)
            if (fin?.Segments != null)
                foreach (var t in fin.Segments) if (t != null) finBones.Add(t);

        foreach (var fin in fins)
        {
            if (fin == null || fin.Segments == null) continue;
            Transform rootSeg = null, lastSeg = null;
            foreach (var t in fin.Segments) if (t != null) { rootSeg ??= t; lastSeg = t; }
            if (rootSeg == null) continue;

            // tia vây tách nhánh (Fin_2_1_M_L con của Fin_1_M_L): đốt đầu của tia đánh số tiếp các đốt vây phía trên nó
            int chainOffset = 0;
            for (Transform p = rootSeg.parent; p != null && p != transform; p = p.parent)
                if (finBones.Contains(p)) chainOffset++;

            bool sideFin = fin.Role == FinRole.Pectoral || fin.Role == FinRole.Pelvic;
            Vector3 rootPos = Local(rootSeg);
            Vector3 tipPos = lastSeg.childCount > 0 ? Local(lastSeg.GetChild(0)) : Local(lastSeg); // chóp = bone *_end
            float side = fin.Side != 0 ? Mathf.Sign(fin.Side) : (rootPos.x >= 0f ? 1f : -1f);
            Vector3 axis = tipPos - rootPos;
            if (axis.sqrMagnitude < 1e-8f) axis = sideFin ? new Vector3(side, 0f, 0f) : Vector3.up;
            axis.Normalize();
            float tipSign = axis.y >= 0f ? 1f : -1f;

            for (int i = 0; i < fin.Segments.Length; i++)
            {
                var t = fin.Segments[i];
                if (t == null) continue;
                RegisterBone(new Bone
                {
                    t = t, kind = sideFin ? Kind.SideFin : Kind.CenterFin, role = fin.Role,
                    along = AlongOf(t), chain = chainOffset + i, side = side, tipSign = tipSign, finAxis = axis,
                });
            }
        }
    }

    /// Bake bone thành dữ liệu job (job: Wave = Amp * sqrt(Speed) * sin(bodyPhase - k * along - PhaseLag) quanh trục Axis, cộng uốn theo đường bơi;
    /// SideFin = quét quanh trục up + xoắn quanh trục gốc->chóp lệch pha 90°, gập ra sau theo FoldScale khi bơi nhanh).
    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        switch (b.kind)
        {
            case Kind.Head: // đầu lắc ngược pha nhẹ để mắt cá ổn định, không uốn theo đường bơi
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave,
                    Along = b.along,
                    Amp = -headSway,
                    PhaseLag = 0f,
                    BendScale = 0f,
                    PitchBend = 0,
                    Axis = BodyAxis,
                    FinAxis = BodyWaveAxis
                };
            case Kind.SideFin:
            {
                bool pectoral = b.role == FinRole.Pectoral;
                float rootAngle = pectoral ? finAngle : pelvicAngle;
                float chainAngle = pectoral ? finChainAngle : pelvicAngle * 0.5f;
                float twistAngle = pectoral ? finTwist : pelvicTwist;
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.SideFin,
                    Along = b.along,
                    Amp = b.chain == 0 ? rootAngle : chainAngle,
                    TwistAmp = twistAngle * (b.chain == 0 ? 1f : 0.5f),
                    PhaseLag = b.chain * finChainLag - (b.side < 0f ? finPhaseOffset : 0f) - (pectoral ? 0f : pelvicPhaseOffset),
                    Side = b.side,
                    FoldScale = pectoral ? 1f : 0.5f,
                    Axis = 0,
                    FinAxis = b.finAxis
                };
            }
            case Kind.CenterFin: // nghiêng sang bên quanh trục thân, chóp trễ pha so với sóng thân tại gốc vây
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave,
                    Along = b.along,
                    Amp = centerFinAngle * (b.chain == 0 ? 1f : 0.7f) * b.tipSign,
                    PhaseLag = (b.chain + 1) * centerFinChainLag,
                    BendScale = 0f,
                    PitchBend = 0,
                    Axis = CenterFinAxis
                };
            default: // Body
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave,
                    Along = b.along,
                    Amp = swayAngle * swayProfile.Evaluate(b.along),
                    PhaseLag = 0f,
                    BendScale = Mathf.Lerp(0.6f, 1.2f, b.along),
                    PitchBend = 1,
                    Axis = BodyAxis,
                    FinAxis = BodyWaveAxis
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
        p.UpAxis = new float3(0f, 1f, 0f);
        p.ForwardAxis = new float3(0f, 0f, 1f);
        p.LateralAxis = new float3(1f, 0f, 0f);
    }

    protected Vector3 Local(Transform t) => transform.InverseTransformPoint(t.position);
    protected float AlongOf(Transform t) => Mathf.InverseLerp(zStart, zTip, AlongCoord(t));

    // ---------- Hook cho class con đổi hướng thân (cá ngựa đứng dọc: SeahorseProceduralAnimator) ----------

    /// Tọa độ dọc thân của bone trong root space, dùng tính along (mặc định z: đầu +Z, đuôi -Z).
    protected virtual float AlongCoord(Transform t) => Local(t).z;
    /// Trục sóng thân / đầu: 0 = up (lắc sang bên), 1 = dọc thân, 2 = BodyWaveAxis (trục tùy ý root space).
    protected virtual int BodyAxis => 0;
    protected virtual Vector3 BodyWaveAxis => Vector3.right;
    /// Trục nghiêng vây lưng / hậu môn: 1 = dọc thân (cá nằm ngang), 0 = up (cá đứng dọc).
    protected virtual int CenterFinAxis => 1;
    /// Bone thuộc chuỗi thân (Spine / Tail) do lớp này đăng ký; class con dùng để chỉnh riêng dữ liệu bake của thân (cua ẩn sĩ giảm sóng vỏ).
    protected bool IsBodyBone(BoneRef bone) => bone is Bone b && b.kind == Kind.Body;
    protected bool IsHeadBone(BoneRef bone) => bone is Bone b && b.kind == Kind.Head;

    /// Class con đọc thông tin vây của bone (SideFin / CenterFin): vai trò, đốt (0 = gốc), bên (−1 / +1, 0 = giữa), trục gốc → chóp (vây bên).
    protected bool TryGetFin(BoneRef bone, out FinRole role, out int chain, out float side, out Vector3 finAxis)
    {
        if (bone is Bone b && (b.kind == Kind.SideFin || b.kind == Kind.CenterFin))
        {
            role = b.role; chain = b.chain; side = b.side; finAxis = b.finAxis;
            return true;
        }
        role = FinRole.Dorsal; chain = 0; side = 0f; finAxis = Vector3.zero;
        return false;
    }
}
