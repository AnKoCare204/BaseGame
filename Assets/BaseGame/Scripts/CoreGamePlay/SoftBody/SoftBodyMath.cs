using Unity.Mathematics;

public static class SoftBodyMath
{
    /// <summary>
    /// Tách phần xoay từ ma trận biến dạng A (Muller et al. 2016, "A robust method to extract the rotational part of a deformation").
    /// q là warm start từ substep trước.
    /// </summary>
    public static quaternion ExtractRotation(in float3x3 a, quaternion q, int iterations)
    {
        if (math.lengthsq(q.value) < 0.5f) q = quaternion.identity;

        for (int it = 0; it < iterations; it++)
        {
            float3x3 r = new float3x3(q);
            float denom = math.abs(math.dot(r.c0, a.c0) + math.dot(r.c1, a.c1) + math.dot(r.c2, a.c2)) + 1e-9f;
            float3 omega = (math.cross(r.c0, a.c0) + math.cross(r.c1, a.c1) + math.cross(r.c2, a.c2)) / denom;
            float w = math.length(omega);
            if (w < 1e-9f) break;
            q = math.normalize(math.mul(quaternion.AxisAngle(omega / w, w), q));
        }

        return q;
    }

    /// <summary>Khoảng cách có dấu tới bề mặt shape (dương = ngoài vật rắn) và pháp tuyến hướng ra.</summary>
    public static float SignedDistance(in SoftShapeData s, float3 p, out float3 normal)
    {
        float d;
        switch (s.Type)
        {
            case (int)SoftShapeType.Plane:
            {
                float3 up = math.mul(s.Rotation, new float3(0f, 1f, 0f));
                d = math.dot(p - s.Position, up);
                normal = up;
                break;
            }
            case (int)SoftShapeType.Sphere:
            {
                float3 diff = p - s.Position;
                float len = math.length(diff);
                normal = len > 1e-6f ? diff / len : new float3(0f, 1f, 0f);
                d = len - s.Size.x;
                break;
            }
            case (int)SoftShapeType.Box:
            {
                quaternion inv = math.inverse(s.Rotation);
                float3 q = math.mul(inv, p - s.Position);
                float3 dq = math.abs(q) - s.Size;
                float3 outside = math.max(dq, 0f);
                float outsideLen = math.length(outside);
                float3 n;
                if (outsideLen > 1e-6f)
                {
                    d = outsideLen;
                    n = math.normalize(outside * math.sign(q));
                }
                else
                {
                    int axis = dq.x > dq.y ? (dq.x > dq.z ? 0 : 2) : (dq.y > dq.z ? 1 : 2);
                    d = dq[axis];
                    n = float3.zero;
                    n[axis] = q[axis] >= 0f ? 1f : -1f;
                }

                normal = math.mul(s.Rotation, n);
                break;
            }
            default:
            {
                quaternion inv = math.inverse(s.Rotation);
                float3 q = math.mul(inv, p - s.Position);
                float3 seg = new float3(0f, math.clamp(q.y, -s.Size.y, s.Size.y), 0f);
                float3 diff = q - seg;
                float len = math.length(diff);
                float3 n = len > 1e-6f ? diff / len : new float3(1f, 0f, 0f);
                d = len - s.Size.x;
                normal = math.mul(s.Rotation, n);
                break;
            }
        }

        if (s.Inverted != 0)
        {
            d = -d;
            normal = -normal;
        }

        return d;
    }
}

/// <summary>
/// Nón lực đỡ trong mặt phẳng 2D: các vật đang chạm (mỗi vật một pháp tuyến, hướng từ vật sang bóng, mở rộng hai bên theo góc
/// ma sát atan(friction)) có tổ hợp lực không âm nào hướng đúng SupportDirection (ngược lực ròng) không, tức là bóng có thể đứng
/// yên ở đó. Trong 2D chỉ cần xét từng pháp tuyến và từng cặp: lấy góc có dấu từ SupportDirection tới mỗi pháp tuyến, giữ góc
/// với tới gần 0 nhất ở phía âm và phía dương; có cả hai phía và cung giữa chúng (đi qua 0) không quá nửa vòng thì nằm trong nón.
/// </summary>
public struct SupportCone
{
    private readonly float2 direction;
    private float nearestNeg;
    private float nearestPos;
    private bool hasNeg;
    private bool hasPos;

    public SupportCone(float2 supportDirection)
    {
        direction = supportDirection;
        nearestNeg = 0f;
        nearestPos = 0f;
        hasNeg = false;
        hasPos = false;
    }

    public bool IsSupported => hasNeg && hasPos && nearestPos - nearestNeg <= math.PI;

    public void Add(float2 normal, float friction)
    {
        if (math.lengthsq(normal) < 1e-12f) return;
        float angle = math.atan2(direction.x * normal.y - direction.y * normal.x, math.dot(direction, normal));
        float spread = math.atan(math.max(0f, friction));
        if (angle - spread <= 0f)
        {
            float reach = math.min(angle + spread, 0f);
            nearestNeg = hasNeg ? math.max(nearestNeg, reach) : reach;
            hasNeg = true;
        }

        if (angle + spread >= 0f)
        {
            float reach = math.max(angle - spread, 0f);
            nearestPos = hasPos ? math.min(nearestPos, reach) : reach;
            hasPos = true;
        }
    }
}
