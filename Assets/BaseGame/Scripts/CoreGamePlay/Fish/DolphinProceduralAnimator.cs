using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho cá heo (SK_Fish28): rig cá chuẩn (Spine_1..3, Tail_1..2, Head, Fin_*) nên kế thừa FishRigProceduralAnimator
/// (Find Bones, biên độ / profile / bước sóng / tần số sóng thân và vây của lớp cha vẫn dùng, chỉnh trên prefab), nhưng cá heo là thú biển,
/// đuôi nằm ngang nên quẫy LÊN XUỐNG chứ không sang ngang như cá:
///  - thân / đuôi / đầu: sóng xoay quanh trục ngang +X root space (hook BodyAxis / BodyWaveAxis như cá ngựa); uốn khi rẽ vẫn quanh trục up
///    (FishBoneData.YawBendUp) nên đổi hướng thân vẫn cong sang bên, ngóc / chúi thì uốn theo trục ngang như cá;
///  - vây lưng / vây giữa bụng (Fin_*_T_*, Fin_*_B_M): không nghiêng sang bên theo sóng thân như cá mà gập nhẹ trước sau quanh trục ngang
///    theo sóng thân, chóp trễ pha hơn gốc (centerFlex);
///  - vây ngực Fin_M_L/R và vây bụng Fin_B_L/R: SideFin của lớp cha (quét trước sau + xoắn), biên độ nhỏ đặt trên prefab vì cá heo chủ yếu
///    dùng vây để lái.
public class DolphinProceduralAnimator : FishRigProceduralAnimator
{
    [Header("Cá heo - vây lưng / vây giữa bụng")]
    [SerializeField] private float centerFlex = 3f;        // độ gập trước sau ở gốc vây theo sóng thân (đốt sau 0.7)
    [SerializeField] private float centerFlexLag = 0.7f;   // rad trễ pha mỗi đốt so với sóng thân tại gốc vây

    protected override int BodyAxis => 2;
    protected override Vector3 BodyWaveAxis => Vector3.right;

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        FishBoneData d = base.BakeBone(bone);
        if (IsBodyBone(bone))
        {
            d.YawBendUp = 1;
            return d;
        }
        if (TryGetFin(bone, out FinRole role, out int chain, out _, out _) && (role == FinRole.Dorsal || role == FinRole.Anal))
        {
            d.Amp = centerFlex * (chain == 0 ? 1f : 0.7f);
            d.PhaseLag = (chain + 1) * centerFlexLag;
            d.Axis = 2;
            d.FinAxis = new float3(1f, 0f, 0f);
        }
        return d;
    }
}
