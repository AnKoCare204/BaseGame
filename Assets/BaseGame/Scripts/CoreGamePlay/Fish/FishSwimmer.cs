using UnityEngine;

/// Cá bơi lượn tự do từ chỗ đang đứng: hướng bơi được pha nhiễu Perlin để lượn cong tự nhiên, thi thoảng đổi tốc độ,
/// quay đầu mượt (ease-out), nghiêng thân khi rẽ, giảm tốc khi rẽ gấp, nhấp nhô nhẹ theo trục dọc.
/// Trong bóng và trong bể, FishSchool tự đặt chỗ cho cá nên StopSwimming + tắt ngay sau Fish.Initialize; cá ở ô chờ cũng
/// StopSwimming. Bơi tự do chỉ còn dùng ở scene demo (FishSwimDemo).
/// Đồng bộ Speed của FishProceduralAnimator theo vận tốc thực (1 = tốc độ danh định).
/// Quy ước: +Z (transform.forward) là hướng đầu cá (prefab đã xoay model cho đúng).
///
/// Dùng: fishSwimmer.WithAnimator(anim).StartSwimming(); ... StopSwimming();
public class FishSwimmer : MonoBehaviour
{
    [Header("Chuyển động")]
    [SerializeField] private float speed = 1f;                 // m/s danh định
    [SerializeField] private float speedVariation = 0.4f;      // ± tỉ lệ mỗi lần đổi tốc độ (0.4 = 60%..140%)
    [SerializeField] private float acceleration = 1f;          // m/s²
    [SerializeField] private float turnSpeed = 90f;            // độ/giây tối đa
    [SerializeField] private float turnResponse = 3f;          // rẽ mượt: tốc độ rẽ tỉ lệ góc còn lại (ease-out), càng lớn càng gắt
    [SerializeField] private float maxPitch = 25f;             // giới hạn ngóc lên / chúi xuống (độ)
    [SerializeField] private float bankAngle = 20f;            // nghiêng thân khi rẽ (độ)
    [SerializeField] private float turnSlowdown = 0.5f;        // giảm tốc khi phải rẽ gấp (0..1)
    [SerializeField] private Vector2 retargetInterval = new Vector2(2f, 5f); // đổi tốc độ sau bấy nhiêu giây (ngẫu nhiên trong khoảng)

    [Header("Lượn")]
    [SerializeField] private float wanderAngle = 25f;          // nhiễu hướng ngang (độ) để đường bơi cong chứ không thẳng
    [SerializeField] private float wanderPitch = 8f;           // nhiễu hướng dọc (độ)
    [SerializeField] private float wanderFrequency = 0.25f;    // Hz
    [SerializeField] private float wobbleAmplitude = 0.03f;    // nhấp nhô nhẹ theo trục dọc (m)
    [SerializeField] private float wobbleFrequency = 0.7f;

    [Header("Đồng bộ animation")]
    [SerializeField] private FishProceduralAnimator fishAnimator;   // Fish set qua WithAnimator; scene demo gán sẵn trong Inspector (null = không đồng bộ animation)
    [SerializeField] private float idleAnimSpeed = 0.5f;            // Speed animator khi đứng yên (StopSwimming)

    private Vector3 wobbleOffset;
    private Quaternion heading = Quaternion.identity;   // hướng bơi, không có roll
    private float v, targetSpeed, retargetTimer, bank, wobblePhase, clock, noiseSeed;

    public bool IsSwimming { get; private set; }

    /// Gắn animator để đồng bộ tốc độ vẫy đuôi.
    public FishSwimmer WithAnimator(FishProceduralAnimator animator)
    {
        fishAnimator = animator;
        return this;
    }

    /// Bắt đầu bơi từ vị trí / hướng hiện tại. Gọi lại sau khi StopSwimming hoặc sau khi dời cá cũng được.
    public void StartSwimming()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        heading = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f) : Quaternion.identity;
        wobbleOffset = Vector3.zero;
        wobblePhase = Random.value * 10f;
        noiseSeed = Random.value * 1000f;
        bank = 0f;
        PickSpeed();
        v = targetSpeed;
        IsSwimming = true;
    }
    public void StopSwimming()
    {
        IsSwimming = false;
        v = 0f;
        if (fishAnimator) fishAnimator.Speed = idleAnimSpeed;
    }

    private void Update()
    {
        if (IsSwimming) Tick(Time.deltaTime);
    }

    private void Tick(float dt)
    {
        if (dt <= 0f) return;
        clock += dt;
        Vector3 pos = transform.position - wobbleOffset;

        retargetTimer -= dt;
        if (retargetTimer <= 0f) PickSpeed();

        // hướng mong muốn: hướng đang bơi pha thêm nhiễu lượn
        float nz = (Mathf.PerlinNoise(clock * wanderFrequency, noiseSeed) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(noiseSeed, clock * wanderFrequency * 0.7f) - 0.5f) * 2f;
        Vector3 desired = Quaternion.Euler(-ny * wanderPitch, nz * wanderAngle, 0f) * (heading * Vector3.forward);

        // giới hạn góc ngóc / chúi
        float horiz = new Vector2(desired.x, desired.z).magnitude;
        if (horiz < 1e-4f)
        {
            desired = heading * Vector3.forward;
            horiz = new Vector2(desired.x, desired.z).magnitude;
        }

        float maxY = horiz * Mathf.Tan(maxPitch * Mathf.Deg2Rad);
        desired.y = Mathf.Clamp(desired.y, -maxY, maxY);
        desired.Normalize();

        // quay đầu mượt: bước quay tỉ lệ góc còn lại (ease-out) nhưng không quá turnSpeed
        var want = Quaternion.LookRotation(desired, Vector3.up);
        float remaining = Quaternion.Angle(heading, want);
        float step = Mathf.Min(turnSpeed * dt, remaining * (1f - Mathf.Exp(-turnResponse * dt)));
        float yawBefore = heading.eulerAngles.y;
        heading = Quaternion.RotateTowards(heading, want, step);
        float yawRate = Mathf.DeltaAngle(yawBefore, heading.eulerAngles.y) / dt; // độ/giây, + = rẽ phải
        float bankTarget = Mathf.Clamp(-yawRate / turnSpeed, -1f, 1f) * bankAngle; // rẽ phải -> nghiêng phải (roll âm)
        bank = Mathf.Lerp(bank, bankTarget, 1f - Mathf.Exp(-4f * dt));

        // tốc độ: chậm lại khi phải rẽ gấp
        float turnFactor = 1f - turnSlowdown * Mathf.Clamp01(remaining / 120f);
        v = Mathf.MoveTowards(v, targetSpeed * turnFactor, acceleration * dt);
        // loài theo nhịp (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed): quãng đường = tốc độ gốc × hệ số nhịp (1 khi idle); tốc độ gốc vẫn tăng / giảm dần và là Speed báo cho animator
        float move = fishAnimator && fishAnimator.IsPulseDriven ? v * fishAnimator.MoveSpeedMultiplier : v;
        pos += heading * Vector3.forward * (move * dt);

        wobblePhase += dt * wobbleFrequency * Mathf.PI * 2f;
        wobbleOffset = Vector3.up * (Mathf.Sin(wobblePhase) * wobbleAmplitude);

        transform.SetPositionAndRotation(pos + wobbleOffset, heading * Quaternion.AngleAxis(bank, Vector3.forward));

        if (fishAnimator) fishAnimator.Speed = v / Mathf.Max(speed, 0.01f);
    }

    private void PickSpeed()
    {
        targetSpeed = Mathf.Max(0.05f, speed * (1f + Random.Range(-speedVariation, speedVariation)));
        retargetTimer = Random.Range(retargetInterval.x, retargetInterval.y);
    }
}
