using UnityEngine;

/// Định hướng "đỉnh đầu theo đường bơi" (FishOrientation.HeadUpAlongPath, hải cẩu): khi di chuyển, đỉnh đầu (+Y model) quay theo tiếp tuyến
/// đường bơi trong không gian (vận tốc thật đo từ vị trí, đã lọc) và mặt (+Z model) xoay về phía camera nhiều nhất có thể (chiếu hướng camera
/// lên mặt phẳng vuông góc với hướng đi; đi thẳng về / xa camera thì mặt ngửa lên); đứng yên (chậm hơn MinSpeed) thì trở về rotation nghỉ
/// của vỏ do FishSchool đặt, tức xoay như cá thường. Thuần C#: Fish gọi Step mỗi LateUpdate rồi gán Rotation (world) cho GraphicContainer.
/// Thân uốn theo đường bơi là việc của animator (RollBend / PitchBend đo từ xoay của model).
public class HeadUpPathOrienter
{
    public float TurnSpeed = 240f;        // độ/giây xoay tối đa (mượt khi đổi hướng)
    public float MinSpeed = 0.3f;         // m/s: dưới mức này về rotation nghỉ; từ 2×MinSpeed trở lên theo hẳn hướng đi
    public float VelocitySmoothing = 6f;  // 1/s lọc vận tốc đo từ vị trí
    public float MaxStep = 2f;            // m mỗi khung, hơn = teleport

    private static readonly Vector3 TowardCamera = Vector3.back;   // camera gameplay nhìn dọc +Z

    public Vector3 Velocity { get; private set; }
    public Quaternion Rotation { get; private set; } = Quaternion.identity;
    private Vector3 lastPosition;
    private bool hasLast;

    /// Đặt lại tại vị trí mới (spawn / đổi model): không vận tốc, xoay như vỏ.
    public void Reset(Vector3 position, Quaternion restRotation)
    {
        lastPosition = position;
        hasLast = true;
        Velocity = Vector3.zero;
        Rotation = restRotation;
    }

    /// Cập nhật theo vị trí world hiện tại và rotation nghỉ (world) của vỏ; trả rotation world cho container model.
    public Quaternion Step(Vector3 position, Quaternion restRotation, float dt)
    {
        if (dt <= 0f) return Rotation;
        if (!hasLast) { Reset(position, restRotation); return Rotation; }

        Vector3 delta = position - lastPosition;
        lastPosition = position;
        if (delta.magnitude > MaxStep) delta = Vector3.zero;   // teleport: bỏ qua, giữ vận tốc cũ tắt dần
        Vector3 measured = delta / dt;
        float k = VelocitySmoothing > 0f ? 1f - Mathf.Exp(-VelocitySmoothing * dt) : 1f;
        Velocity += (measured - Velocity) * k;

        Rotation = Quaternion.RotateTowards(Rotation, TargetRotation(Velocity, restRotation, MinSpeed), TurnSpeed * dt);
        return Rotation;
    }

    /// Rotation đích: +Y model theo hướng vận tốc, +Z model = hướng camera chiếu vuông góc với hướng đi (không chiếu được thì ngửa lên +Y world);
    /// trộn dần từ rotation nghỉ khi tốc độ vượt minSpeed.
    public static Quaternion TargetRotation(Vector3 velocity, Quaternion restRotation, float minSpeed)
    {
        float speed = velocity.magnitude;
        if (speed <= minSpeed) return restRotation;
        Vector3 dir = velocity / speed;
        Vector3 face = Vector3.ProjectOnPlane(TowardCamera, dir);
        if (face.sqrMagnitude < 1e-4f) face = Vector3.ProjectOnPlane(Vector3.up, dir);
        Quaternion headUp = Quaternion.LookRotation(face.normalized, dir);
        float w = Mathf.Clamp01((speed - minSpeed) / Mathf.Max(minSpeed, 1e-3f));
        return Quaternion.Slerp(restRotation, headUp, w);
    }
}
