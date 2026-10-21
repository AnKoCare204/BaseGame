using UnityEngine;

/// Animator procedural cho cá đèn (SK_Fish41): rig cá chuẩn, bơi y hệt FishRigProceduralAnimator (thân, vây bụng / lưng sau / hậu môn giữ nguyên
/// tham số lớp cha); chỉ đổi cách bake hai chỗ:
///  - cần đèn Fin_*_T_F trên đầu (Find Bones xếp nó vào vai Dorsal): cần dẻo vung sang bên quanh trục dọc lên theo sóng thân với trễ pha (chóp
///    trễ hơn gốc → như roi), trễ theo rẽ hướng (BendScale, lấy mẫu lịch sử trễ nhất Along = 1) và ngóc / chúi theo đường bơi (PitchBend);
///  - vây ngực Fin_*_M_L/R là mái chèo dẹt xòe ngang sang hai bên: vỗ lên xuống như cánh cá đuối (SideFin quanh trục dọc thân, Axis 1; Side đổi
///    dấu nên hai bên cùng lên / cùng xuống), hai cánh cùng nhịp (không so le π như cá thường), đốt chóp trễ pha gốc (gợn từ thân ra chóp), xoắn
///    nhẹ quanh gốc → chóp lệch pha 90° (mép cánh ngả theo hướng vỗ), không gập ra sau khi bơi nhanh (với vây ngang gập quanh trục này chỉ làm
///    cánh ngóc lên). Tần số / biên độ theo Speed vẫn theo luật vây cá thường (idle vỗ chậm rộng, bơi vỗ nhanh hẹp).
public class AnglerfishProceduralAnimator : FishRigProceduralAnimator
{
    [Header("Cần đèn")]
    [SerializeField] private float lureSway = 4f;      // độ vung sang bên ở gốc cần theo sóng thân
    [SerializeField] private float lureTipSway = 6f;   // độ ở đốt chóp (cần dẻo, chóp vung nhiều hơn)
    [SerializeField] private float lureLag = 1.2f;     // rad trễ pha gốc cần so với sóng thân
    [SerializeField] private float lureTipLag = 2f;    // rad trễ pha chóp (roi)
    [SerializeField] private float lureBend = 1f;      // trễ theo rẽ hướng ở gốc (× turnBend của lớp cha)
    [SerializeField] private float lureTipBend = 1.4f; // trễ theo rẽ hướng ở chóp

    [Header("Vây ngực ngang (Fin_M_L / Fin_M_R)")]
    [SerializeField] private float wingFlap = 12f;     // độ vỗ lên xuống ở gốc vây (xoay quanh trục dọc thân) khi Speed = 1
    [SerializeField] private float wingTipFlap = 8f;   // độ ở đốt chóp
    [SerializeField] private float wingLag = 0.9f;     // rad chóp trễ pha gốc → gợn từ thân ra chóp
    [SerializeField] private float wingTwist = 6f;     // độ xoắn quanh trục gốc → chóp, lệch pha 90° so với vỗ (đốt chóp một nửa)

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        FishBoneData d = base.BakeBone(bone);
        if (!TryGetFin(bone, out FinRole role, out int chain, out float side, out Vector3 finAxis)) return d;
        bool root = chain == 0;
        if (role == FinRole.Pectoral)
        {
            return new FishBoneData
            {
                Kind = (int)FishBoneKind.SideFin,
                Along = d.Along,
                Amp = root ? wingFlap : wingTipFlap,
                TwistAmp = wingTwist * (root ? 1f : 0.5f),
                PhaseLag = chain * wingLag,
                Side = side,
                FoldScale = 0f,
                Axis = 1,
                FinAxis = finAxis
            };
        }
        if (role != FinRole.Dorsal || !bone.t.name.Contains("_T_F")) return d;
        return new FishBoneData
        {
            Kind = (int)FishBoneKind.Wave,
            Along = 1f,
            Amp = root ? lureSway : lureTipSway,
            PhaseLag = root ? lureLag : lureTipLag,
            BendScale = root ? lureBend : lureTipBend,
            PitchBend = 1,
            Axis = 0
        };
    }
}
