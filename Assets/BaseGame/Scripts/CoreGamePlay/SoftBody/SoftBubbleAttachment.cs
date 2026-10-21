using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghim một chỏm particle của bóng vào một Transform: bóng treo lơ lửng theo tay, gắn vào móc, hoặc đi theo platform.
/// Chọn particle theo hình nghỉ: những particle có hướng trong nón ConeAngle quanh Direction.
/// </summary>
[DefaultExecutionOrder(-60)]
public class SoftBubbleAttachment : MonoBehaviour
{
    [SerializeField] private SoftBubble Bubble;
    [SerializeField] private Transform Target;

    [SerializeField, Tooltip("Hướng (theo hình cầu nghỉ) của chỏm particle bị ghim, ví dụ (0,1,0) là đỉnh bóng")]
    private Vector3 Direction = Vector3.up;

    [SerializeField, Range(0f, 180f), Tooltip("Nửa góc nón quanh Direction. 180 = ghim toàn bộ bóng")]
    private float ConeAngle = 35f;

    [SerializeField, Range(0f, 1f), Tooltip("1 = đi theo tuyệt đối, nhỏ hơn thì bóng bị kéo mềm hơn")]
    private float Stiffness = 1f;

    public bool IsBound { get; private set; }
    public IReadOnlyList<int> Particles => particles;

    private readonly List<int> particles = new();
    private readonly List<Vector3> localOffsets = new();

    private void Reset()
    {
        Bubble = GetComponent<SoftBubble>();
    }

    private void OnEnable()
    {
        IsBound = false;
    }

    private void OnDisable() => Unbind();

    private void Update()
    {
        if (Bubble == null || Target == null) return;
        if (!IsBound)
        {
            // Đợi world đăng ký xong bóng.
            if (Bubble.Index < 0 || Bubble.ParticleCount == 0) return;
            Bind();
        }

        for (int k = 0; k < particles.Count; k++)
        {
            Bubble.SetPin(particles[k], Target.TransformPoint(localOffsets[k]), Stiffness);
        }
    }

    /// <summary>Chọn lại particle và ghi nhớ offset so với Target tại thời điểm gọi.</summary>
    public void Bind()
    {
        Unbind();
        if (Bubble == null || Target == null || Bubble.MeshData == null) return;

        Vector3 dir = Direction.sqrMagnitude > 1e-8f ? Direction.normalized : Vector3.up;
        float minDot = Mathf.Cos(ConeAngle * Mathf.Deg2Rad);
        Icosphere.Data data = Bubble.MeshData;
        for (int i = 0; i < data.Vertices.Length; i++)
        {
            if (Vector3.Dot((Vector3)data.Vertices[i], dir) < minDot) continue;
            if (!Bubble.TryGetParticlePosition(i, out Vector3 world)) continue;
            particles.Add(i);
            localOffsets.Add(Target.InverseTransformPoint(world));
        }

        IsBound = particles.Count > 0;
    }

    public void Unbind()
    {
        if (Bubble != null)
        {
            foreach (int i in particles) Bubble.ClearPin(i);
        }

        particles.Clear();
        localOffsets.Clear();
        IsBound = false;
    }
}
