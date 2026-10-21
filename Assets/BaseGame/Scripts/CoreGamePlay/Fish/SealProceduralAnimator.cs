using System.Collections.Generic;
using UnityEngine;

/// Animator procedural cho hải cẩu (SK_Fish26): rig cá chuẩn (Spine_1..3, Tail_1..2, Head, Fin_*), model bind ở tư thế ngồi: đầu cao phía
/// trước (+Z), thân đi xuống và ra sau tới đuôi chẻ. Xương thật sự dịch mesh: thân / đuôi (Tail_* = vây sau), vây trước Fin_*_M_L/R; các chuỗi
/// Fin_B_L/R, Fin_B_M, Fin_T_B, Fin_T_F là lông bụng / lưng / đỉnh đầu → giữ yên.
/// Hướng: prefab Fish_26 đặt FishModel.Orientation = HeadUpAlongPath (HeadUpPathOrienter): khi bơi đỉnh đầu (+Y) theo tiếp tuyến đường bơi và
/// mặt (+Z) xoay về camera nhiều nhất có thể, đứng yên thì xoay như vỏ (góc nghỉ của cá). Vì thế sóng thân xoay quanh +Z (mặt phẳng nhìn thấy),
/// đổi hướng trong mặt phẳng đó thì thân / đuôi trễ theo (RollBend), ngóc / chúi thì uốn theo (PitchBend) → uốn theo đường bơi.
/// Kế thừa FishRigProceduralAnimator (Find Bones, trục, trễ pha / bước sóng của lớp cha) nhưng biên độ, tần số và dạng lấy từ nhóm "Hải cẩu"
/// (các field swayAngle / swayProfile / headSway / finAngle / finTwist / finChainAngle / finFold / pelvic* / centerFinAngle / swayFrequency /
/// finFrequency của lớp cha KHÔNG dùng).
///  - IDLE: ngồi như bind, đuôi đung đưa chậm (idleBodyFrequency × Speed), vây trước xòe và phe phẩy nhẹ.
///  - BƠI (nhịp ổn định): duỗi thẳng người thành một đường dọc theo −Y (đầu trên, đuôi dưới: mỗi đốt thân bù góc lệch của đoạn xương so với
///    phương thẳng đứng, PitchBias × straighten; đầu bù ngược để mặt vẫn hướng +Z), đuôi quạt trong mặt phẳng nhìn thấy (tailSweep × vị trí dọc
///    thân²), vây trước hơi ép về sau (flipperTuck) và phe phẩy nhỏ.
public class SealProceduralAnimator : FishRigProceduralAnimator
{
    [Header("Hải cẩu - nhịp")]
    [SerializeField] private float idleBodyFrequency = 0.7f;  // Hz × Speed lúc idle (~0.42 Hz): đuôi đung đưa chậm
    [SerializeField] private float idleFinFrequency = 1.5f;   // Hz × (0.7 + 0.3·Speed) lúc idle: vây trước phe phẩy
    [SerializeField] private float swimBodyFrequency = 0.9f;  // Hz khi bơi (nhịp ổn định): đuôi quạt
    [SerializeField] private float swimFinFrequency = 1.5f;   // Hz khi bơi: vây trước phe phẩy

    [Header("Hải cẩu - thân / đuôi")]
    [SerializeField] private float tailSweep = 12f;           // độ mỗi đốt tại chóp đuôi (× bình phương vị trí dọc thân), cộng dồn ≈ 15° ở chóp
    [SerializeField] private float sealHeadSway = 0.5f;       // độ đầu lắc ngược pha nhẹ (gần như đứng yên)
    [SerializeField, Range(0f, 1f)] private float straighten = 1f; // bơi: 1 = duỗi thẳng hoàn toàn thành đường dọc, 0 = giữ tư thế ngồi
    [SerializeField] private float pathBend = 0.8f;           // thân / đuôi trễ theo khi đổi hướng trong mặt phẳng nhìn (RollBend tại chóp; gốc 0.3 lần)

    [Header("Hải cẩu - vây trước")]
    [SerializeField] private float flipperFlutter = 5f;       // độ quét gốc vây trước (đốt sau 0.5)
    [SerializeField] private float flipperTwist = 5f;         // độ xoắn vây trước
    [SerializeField] private float flipperTuck = 15f;         // độ ép vây về sau khi bơi (idle 0 = xòe như bind)

    private readonly Dictionary<Transform, float> straightBias = new Dictionary<Transform, float>();   // độ bù quanh +X mỗi đốt thân khi bơi
    private float rootTilt;   // độ lệch của đoạn thân đầu tiên so với phương thẳng đứng (đầu bù ngược)

    protected override int BodyAxis => 1;   // sóng thân quanh trục mặt +Z = quạt trong mặt phẳng nhìn thấy

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        base.OnBuild(skinnedBones);
        // Duỗi thẳng: đi dọc chuỗi thân từ gốc, góc lệch (quanh +X, root space) của đoạn xương so với phương thẳng đứng −Y; đốt sau chỉ bù phần
        // lệch còn lại vì đã kế thừa xoay của đốt trước.
        straightBias.Clear();
        rootTilt = 0f;
        float prev = 0f;
        Transform t = BodyRoot;
        bool first = true;
        while (t != null)
        {
            Transform next = NextInChain(t);
            Vector3 d = next != null ? Local(next) - Local(t) : Vector3.down;
            float tilt = d.sqrMagnitude > 1e-8f ? Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg : prev;
            straightBias[t] = tilt - prev;
            if (first) { rootTilt = tilt; first = false; }
            prev = tilt;
            t = next != null && !next.name.EndsWith("_end") ? next : null;
        }
    }

    /// Con kế tiếp trong chuỗi thân (Spine_* / Tail_* / *_end), null nếu hết.
    private static Transform NextInChain(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (c.name.StartsWith("Spine") || c.name.StartsWith("Tail")) return c;
        }
        return null;
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        FishBoneData d = base.BakeBone(bone);   // hình học (Along, trục, trễ pha, bên) từ lớp cha; biên độ / dạng đặt lại bên dưới
        if (IsBodyBone(bone))
        {
            d.Amp = tailSweep * d.Along * d.Along;
            d.BendScale = 0f;                                   // không xoắn quanh trục thân khi model xoay theo yaw
            d.RollBend = pathBend * Mathf.Lerp(0.3f, 1f, d.Along);
            d.PitchBias = straightBias.TryGetValue(bone.t, out float bias) ? bias * straighten : 0f;
            d.IdleVariant = 1;
            d.IdleAmp = d.Amp;
            d.IdleBias = 0f;
            d.IdlePhaseLag = d.PhaseLag;
            d.IdlePitchBias = 0f;
            return d;
        }
        if (IsHeadBone(bone))
        {
            d.Amp = -sealHeadSway;
            d.PitchBias = -rootTilt * straighten;                // bù lại xoay của gốc để mặt vẫn hướng +Z
            d.RollBend = 0f;
            d.IdleVariant = 1;
            d.IdleAmp = d.Amp;
            d.IdleBias = 0f;
            d.IdlePhaseLag = d.PhaseLag;
            d.IdlePitchBias = 0f;
            return d;
        }
        if (TryGetFin(bone, out FinRole role, out int chain, out _, out _))
        {
            if (role == FinRole.Pectoral)
            {
                bool root = chain == 0;
                d.Amp = flipperFlutter * (root ? 1f : 0.5f);
                d.TwistAmp = flipperTwist * (root ? 1f : 0.5f);
                d.FoldScale = 0f;
                d.Bias = root ? flipperTuck : 0f;             // bias dương quanh trục dọc lên = cả hai vây gập về phía đuôi
                d.IdleVariant = 1;
                d.IdleAmp = d.Amp;
                d.IdleBias = 0f;
                d.IdlePhaseLag = d.PhaseLag;
                return d;
            }
            d.Amp = 0f;                                       // lông bụng / lưng / đỉnh đầu: đứng yên
            d.TwistAmp = 0f;
            d.Bias = 0f;
            return d;
        }
        return d;
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        base.OnBakeParams(ref p);
        p.SwayFrequency = idleBodyFrequency;
        p.FinFrequency = idleFinFrequency;
        p.FinFold = 0f;
        p.SteadyRhythm = 1;
        p.SwimSwayFrequency = swimBodyFrequency;
        p.SwimFinFrequency = swimFinFrequency;
    }
}
