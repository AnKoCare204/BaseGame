using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural cho bạch tuộc (SK_Fish21): Root > Spine_1 > Head_1 > Head_2 (mantle); 8 tay là con của Spine_1, mỗi tay 2 đốt:
/// Arm_<đốt>_F / Arm_<đốt>_B (trước / sau) và Arm_<tay>_<đốt>_L / _R, xếp thành vòng dưới thân, bind pose xòe ngang.
/// Root space: thân đứng +Y, mặt +Z. Nút Find Bones điền body / mantle / arms theo tên + cây; sửa tay trong Inspector được.
/// Hai chế độ blend theo Speed, BƠI ≈ IDLE + MỘT CHÚT (người dùng chọn sau khi thử bản phụt mạnh theo GIF): idle = tay rủ nhẹ đung đưa, lệch pha
/// nhẹ quanh vòng (ringWave), mantle thở, nhịp pulseFrequency × Speed; bơi = cùng dạng nhưng nhịp ổn định swimFrequency (không theo Speed), góc
/// nghỉ / biên độ nhỉnh hơn, méo pha (armSkew) và ép vuông tanh (armSharpen) hơi mạnh hơn, mantle vươn nhẹ. Blend idle↔bơi được lớp cha làm mượt
/// theo thời gian (blendSmoothing) nên Speed nhấp nhô trong bóng không làm tay giật.
/// Hướng: prefab Fish_21 đặt FishModel.Orientation = FaceCameraHeadFirst với FaceCameraLean = 0 (như sao biển): mặt luôn về camera, đứng thẳng,
/// không xoay theo hướng đi; các tham số RollBend (tay / mantle trễ khi thân xoay) vì thế không tác dụng, chỉ có ý nghĩa nếu đặt lại FaceCameraLean > 0.
/// Hai chế độ blend theo Speed (FishParams.IdleSpeed / SwimSpeed, xem lớp cha):
///  - Bơi: 8 tay đồng pha xoay quanh tiếp tuyến vòng tay từ 0 (bind, xòe) tới 2·armBias (rủ thẳng xuống), chóp duỗi thẳng khi cụp và trễ pha nhẹ;
///    mantle stretch dọc + bóp ngang đúng lúc cụp (kênh scale), gật nhẹ; thân nghiêng theo hướng rẽ.
///  - Idle (lơ lửng trong bóng): cùng nhịp phụt nhưng biên độ nhỏ hơn (tay không rủ hết), các tay lệch pha nhẹ (ringWave) cho tự nhiên;
///    mantle thở nhẹ hơn. Pha chạy theo Speed nên idle tự chậm hơn.
public class OctopusProceduralAnimator : FishProceduralAnimator
{
    private enum Kind { Body, Mantle, Arm }

    private class Bone : BoneRef
    {
        public Kind kind;
        public int chain;     // đốt thứ mấy (0 = gốc)
        public float angle;   // tay: góc quanh vòng (rad, 0 = phía +Z, tăng về +X)
        public Vector3 axis;  // tay: tiếp tuyến vòng tại gốc tay (root space); quay dương = hạ tay xuống / cụp về tâm
    }

    [Header("Bone (nút Find Bones)")]
    [SerializeField, PropertyOrder(-10)] private Transform body;                                   // Spine_1
    [SerializeField, PropertyOrder(-10)] private Transform[] mantle = Array.Empty<Transform>();    // Head_1..N, dưới -> trên
    [SerializeField, PropertyOrder(-10)] private BoneChain[] arms = Array.Empty<BoneChain>();

    [Header("Nhịp")]
    [SerializeField] private float pulseFrequency = 0.7f;  // Hz idle khi Speed = 1 (idle thực 0.6 → ~0.42 Hz)
    [SerializeField] private float swimFrequency = 0.55f;  // Hz khi bơi (nhịp ổn định, không theo Speed): nhanh hơn idle một chút

    [Header("Thân / mantle")]
    [SerializeField] private float bodySway = 2f;          // độ lắc thân quanh trục dọc lên
    [SerializeField] private float mantleNod = 1.3f;       // độ gật mantle quanh trục ngang lúc bơi, mỗi đốt (idle 1)
    [SerializeField] private float mantleNodIdle = 1f;     // độ gật lúc idle
    [SerializeField] private float mantleLag = 0.3f;       // rad trễ pha mỗi đốt mantle
    [SerializeField] private float mantleStretch = 0.08f;  // bơi: mantle dài ra theo trục dọc (tỉ lệ) lúc cụp, bóp ngang 0.6 lần; nhỉnh hơn thở idle
    [SerializeField] private float mantleBreath = 0.06f;   // idle: thở nhẹ (tỉ lệ)
    [SerializeField] private float mantleBend = 0.5f;      // mantle nghiêng theo hướng rẽ

    [Header("Tay - bơi (≈ idle + một chút)")]
    [SerializeField] private float armBias = 33f;          // độ: góc nghỉ gốc tay dưới bind pose khi bơi (idle 30)
    [SerializeField] private float armSweep = 28f;         // độ biên độ ở gốc tay khi bơi, không nhân √Speed (idle nhìn thấy ≈ 30 × √0.6 = 23)
    [SerializeField] private float armTipBias = 11f;       // độ góc nghỉ đốt chóp (idle 10)
    [SerializeField] private float armTipSweep = 9f;       // độ biên độ đốt chóp (idle nhìn thấy ≈ 7.7)
    [SerializeField] private float armLag = 0.5f;          // rad trễ pha chóp so với gốc (như idle)
    [SerializeField] private float armSkew = 0.3f;         // méo pha: cụp nhanh, xòe ra chậm (idle 0.25)
    [SerializeField] private float armSharpen = 0.2f;      // ép sóng gần vuông (tanh) nhẹ (idle 0.15; cao hơn = giật gắt, dễ thấy khựng)
    [SerializeField] private float armBend = 0.3f;         // tay bị kéo theo hướng rẽ (yaw)
    [SerializeField] private float armRollBend = 0.6f;     // tay trễ lại khi thân xoay trong mặt phẳng màn hình để đầu dẫn hướng (HeadFirstOrienter)
    [SerializeField] private float armTipRollBend = 0.8f;  // chóp trễ nhiều hơn gốc
    [SerializeField] private float mantleRollBend = 0.3f;  // mantle dẫn đầu nên trễ ít

    [Header("Tay - idle (lơ lửng)")]
    [SerializeField] private float armIdleBias = 30f;      // độ góc nghỉ gốc tay lúc idle (tay không rủ hết cỡ)
    [SerializeField] private float armIdleSway = 30f;      // độ biên độ gốc tay lúc idle
    [SerializeField] private float armIdleTipBias = 10f;   // độ góc nghỉ chóp
    [SerializeField] private float armIdleTipSway = 10f;   // độ biên độ chóp
    [SerializeField] private float armIdleLag = 0.5f;      // rad trễ pha chóp
    [SerializeField] private float armIdleSkew = 0.25f;    // idle: méo pha nhẹ (vẫn hơi "phụt" nhưng mềm)
    [SerializeField] private float armIdleSharpen = 0.15f; // idle: gần sin, không giữ-rồi-giật (tránh cảm giác khựng khi lơ lửng)
    [SerializeField] private float ringWave = 0.3f;        // số vòng sóng quanh vòng tay lúc idle (0 = đồng pha; nhỏ = lệch nhẹ cho tự nhiên)

#if UNITY_EDITOR
    /// Công cụ editor: body = Spine nông nhất, mantle = chuỗi Head_* dưới body, arms = mỗi con Arm_* của body theo chuỗi con đầu tiên.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        body = CreatureRigUtil.FindShallowest(transform, "Spine");
        Transform headRoot = CreatureRigUtil.Child(body, "Head");
        mantle = headRoot != null ? CreatureRigUtil.Chain(headRoot) : Array.Empty<Transform>();
        var list = new List<BoneChain>();
        foreach (Transform root in CreatureRigUtil.Children(body, "Arm_"))
            list.Add(new BoneChain { Segments = CreatureRigUtil.Chain(root) });
        arms = list.ToArray();
        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"OctopusProceduralAnimator '{name}': thân {(body ? body.name : "không có")}, mantle {mantle.Length} đốt, {arms.Length} tay.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        if (body == null)
        {
            Debug.LogWarning($"OctopusProceduralAnimator '{name}' chưa gán body (nút Find Bones trên prefab) nên không animate.", this);
            return;
        }

        Vector3 center = CreatureRigUtil.Local(transform, body);
        RegisterBone(new Bone { t = body, kind = Kind.Body });
        for (int i = 0; i < mantle.Length; i++)
            if (mantle[i] != null) RegisterBone(new Bone { t = mantle[i], kind = Kind.Mantle, chain = i });

        foreach (BoneChain arm in arms)
        {
            Transform first = CreatureRigUtil.First(arm?.Segments);
            if (first == null) continue;
            Vector3 radial = Vector3.ProjectOnPlane(CreatureRigUtil.Local(transform, first) - center, Vector3.up);
            if (radial.sqrMagnitude < 1e-8f)
                radial = Vector3.ProjectOnPlane(CreatureRigUtil.Local(transform, CreatureRigUtil.Tip(CreatureRigUtil.Last(arm.Segments))) - center, Vector3.up);
            if (radial.sqrMagnitude < 1e-8f) radial = Vector3.forward;
            radial.Normalize();
            float angle = Mathf.Atan2(radial.x, radial.z);
            Vector3 axis = Vector3.Cross(Vector3.up, radial).normalized;   // quay dương quanh trục này = hạ đầu tay hướng ra ngoài xuống dưới
            for (int i = 0; i < arm.Segments.Length; i++)
                if (arm.Segments[i] != null) RegisterBone(new Bone { t = arm.Segments[i], kind = Kind.Arm, chain = i, angle = angle, axis = axis });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (Bone)bone;
        switch (b.kind)
        {
            case Kind.Mantle:
            {
                bool root = b.chain == 0;   // chỉ scale đốt gốc: đốt trên là con nên kế thừa, scale cả hai sẽ nhân đôi
                float side = -0.6f;
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = 0f, Amp = mantleNod, PhaseLag = b.chain * mantleLag, PhaseSkew = armSkew, PulseSharpen = armSharpen,
                    BendScale = mantleBend, PitchBend = 0, Axis = 2, FinAxis = new float3(1f, 0f, 0f), RollBend = mantleRollBend,
                    ScaleAmp = root ? new float3(side * mantleStretch, mantleStretch, side * mantleStretch) : float3.zero,
                    IdleVariant = 1, IdleAmp = mantleNodIdle, IdlePhaseLag = b.chain * mantleLag, IdleBias = 0f,
                    IdleScaleAmp = root ? new float3(side * mantleBreath, mantleBreath, side * mantleBreath) : float3.zero,
                    IdlePhaseSkew = armIdleSkew, IdlePulseSharpen = armIdleSharpen
                };
            }
            case Kind.Arm:
            {
                bool root = b.chain == 0;
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = 0.5f, Axis = 2, FinAxis = b.axis, BendScale = armBend, PitchBend = 0,
                    RollBend = root ? armRollBend : armTipRollBend,
                    Amp = root ? armSweep : armTipSweep, Bias = root ? armBias : armTipBias, PhaseLag = b.chain * armLag + ringWave * b.angle,   // bơi cũng lệch pha nhẹ quanh vòng như idle
                    PhaseSkew = armSkew, PulseSharpen = armSharpen,
                    IdleVariant = 1, IdleAmp = root ? armIdleSway : armIdleTipSway, IdleBias = root ? armIdleBias : armIdleTipBias,
                    IdlePhaseLag = b.chain * armIdleLag + ringWave * b.angle, IdlePhaseSkew = armIdleSkew, IdlePulseSharpen = armIdleSharpen
                };
            }
            default:
                return new FishBoneData
                {
                    Kind = (int)FishBoneKind.Wave, Along = 0f, Amp = bodySway, PhaseLag = 0f, BendScale = 1f, PitchBend = 1, Axis = 0
                };
        }
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = pulseFrequency;
        p.FinFrequency = pulseFrequency;
        p.WaveK = 0f;
        p.SteadyRhythm = 1;                 // bơi: nhịp và biên độ theo tham số, không theo Speed (FishSchool báo ≈ 2.4 khi bơi)
        p.SwimSwayFrequency = swimFrequency;
        p.SwimFinFrequency = swimFrequency;
    }
}
