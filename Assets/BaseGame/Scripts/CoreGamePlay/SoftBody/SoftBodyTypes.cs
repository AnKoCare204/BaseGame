using System;
using Unity.Mathematics;
using UnityEngine;

public enum SoftShapeType
{
    Plane = 0,
    Sphere = 1,
    Box = 2,
    Capsule = 3
}

public enum SoftSimulationMode
{
    Update,
    FixedUpdate
}

/// <summary>Tham số của một SoftBubbleConfig, đã chuyển sang struct unmanaged để dùng trong job.</summary>
public struct SoftBubbleParams
{
    public float EdgeCompliance;
    public float VolumeCompliance;

    /// <summary>Hệ số gamma của giảm chấn XPBD cho cạnh: gamma/(1+gamma) là phần vận tốc dọc cạnh bị triệt mỗi substep.</summary>
    public float EdgeDampingGamma;

    /// <summary>Hệ số gamma của giảm chấn XPBD cho thể tích.</summary>
    public float VolumeDampingGamma;

    /// <summary>Độ mềm của nan hoa nối tâm với từng đỉnh (khối đặc kiểu thạch).</summary>
    public float RadialCompliance;

    /// <summary>Hệ số gamma của giảm chấn XPBD cho nan hoa.</summary>
    public float RadialDampingGamma;

    public float Pressure;
    public float ShapeStiffness;
    public int AllowRotation;

    public float GravityScale;
    public float Buoyancy;
    public float LinearDrag;
    public float InternalDamping;
    public float MaxSpeed;

    public float SurfaceCompliance;
    public float SurfaceFriction;

    /// <summary>0..1: tỉ lệ vận tốc pháp tuyến tương đối bị triệt khi chạm (restitution 0 khi = 1).</summary>
    public float ContactDamping;

    public int SleepEnabled;
    public float SleepThreshold;
    public float SleepDelay;
}

/// <summary>Luật tiếp xúc giữa hai bóng (unmanaged, dùng trong job).</summary>
public struct ContactRule
{
    public int Enabled;
    public float Compliance;
    public float Friction;
    public float Margin;
    public float AdhesionDistance;
    public float AdhesionStrength;

    public static ContactRule Disabled => default;

    public static ContactRule Average(ContactRule a, ContactRule b)
    {
        return new ContactRule
        {
            Enabled = (a.Enabled != 0 && b.Enabled != 0) ? 1 : 0,
            Compliance = (a.Compliance + b.Compliance) * 0.5f,
            Friction = (a.Friction + b.Friction) * 0.5f,
            Margin = (a.Margin + b.Margin) * 0.5f,
            AdhesionDistance = (a.AdhesionDistance + b.AdhesionDistance) * 0.5f,
            AdhesionStrength = (a.AdhesionStrength + b.AdhesionStrength) * 0.5f
        };
    }
}

/// <summary>Bản Inspector của ContactRule (class để giữ default value khi serialize).</summary>
[Serializable]
public class ContactSettings
{
    [Tooltip("Cho phép va chạm với bóng khác")]
    public bool Enabled = true;

    [Tooltip("Độ mềm của contact. 0 = cứng. Càng lớn hai bóng càng lún vào nhau. 0.00001 lún nhẹ, 0.0001 lún sâu")]
    [Range(0f, 0.0001f)] public float Compliance = 0.00001f;

    [Range(0f, 1f)]
    public float Friction = 0.2f;

    [Tooltip("Cộng thêm vào bán kính bóng kia khi va chạm. Âm = cho phép lấn vào nhau nhiều hơn")]
    public float Margin = 0f;

    [Tooltip("Khoảng cách ngoài bề mặt bóng kia mà particle vẫn bị hút vào (dính nhau). 0 = tắt")]
    public float AdhesionDistance = 0f;

    [Tooltip("Tỉ lệ hút mỗi substep (0..1)")]
    [Range(0f, 1f)]
    public float AdhesionStrength = 0f;

    public ContactRule ToRule()
    {
        return new ContactRule
        {
            Enabled = Enabled ? 1 : 0,
            Compliance = Compliance,
            Friction = Friction,
            Margin = Margin,
            AdhesionDistance = AdhesionDistance,
            AdhesionStrength = AdhesionStrength
        };
    }
}

/// <summary>Vị trí của một bóng trong các mảng particle/edge/tri dùng chung.</summary>
public struct BubbleDesc
{
    public int ParticleStart;

    /// <summary>Tổng particle gồm các đỉnh mesh và một particle tâm (hub) ở cuối.</summary>
    public int ParticleCount;

    /// <summary>Số particle là đỉnh mesh (bề mặt). Particle tâm nằm ngay sau.</summary>
    public int SurfaceCount;

    public int EdgeStart;
    public int EdgeCount;

    /// <summary>Nan hoa: cạnh nối particle tâm với từng đỉnh, nằm sau các cạnh bề mặt.</summary>
    public int RadialEdgeStart;
    public int RadialEdgeCount;

    public int TriStart;
    public int TriCount;
    public float RestVolume;

    /// <summary>Bán kính của lớp particle mô phỏng (đã trừ ParticleRadius so với bán kính hiển thị).</summary>
    public float RestRadius;

    /// <summary>Bán kính mỗi particle: độ dày khi chạm shape, bán kính hạt khi chạm bóng khác, và độ đẩy mesh ra ngoài.</summary>
    public float ParticleRadius;

    /// <summary>Độ dài cạnh lưới trung bình lúc nghỉ, dùng để mở rộng vùng dò hạt sao cho không lọt qua tam giác.</summary>
    public float EdgeLength;

    /// <summary>Đoạn đỉnh lưới hiển thị của bóng trong mảng visual của world. VisualCount đỉnh đầu là SurfaceCount particle, phần sau là trung điểm nội suy.</summary>
    public int VisualStart;
    public int VisualCount;

    /// <summary>Tam giác lưới hiển thị (index toàn cục vào mảng visual).</summary>
    public int VisualTriStart;
    public int VisualTriCount;

    public int ParamsIndex;
}

/// <summary>Ghim một particle về vị trí world (kéo bằng chuột, gắn vào transform). Stiffness 1 = đi theo tuyệt đối.</summary>
public struct ParticlePin
{
    public int Particle;
    public float3 Target;
    public float Stiffness;
}

/// <summary>Trạng thái tổng hợp của một bóng sau mỗi substep.</summary>
public struct BubbleState
{
    public float3 Center;
    public float3 Velocity;
    public quaternion Rotation;

    /// <summary>Khoảng cách trung bình từ tâm tới particle.</summary>
    public float Radius;

    /// <summary>Khoảng cách lớn nhất từ tâm tới particle, dùng cho broadphase.</summary>
    public float MaxRadius;

    public float Volume;
    public float MaxStrain;

    /// <summary>Thời gian liên tục (giây) bóng đứng yên dưới ngưỡng ngủ.</summary>
    public float SleepTimer;

    /// <summary>1 = đang ngủ: bỏ qua mô phỏng, vẫn là vật cản cho bóng khác.</summary>
    public int Sleeping;

    public int Active;
}

/// <summary>Lệnh từ gameplay gửi vào solver cho một bóng trong step hiện tại.</summary>
public struct BubbleCommand
{
    public float3 VelocityDelta;
    public float3 Target;
    public float TargetStiffness;
    public float TargetDamping;
    public int HasTarget;
    public int Frozen;

    /// <summary>Tọa độ Z mà tâm bóng bị giữ cố định khi world bật LockZ.</summary>
    public float PlaneZ;

    /// <summary>Đoạn ghim của bóng này trong mảng Pins của world.</summary>
    public int PinStart;
    public int PinCount;

    /// <summary>1 = đánh thức riêng bóng này ở step hiện tại (đổi bán kính, Wake() từ gameplay, hàng xóm vừa đổi kích thước).</summary>
    public int Wake;
}

/// <summary>
/// Một đỉnh lưới hiển thị (vị trí so với tâm bóng + pháp tuyến), khớp stream 0 của Mesh (Position float3, Normal float3)
/// để upload một lần bằng SetVertexBufferData.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct SoftVisualVertex
{
    public float3 Position;
    public float3 Normal;
}

/// <summary>Collider giải tích (plane, sphere, box, capsule) cho particle va chạm.</summary>
public struct SoftShapeData
{
    public int Type;
    public int Inverted;
    public float3 Position;
    public quaternion Rotation;
    public float3 Size;
    public float Friction;
}
