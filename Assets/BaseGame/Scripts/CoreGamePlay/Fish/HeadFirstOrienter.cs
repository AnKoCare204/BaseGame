using UnityEngine;

/// Định hướng model kiểu "bị đẩy đi" cho bạch tuộc / sứa (FishOrientation.FaceCameraHeadFirst): mặt (+Z local) luôn quay về camera
/// (camera gameplay nhìn dọc +Z, không nghiêng → mặt hướng −Z world), còn đỉnh đầu (+Y local) xoay trong mặt phẳng màn hình XY để
/// dẫn theo hướng vận tốc thật (đo từ vị trí, đã lọc), xoay tối đa MaxLean (180 = trọn vòng, đi xuống thì lộn ngược — bạch tuộc; 0 = luôn
/// đứng thẳng — sao biển). Chậm hơn MinSpeed thì dựng thẳng lại.
/// Thuần C#, không phụ thuộc Unity lifecycle: Fish gọi Step mỗi LateUpdate rồi gán Rotation cho GraphicContainer (world). Thành phần Z
/// của vận tốc (chiều sâu) bị bỏ qua; bước nhảy quá MaxStep coi là đặt lại vị trí (không tính vận tốc).
public class HeadFirstOrienter
{
    public float TurnSpeed = 240f;        // độ/giây xoay tối đa (mượt khi đổi hướng)
    public float MinSpeed = 0.3f;         // m/s: dưới mức này (bob / trôi nhẹ trong bóng) giữ thẳng; từ 2×MinSpeed trở lên theo hẳn vận tốc
    public float VelocitySmoothing = 6f;  // 1/s lọc vận tốc đo từ vị trí
    public float MaxStep = 2f;            // m mỗi khung, hơn = teleport
    public float MaxLean = 180f;          // độ nghiêng tối đa của đỉnh đầu khỏi thẳng đứng về phía vận tốc (FishModel.FaceCameraLean)

    private static readonly Vector3 TowardCamera = Vector3.back;

    public Vector3 Velocity { get; private set; }
    public Quaternion Rotation { get; private set; } = Quaternion.LookRotation(TowardCamera, Vector3.up);
    private Vector3 lastPosition;
    private bool hasLast;

    /// Đặt lại tại vị trí mới (spawn / đổi model): đứng thẳng, nhìn camera, không vận tốc.
    public void Reset(Vector3 position)
    {
        lastPosition = position;
        hasLast = true;
        Velocity = Vector3.zero;
        Rotation = Quaternion.LookRotation(TowardCamera, Vector3.up);
    }

    /// Cập nhật theo vị trí world hiện tại; trả rotation world cho container model.
    public Quaternion Step(Vector3 position, float dt) => Step(position, dt, 1f);

    /// velocityScale: nhân vào vận tốc đo được trước khi lọc. Loài di chuyển theo nhịp (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed) truyền 1 / MoveSpeedMultiplier để đoạn trượt chậm
    /// giữa hai nhịp không bị coi là "đứng yên" (đầu giữ theo hướng đi thay vì dựng thẳng lại rồi nghiêng lại mỗi nhịp).
    public Quaternion Step(Vector3 position, float dt, float velocityScale)
    {
        if (dt <= 0f) return Rotation;
        if (!hasLast) { Reset(position); return Rotation; }

        Vector3 delta = position - lastPosition;
        lastPosition = position;
        if (delta.magnitude > MaxStep) delta = Vector3.zero;   // teleport: bỏ qua, giữ vận tốc cũ tắt dần
        Vector3 measured = delta / dt * velocityScale;
        float k = VelocitySmoothing > 0f ? 1f - Mathf.Exp(-VelocitySmoothing * dt) : 1f;
        Velocity += (measured - Velocity) * k;

        Rotation = Quaternion.RotateTowards(Rotation, TargetRotation(Velocity, MinSpeed, MaxLean), TurnSpeed * dt);
        return Rotation;
    }

    /// Rotation đích: mặt về camera, đỉnh đầu xoay trong mặt phẳng XY về phía vận tốc (trộn dần từ thẳng đứng khi tốc độ vượt minSpeed),
    /// góc xoay kẹp trong ±maxLean (180 = theo hẳn hướng vận tốc, 0 = luôn thẳng đứng).
    public static Quaternion TargetRotation(Vector3 velocity, float minSpeed, float maxLean = 180f)
    {
        Vector3 planar = new Vector3(velocity.x, velocity.y, 0f);
        float speed = planar.magnitude;
        float lean = 0f;
        if (speed > minSpeed && maxLean > 0f)
        {
            float w = Mathf.Clamp01((speed - minSpeed) / Mathf.Max(minSpeed, 1e-3f));
            float toward = Vector2.SignedAngle(Vector2.up, new Vector2(planar.x, planar.y));   // góc từ thẳng đứng tới hướng đi, quanh trục nhìn
            lean = Mathf.Clamp(toward, -maxLean, maxLean) * w;
        }
        Vector3 up = Quaternion.AngleAxis(lean, Vector3.forward) * Vector3.up;
        return Quaternion.LookRotation(TowardCamera, up);
    }
}
