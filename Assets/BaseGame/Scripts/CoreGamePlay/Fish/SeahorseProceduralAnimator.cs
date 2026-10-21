using UnityEngine;

/// Animator procedural cho cá ngựa (SK_Fish25): rig cá chuẩn (Spine_1..3, Tail_1..2, Head, Fin_*) nhưng model đứng dọc:
/// đầu ở trên (+Y), đuôi cuộn xuống dưới (-Y), mặt / bụng hướng +Z, vây lưng Fin_*_T_B ở lưng -Z, mào Fin_*_T_F trên đầu, vây ngực hai bên đầu.
/// Kế thừa FishRigProceduralAnimator (Find Bones, trục qua hook AlongCoord / BodyAxis / CenterFinAxis, trễ pha / bước sóng của lớp cha)
/// nhưng biên độ, tần số và dạng vây lấy từ nhóm "Cá ngựa" bên dưới (các field swayAngle / swayProfile / headSway / finAngle / finTwist /
/// finChainAngle / finFold / pelvicAngle / pelvicTwist / centerFinAngle / swayFrequency / finFrequency của lớp cha KHÔNG dùng).
/// Kiểu cá ngựa thật (tham khảo: bơi đứng, đẩy bằng vây lưng rung 30–70 nhịp/s, vây ngực chỉ lái, đuôi cuộn để bám):
///  - thân gần như cứng: cuộn tới lui quanh trục ngang X rất chậm, biên độ tăng dần ra chóp đuôi (bodyCurl × vị trí dọc thân);
///  - vây lưng (T_B) = SideFin quét sang hai bên quanh trục dọc thân theo pha vây nhanh, gợn chạy từ gốc xuống chóp; mào đầu (T_F) đứng yên;
///  - vây ngực / vây bụng rung nhanh biên độ nhỏ, không gập khi bơi nhanh; vây hậu môn nghiêng nhẹ theo sóng thân;
///  - bơi: nhịp ổn định (SteadyRhythm) vây swimFinFrequency ~3 Hz (nhẹ nhàng theo ý người dùng), thân swimBodyFrequency ~0.3 Hz, cả người ngả về trước leanForward độ
///    (bias ở đốt gốc Spine_1, quay dương quanh +X: đầu ra +Z, đuôi lùi −Z); idle: đứng thẳng, vây idleFinFrequency × (0.7 + 0.3·Speed), thân
///    idleBodyFrequency × Speed.
public class SeahorseProceduralAnimator : FishRigProceduralAnimator
{
    [Header("Cá ngựa - nhịp")]
    [SerializeField] private float idleBodyFrequency = 0.6f;  // Hz × Speed lúc idle (0.6 → ~0.36 Hz): đuôi cuộn / nhả rất chậm
    [SerializeField] private float idleFinFrequency = 2.5f;   // Hz × (0.7 + 0.3·Speed) lúc idle (~2.2 Hz): vây phe phẩy chậm
    [SerializeField] private float swimBodyFrequency = 0.3f;  // Hz khi bơi (nhịp ổn định)
    [SerializeField] private float swimFinFrequency = 3f;     // Hz khi bơi: vây phe phẩy nhẹ nhàng, mượt (cá thật 30–70 Hz nhưng nhìn quá "điện")

    [Header("Cá ngựa - thân")]
    [SerializeField] private float bodyCurl = 4f;             // độ mỗi đốt tại chóp đuôi (nhân vị trí dọc thân 0..1), thân gần cứng
    [SerializeField] private float headNod = 1f;              // độ đầu gật ngược pha nhẹ
    [SerializeField] private float leanForward = 10f;         // độ cả người ngả về trước khi bơi (idle 0)

    [Header("Cá ngựa - vây")]
    [SerializeField] private float dorsalFlutter = 8f;        // độ quét gốc vây lưng (đốt sau 0.7)
    [SerializeField] private float dorsalChainLag = 1.2f;     // rad trễ pha mỗi đốt vây lưng (gợn chạy xuống chóp)
    [SerializeField] private float pectoralFlutter = 5f;      // độ quét gốc vây ngực (đốt sau 0.5)
    [SerializeField] private float pectoralTwist = 4f;        // độ xoắn vây ngực
    [SerializeField] private float pelvicFlutter = 4f;        // độ vây bụng
    [SerializeField] private float analTilt = 2f;             // độ vây hậu môn nghiêng theo sóng thân

    protected override float AlongCoord(Transform t) => -Local(t).y;
    protected override int BodyAxis => 2;
    protected override Vector3 BodyWaveAxis => Vector3.right;
    protected override int CenterFinAxis => 0;

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        FishBoneData d = base.BakeBone(bone);   // hình học (Along, trục, trễ pha, bên) từ lớp cha; biên độ / dạng đặt lại bên dưới
        if (IsBodyBone(bone))
        {
            d.Amp = bodyCurl * d.Along;
            if (bone.t == BodyRoot)
            {
                d.Bias = leanForward;
                d.IdleVariant = 1;
                d.IdleAmp = d.Amp;
                d.IdleBias = 0f;
                d.IdlePhaseLag = d.PhaseLag;
            }
            return d;
        }
        if (IsHeadBone(bone))
        {
            d.Amp = -headNod;
            return d;
        }
        if (TryGetFin(bone, out FinRole role, out int chain, out float side, out Vector3 finAxis))
        {
            switch (role)
            {
                case FinRole.Dorsal:
                    if (bone.t.name.Contains("_T_F"))   // mào trên đầu: đứng yên
                    {
                        d.Amp = 0f;
                        return d;
                    }
                    return new FishBoneData
                    {
                        Kind = (int)FishBoneKind.SideFin, Along = d.Along, Amp = dorsalFlutter * (chain == 0 ? 1f : 0.7f), TwistAmp = 0f,
                        PhaseLag = chain * dorsalChainLag, Side = 1f, FoldScale = 0f, Axis = 0, FinAxis = finAxis
                    };
                case FinRole.Pectoral:
                    d.Amp = pectoralFlutter * (chain == 0 ? 1f : 0.5f);
                    d.TwistAmp = pectoralTwist * (chain == 0 ? 1f : 0.5f);
                    d.FoldScale = 0f;
                    return d;
                case FinRole.Pelvic:
                    d.Amp = pelvicFlutter * (chain == 0 ? 1f : 0.5f);
                    d.TwistAmp = pelvicFlutter * 0.5f;
                    d.FoldScale = 0f;
                    return d;
                default: // Anal
                    d.Amp = Mathf.Sign(d.Amp) * analTilt * (chain == 0 ? 1f : 0.7f);
                    return d;
            }
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
