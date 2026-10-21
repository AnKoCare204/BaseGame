using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// Animator procedural chung cho đồ vật (SK_Prop*): đồ vật không có quy ước xương riêng nên nút Find Bones gom mọi bone có skin
/// (trừ Root xoay cả model và *_end). Đồ vật trôi xoay tự do (prefab đặt FishModel.Orientation = Tumble, TumbleOrienter xoay cả model); job đo
/// tốc độ góc của model trên cả ba trục và bone ở đây ngả ngược chiều xoay như bị nước kéo lại (xoay càng nhanh ngả càng nhiều):
///  - bone gắn sát Root (tầng 0) là lõi cứng: thân vali, bánh lái, đáy lon, gốc san hô (coreSoftness, mặc định 0);
///  - bone sâu hơn là phần mềm: tầng càng sâu càng mềm (softnessPerDepth) và càng trễ (lấy mẫu lịch sử xoay muộn hơn, tầng sâu nhất trễ trọn
///    bendDelay của lớp cha) nên dải vải / cổ phao / nhánh san hô uốn lan dần từ gốc ra ngọn.
/// Đung đưa đều theo pha (swayAngle) mặc định tắt vì chuyển động đến từ việc xoay; bật thì đung đưa quanh trục dọc lên.
/// Đồ vật không xương (mesh tĩnh) thì không gắn component này (FishModel.Animator để trống như key_bubble), vẫn xoay cả khối nhờ Tumble.
/// Đồ vật cần chuyển động riêng: kế thừa class này rồi override FindBones / OnBuild / BakeBone / OnBakeParams.
public class PropProceduralAnimator : FishProceduralAnimator
{
    protected class PropBone : BoneRef
    {
        public int depth; // số bone đã đăng ký phía trên (0 = gắn sát Root)
    }

    [Header("Bone (nút Find Bones gom mọi bone có skin)")]
    [SerializeField, PropertyOrder(-10)] private Transform[] bones = Array.Empty<Transform>();

    [Header("Mềm theo chiều xoay (model xoay thì bone ngả ngược lại)")]
    [SerializeField] private float softness = 4f;            // hệ số ngả của bone mềm tầng 1 (× turnBend độ của lớp cha mỗi 90°/s)
    [SerializeField] private float softnessPerDepth = 0.35f; // mỗi tầng sâu hơn mềm thêm bấy nhiêu lần softness
    [SerializeField] private float coreSoftness;             // bone gắn sát Root (lõi cứng = 0); > 0 nếu muốn cả lõi hơi dẻo

    [Header("Đung đưa đều (0 = tắt)")]
    [SerializeField] private float swayFrequency = 0.6f;  // Hz khi Speed = 1
    [SerializeField] private float swayAngle;             // độ mỗi bone khi Speed = 1, quanh trục dọc lên
    [SerializeField] private float swayLag = 0.6f;        // rad trễ pha mỗi tầng bone

    private int maxDepth;

    protected Transform[] PropBones => bones;

#if UNITY_EDITOR
    /// Công cụ editor: gom SkinnedMeshRenderer nếu chưa có rồi lấy mọi bone có skin, bỏ Root và *_end.
    [Button("Find Bones"), PropertyOrder(-11)]
    public virtual void FindBones()
    {
        if (SkinnedMeshes == null || SkinnedMeshes.Length == 0) CollectSkinnedMeshes();
        var found = new List<Transform>();
        foreach (SkinnedMeshRenderer smr in SkinnedMeshes)
        {
            if (smr == null || smr.bones == null) continue;
            foreach (Transform b in smr.bones)
            {
                if (b == null || found.Contains(b) || CreatureRigUtil.IsEnd(b)) continue;
                if (b.name.Equals("Root", StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(b);
            }
        }
        bones = found.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"{GetType().Name} '{name}': {bones.Length} bone.", this);
    }
#endif

    protected override void OnBuild(HashSet<Transform> skinnedBones)
    {
        maxDepth = 0;
        if (bones == null) return;
        foreach (Transform t in bones)
        {
            if (t == null) continue;
            int depth = 0;
            for (Transform p = t.parent; p != null && p != transform; p = p.parent)
                if (Array.IndexOf(bones, p) >= 0) depth++;
            maxDepth = Mathf.Max(maxDepth, depth);
            RegisterBone(new PropBone { t = t, depth = depth });
        }
    }

    protected override FishBoneData BakeBone(BoneRef bone)
    {
        var b = (PropBone)bone;
        float soft = Mathf.Max(0f, b.depth == 0 ? coreSoftness : softness * (1f + softnessPerDepth * (b.depth - 1)));
        return new FishBoneData
        {
            Kind = (int)FishBoneKind.Wave,
            Along = maxDepth > 0 ? (float)b.depth / maxDepth : 0f,   // job lấy mẫu lịch sử xoay trễ BendDelay × Along
            Amp = swayAngle,
            PhaseLag = b.depth * swayLag,
            BendScale = soft,                 // model xoay quanh trục dọc lên (kênh yaw cộng quanh trục sóng nên Axis phải là 0)
            PitchBend = soft > 0f ? 1 : 0,    // quanh trục ngang
            RollBend = soft,                  // quanh trục dọc thân
            Axis = 0
        };
    }

    protected override void OnBakeParams(ref FishParams p)
    {
        p.SwayFrequency = swayFrequency;
        p.FinFrequency = swayFrequency;
        p.WaveK = 0f;
        p.PitchBend = p.TurnBend;   // đồ vật không có "đầu đuôi": mềm như nhau trên cả ba trục
    }
}
