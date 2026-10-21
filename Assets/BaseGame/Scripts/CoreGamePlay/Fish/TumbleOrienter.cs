using UnityEngine;

/// Định hướng "lắc lư đứng thẳng" (FishOrientation.Tumble, đồ vật): model lơ lửng dưới nước, không quay theo hướng đi, nhưng mặt trên luôn gần
/// như hướng lên vì ánh sáng vẽ sẵn trên model chỉ đúng ở tư thế đó. Rotation (world) = Yaw × Nghiêng × Spin:
///  - Yaw quanh trục Y world và Spin quanh trục Y của chính model đi được mọi góc. Tốc độ góc mỗi kênh = MaxSpeed × tổng ba sóng sin (chậm /
///    vừa / nhanh, tần số theo tỉ lệ vô tỉ nên tổng không lặp lại trong thời gian chơi): sóng chậm (chu kỳ ~40 s) cho model xoay gần trọn vòng
///    rồi mới đổi chiều, sóng vừa và nhanh làm tốc độ tăng giảm mượt liên tục.
///  - Nghiêng quanh trục X rồi trục Z của model, mỗi trục tối đa TiltLimit độ. Góc nghiêng = TiltLimit × tổng sóng (luôn trong [−1, 1]) nên
///    không bao giờ vượt giới hạn, không cần hãm hay chốt. Hai trục cùng chạm giới hạn thì mặt trên lệch acos(cos² TiltLimit) khỏi phương
///    thẳng đứng (20° mỗi trục = 28°).
/// Mỗi kênh một nhịp và bộ pha riêng lấy từ hạt giống, nên các kênh không chung nhịp và hai đồ vật cạnh nhau không xoay giống nhau.
/// Giới hạn hướng mặt trước (FacingLimit &lt; 180, FishModel.TumbleFacing): mặt trước (+Z model) chỉ được lệch tối đa FacingLimit độ quanh trục Y
/// khỏi hướng về camera (camera gameplay nhìn dọc +Z → về camera = −Z world) để mặt lưng không bao giờ quay ra trước. Khi đó bỏ kênh Spin và
/// Yaw = hướng camera + FacingLimit × biên độ × sin(pha): đồ vật đung đưa qua lại, chậm dần ở hai biên rồi quay đầu; pha chạy với nhịp
/// MaxSpeed / (2π × FacingLimit) (tốc độ đỉnh ≈ MaxSpeed) × (1 ± SwingRateVariation × sóng kênh Spin), biên độ mỗi lượt trong
/// [SwingAmpMin, 1] theo sóng kênh Yaw. |sin| ≤ 1 và biên độ ≤ 1 nên giới hạn đúng theo công thức; nghiêng X chỉ ngóc / chúi mặt trước, nghiêng Z
/// chỉ lăn quanh mặt trước nên không đổi hướng ngang của nó.
/// Thuần C#: Fish gọi Step mỗi LateUpdate rồi gán Rotation (world) cho GraphicContainer. Phần mềm của đồ vật bị kéo trễ theo chiều xoay là việc
/// của PropProceduralAnimator (job đo tốc độ góc của model).
public class TumbleOrienter
{
    public float MaxSpeed = 40f;      // độ/giây tối đa của mỗi kênh xoay quanh Y (tốc độ thường gặp khoảng 0.45 lần)
    public float ChangeRate = 0.15f;  // Hz của sóng vừa; sóng chậm / nhanh theo tỉ lệ Ratio
    public float MaxDt = 0.1f;        // giây: kẹp dt của khung rớt để model không nhảy góc
    public float TiltLimit = 20f;     // độ: nghiêng tối đa quanh mỗi trục X, Z của model
    public float FacingLimit = 180f;  // độ: mặt trước (+Z) lệch tối đa khỏi hướng về camera quanh trục Y; ≥ 180 = xoay tự do mọi góc

    private const int Yaw = 0, Spin = 1, TiltX = 2, TiltZ = 3;
    private const int Channels = 4;
    private const int Waves = 3;
    private const float MinHorizontal = 0.1f;   // hình chiếu ngang của mặt trước ngắn hơn mức này thì coi như đang chĩa thẳng lên / xuống
    private static readonly float[] Ratio = { 0.16f, 1f, 2.739f };    // chậm, vừa, nhanh (× ChangeRate × nhịp kênh)
    private static readonly float[] Weight = { 1f, 0.6f, 0.3f };
    private const float WeightSum = 1.9f;
    private static readonly float[] ChannelRate = { 0.85f, 1f, 1.17f, 0.92f };  // bốn nhịp khác nhau, chia ngẫu nhiên cho bốn kênh
    private const float CameraYaw = 180f;            // yaw (độ quanh Y world) khi mặt trước +Z hướng về camera (−Z world)
    private const float SwingRateVariation = 0.6f;   // giới hạn hướng: nhịp đung đưa = cơ bản × (1 ± 0.6 × sóng)
    private const float SwingAmpMin = 0.7f;          // giới hạn hướng: biên độ mỗi lượt ∈ [0.7, 1] × FacingLimit (≥ 50° khi giới hạn 75°, đủ chứa tư thế nghỉ của vỏ)

    public Quaternion Rotation { get; private set; } = Quaternion.identity;

    private readonly double[] phase = new double[Channels * Waves];   // rad, [kênh × Waves + sóng]
    private readonly double[] rate = new double[Channels];            // hệ số tần số mỗi kênh
    private double clock;
    private float yaw;    // độ, quanh trục Y world
    private float spin;   // độ, quanh trục Y của model
    private double swingPhase;   // rad, giới hạn hướng: Yaw = CameraYaw + FacingLimit × biên độ × sin(swingPhase)

    private bool FacingLimited => FacingLimit < 180f;

    /// Đặt lại (spawn / đổi model): mặt trước bắt đầu theo hướng ngang của rotation cho trước (phần ngóc / chúi / nghiêng của nó bị bỏ), pha và
    /// nhịp mỗi kênh lấy từ hạt giống. Có giới hạn hướng thì hướng đầu bị kẹp vào biên độ của lượt đầu, chiều đung đưa đầu tiên ngẫu nhiên.
    public void Reset(Quaternion startRotation, int seed)
    {
        var random = new System.Random(seed);
        int shift = random.Next(Channels);   // xoay vòng bảng nhịp để không phải lúc nào kênh Yaw cũng chậm nhất
        for (int c = 0; c < Channels; c++)
        {
            rate[c] = ChannelRate[(c + shift) % Channels] * (0.92 + 0.16 * random.NextDouble());
            for (int k = 0; k < Waves; k++) phase[c * Waves + k] = random.NextDouble() * 2.0 * System.Math.PI;
        }
        clock = 0.0;
        yaw = HorizontalYaw(startRotation);
        spin = 0f;
        if (FacingLimited)
        {
            float amplitude = SwingAmplitude();
            float offset = amplitude > 1e-3f ? Mathf.Clamp(Mathf.DeltaAngle(CameraYaw, yaw) / amplitude, -1f, 1f) : 0f;
            swingPhase = System.Math.Asin(offset);
            if (random.NextDouble() < 0.5) swingPhase = System.Math.PI - swingPhase;   // cùng góc, đi theo chiều ngược lại
            yaw = SwingYaw(amplitude);
        }
        Rotation = Compose();
    }

    /// Tiến dt giây; trả rotation world cho container model.
    public Quaternion Step(float dt)
    {
        if (dt <= 0f) return Rotation;
        dt = Mathf.Min(dt, MaxDt);
        clock += dt;
        if (FacingLimited)
        {
            // pha luôn tiến (nhịp ∈ [0.4, 1.6] × cơ bản) nên đồ vật đung đưa hết lượt này sang lượt khác, chậm dần ở hai biên
            double baseHz = MaxSpeed / (2.0 * System.Math.PI * Mathf.Max(FacingLimit, 1f));
            swingPhase += 2.0 * System.Math.PI * baseHz * (1.0 + SwingRateVariation * Wave(Spin, clock)) * dt;
            yaw = SwingYaw(SwingAmplitude());
        }
        else
        {
            yaw = Mathf.Repeat(yaw + MaxSpeed * Wave(Yaw, clock) * dt, 360f);
            spin = Mathf.Repeat(spin + MaxSpeed * Wave(Spin, clock) * dt, 360f);
        }
        Rotation = Compose();
        return Rotation;
    }

    /// Giới hạn hướng: biên độ lượt đung đưa hiện tại (độ), trong [SwingAmpMin, 1] × FacingLimit.
    private float SwingAmplitude() => Mathf.Max(FacingLimit, 0f) * Mathf.Lerp(SwingAmpMin, 1f, 0.5f + 0.5f * Wave(Yaw, clock));

    private float SwingYaw(float amplitude) => CameraYaw + amplitude * (float)System.Math.Sin(swingPhase);

    private Quaternion Compose()
    {
        Quaternion tilt = Quaternion.AngleAxis(TiltLimit * Wave(TiltX, clock), Vector3.right)
                          * Quaternion.AngleAxis(TiltLimit * Wave(TiltZ, clock), Vector3.forward);
        return (Quaternion.AngleAxis(yaw, Vector3.up) * tilt * Quaternion.AngleAxis(spin, Vector3.up)).normalized;
    }

    /// Hướng ngang (độ quanh Y world) của mặt trước; mặt trước chĩa thẳng lên / xuống thì lấy theo sườn phải, vốn nằm ngang khi đó.
    private static float HorizontalYaw(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        if (new Vector2(forward.x, forward.z).magnitude >= MinHorizontal) return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        Vector3 right = rotation * Vector3.right;
        return Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
    }

    /// Tổng sóng có trọng số của một kênh tại thời điểm t, luôn trong [−1, 1].
    private float Wave(int channel, double t)
    {
        double sum = 0.0;
        for (int k = 0; k < Waves; k++)
            sum += Weight[k] * System.Math.Sin(2.0 * System.Math.PI * ChangeRate * rate[channel] * Ratio[k] * t + phase[channel * Waves + k]);
        return (float)(sum / WeightSum);
    }
}
