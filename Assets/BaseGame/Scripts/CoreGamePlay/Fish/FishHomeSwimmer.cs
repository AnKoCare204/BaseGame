using UnityEngine;

/// Cá trang trí ở màn Home (con của FishHomeRenderManager.RendererGroup): bơi tự do trong khung nhìn của camera render.
/// Khác FishSwimmer (bơi không giới hạn): vùng bơi là hình chữ nhật do FishHomeRenderManager tính từ camera ortho rồi gọi SetArea.
/// Camera nhìn ngang thân nên cá bơi ngang là chính (trái <-> phải): tới gần mép ngang, hoặc thi thoảng giữa chừng, thì lượn vòng
/// chữ U quay đầu; theo chiều dọc cá tự chọn một độ cao đích rồi ngóc / chúi dần tới đó (khung dọc cao nên cần chủ động đổi tầng).
/// Mỗi cá giữ một làn sâu (Z) riêng để không xuyên qua nhau, vòng quay đầu luôn chọn phía đưa cá về lại làn.
/// Yaw / climb giữ dạng số thay vì RotateTowards trên quaternion để cú quay 180° luôn là lượn ngang chứ không lộn qua đầu.
/// Mọi toạ độ là local space của parent (RendererGroup). Quy ước: +Z của transform là đầu cá (giống FishSwimmer).
/// Bật object là bơi (OnEnable), tắt là dừng; bật lại thì bơi tiếp từ chỗ cũ.
///
/// Máy trạng thái (CurrentState): Swim bơi lượn; Rest lơ lửng nghỉ tại chỗ; Flee giật mình bỏ chạy khỏi điểm chạm;
/// Curious tò mò bơi lại gần điểm chạm rồi ngắm; Outside đang ở ngoài khung camera chờ vào lại.
/// Phần tích phân chuyển động (Simulate) dùng chung, mỗi trạng thái chỉ đổi "ý muốn": tốc độ đích, hướng ngang, độ cao đích,
/// hệ số rẽ, có cho vượt mép hay không. Flee / Curious ở FishHomeSwimmer.Reaction.cs, Outside ở FishHomeSwimmer.Outside.cs.
/// Việc liên quan tới nhiều cá (rời khung, tò mò, làn mới) hỏi trọng tài IFishHomeArbiter (manager); không có trọng tài thì chỉ bơi và nghỉ.
public partial class FishHomeSwimmer : MonoBehaviour
{
    public enum State { Swim, Rest, Flee, Curious, Outside }

    [Header("Chuyển động")]
    [SerializeField] private float speed = 1f;                 // đơn vị/giây danh định
    [SerializeField] private float speedVariation = 0.4f;      // ± tỉ lệ mỗi lần đổi tốc độ (0.4 = 60%..140%)
    [SerializeField] private float acceleration = 1f;
    [SerializeField] private float turnSpeed = 120f;           // độ/giây tối đa
    [SerializeField] private float turnResponse = 3f;          // rẽ mượt: tốc độ rẽ tỉ lệ góc còn lại (ease-out), càng lớn càng gắt
    [SerializeField] private float maxPitch = 25f;             // giới hạn ngóc lên / chúi xuống (độ)
    [SerializeField] private float bankAngle = 20f;            // nghiêng thân khi rẽ (độ)
    [SerializeField] private float turnSlowdown = 0.5f;        // giảm tốc khi đang quay đầu (0..1)
    [SerializeField] private Vector2 retargetInterval = new Vector2(2f, 5f); // đổi tốc độ sau bấy nhiêu giây (ngẫu nhiên trong khoảng)

    [Header("Lượn")]
    [SerializeField] private float wanderAngle = 20f;          // nhiễu hướng ngang (độ) để đường bơi cong chứ không thẳng
    [SerializeField] private float wanderPitch = 10f;          // nhiễu hướng dọc (độ)
    [SerializeField] private float wanderFrequency = 0.25f;    // Hz
    [SerializeField] private float wobbleAmplitude = 0.03f;    // nhấp nhô nhẹ theo trục dọc
    [SerializeField] private float wobbleFrequency = 0.7f;

    [Header("Vùng bơi (FishHomeRenderManager ghi đè areaCenter / areaHalfSize / laneZ qua SetArea)")]
    [SerializeField] private Vector2 areaCenter;                                  // tâm vùng bơi, local space của parent
    [SerializeField] private Vector2 areaHalfSize = new Vector2(3.9f, 7f);        // nửa rộng / nửa cao vùng bơi
    [SerializeField] private float laneZ;                                         // làn sâu của cá này
    [SerializeField] private float edgeMargin = 0.6f;                             // cách mép ngang bấy nhiêu thì quay đầu (chừa chỗ cho vòng lượn); độ cao đích cũng né mép trên / dưới bấy nhiêu
    [SerializeField] private Vector2 turnAroundInterval = new Vector2(6f, 14f);   // thi thoảng tự quay đầu giữa chừng sau bấy nhiêu giây
    [SerializeField] private Vector2 changeHeightInterval = new Vector2(4f, 9f);  // chọn độ cao đích mới sau bấy nhiêu giây
    [SerializeField] private float climbGain = 15f;                               // độ ngóc / chúi mỗi đơn vị lệch so với độ cao đích
    [SerializeField] private float laneGain = 20f;                                // độ lệch yaw mỗi đơn vị lệch làn (kéo cá về làn)
    [SerializeField] private float laneDepth = 1.5f;                              // cá không rời làn quá bấy nhiêu (vòng quay đầu dạt theo Z)

    [Header("Model")]
    [SerializeField] private FishModel fishModel;               // lấy kích thước thân (LocalBounds) + animator; Reset tự gán
    [SerializeField] private Vector2 fallbackBodySize = new Vector2(2f, 1.5f); // dài / cao thân khi không có FishModel
    [SerializeField] private float idleAnimSpeed = 0.5f;        // Speed animator khi dừng, cũng là sàn khi bơi rất chậm (cá vẫn vẫy vây)

    [Header("Nghỉ")]
    [SerializeField, Tooltip("Xác suất chuyển sang nghỉ mỗi lần đổi tốc độ (chỉ khi đang bơi thẳng, không sát mép, không vừa hoảng)")]
    private float restChance = 0.35f;
    [SerializeField, Tooltip("Thời gian nghỉ (giây), ngẫu nhiên trong khoảng")]
    private Vector2 restDuration = new Vector2(3f, 8f);
    [SerializeField, Tooltip("Cách nhau tối thiểu giữa hai lần nghỉ (giây), ngẫu nhiên trong khoảng")]
    private Vector2 restInterval = new Vector2(5f, 12f);
    [SerializeField, Tooltip("Tốc độ đích khi lơ lửng nghỉ / ngắm (đơn vị/giây), > 0 để cá vẫn trôi nhẹ thay vì đứng chết")]
    private float hoverSpeed = 0.08f;
    [SerializeField, Tooltip("Nhiễu yaw khi nghỉ (độ): cá khẽ xoay đầu")]
    private float restYawWander = 6f;

    [Header("Hoảng (Startle)")]
    [SerializeField, Tooltip("Hệ số tốc độ đích lúc bỏ chạy ở cường độ 1")]
    private float fleeSpeedMul = 2.5f;
    [SerializeField, Tooltip("Hệ số nhân turnSpeed và turnResponse lúc bỏ chạy (phản ứng gắt hơn)")]
    private float fleeTurnMul = 2f;
    [SerializeField, Tooltip("Thời gian bỏ chạy (giây) ở cường độ 0 và 1")]
    private Vector2 fleeDuration = new Vector2(0.4f, 0.9f);
    [SerializeField, Tooltip("Dạt dọc ra xa điểm chạm bấy nhiêu đơn vị ở cường độ 1")]
    private float fleeVertical = 1.5f;
    [SerializeField, Tooltip("Sau khi hết hoảng, bấy nhiêu giây không nghỉ")]
    private float calmDuration = 3f;
    [SerializeField, Tooltip("Đang bỏ chạy mà tới mép ngang: xác suất chạy luôn ra ngoài khung (nếu trọng tài cho phép)")]
    private float fleeLeaveChance = 0.7f;

    [Header("Tò mò (Curious)")]
    [SerializeField, Tooltip("Xác suất cá tò mò sau mỗi cú nhấn (không tính vệt rê)")]
    private float curiousChance = 0.3f;
    [SerializeField, Tooltip("Chờ bấy nhiêu giây rồi mới bơi lại (cá vừa hoảng thì tính từ lúc hết hoảng)")]
    private Vector2 curiousDelay = new Vector2(1.5f, 3f);
    [SerializeField, Tooltip("Hệ số tốc độ khi bơi lại gần điểm chạm")]
    private float curiousSpeedMul = 0.6f;
    [SerializeField, Tooltip("Cách điểm chạm bấy nhiêu đơn vị thì dừng lại ngắm")]
    private float curiousStopDistance = 1f;
    [SerializeField, Tooltip("Bơi lại quá bấy nhiêu giây chưa tới thì thôi, ngắm tại chỗ")]
    private float curiousTimeout = 6f;
    [SerializeField, Tooltip("Ngắm bấy nhiêu giây (ngẫu nhiên trong khoảng) rồi bơi tiếp")]
    private Vector2 curiousLinger = new Vector2(2f, 4f);

    [Header("Rời khung (Outside)")]
    [SerializeField, Tooltip("Tới mép ngang: xác suất bơi tiếp ra ngoài khung thay vì quay đầu (nếu trọng tài cho phép)")]
    private float leaveChance = 0.4f;
    [SerializeField, Tooltip("Ở ngoài khung bấy nhiêu giây (ngẫu nhiên trong khoảng) rồi vào lại")]
    private Vector2 outsideDuration = new Vector2(2f, 8f);
    [SerializeField, Tooltip("Sau khi vào lại, bấy nhiêu giây (ngẫu nhiên trong khoảng) không rời khung nữa")]
    private Vector2 leaveInterval = new Vector2(10f, 25f);
    [SerializeField, Tooltip("Hệ số tốc độ đích lúc vừa vào lại (rồi tự hạ về bình thường)")]
    private float enterSpeedMul = 1.6f;

    [Header("Gỡ lỗi")]
    [SerializeField, Tooltip("In mỗi lần chuyển trạng thái ra Console với tiền tố [FishHome]")]
    private bool debugLog;

    private const float RestMaxTurnRemaining = 15f;   // đang còn phải quay hơn bấy nhiêu độ thì không nghỉ (giữa cú quay)
    private const float RestEdgeFraction = 0.7f;       // sát mép phía trước hơn bấy nhiêu phần của turnX thì không nghỉ
    private const float LeaveExtraClamp = 0.5f;        // đang rời khung: chốt chặn X nới ra thêm bấy nhiêu sau khi cả thân đã ra ngoài

    private FishProceduralAnimator fishAnimator;
    private IFishHomeArbiter arbiter;
    private Vector3 wobbleOffset;
    private float yaw, baseYaw, climb;   // độ; baseYaw = 90 + 180k (90 = bơi sang +X), không wrap để mang sẵn chiều lượn khi quay đầu
    private float v, targetSpeed, retargetTimer, turnAroundTimer, heightTimer, targetY, bank, wobblePhase, clock, noiseSeed;

    // máy trạng thái
    private float stateTimer;         // đếm ngược của trạng thái hiện tại (nghỉ, hoảng, ngắm / tiếp cận, ở ngoài)
    private float restTimer;          // đếm ngược tới lúc được phép nghỉ lần nữa
    private float calmTimer;          // đếm ngược thời gian bình tĩnh sau hoảng (không nghỉ)
    private float leaveTimer;         // đếm ngược tới lúc được phép rời khung lần nữa
    private bool leaving;             // đang bơi ra ngoài khung (bỏ quy tắc quay đầu ở mép, nới chốt chặn X)
    private float lastYawRemaining;   // góc còn phải quay ở lần tích phân trước (độ)
    private float fleeIntensity;      // cường độ cú hoảng hiện tại (0 = không hoảng)
    private bool curiousPending;      // đã trúng tò mò, đang chờ curiousTimer
    private bool curiousLingering;    // đang ở pha ngắm của Curious
    private Vector2 curiousTarget;    // điểm chạm muốn bơi lại (local XY, đã kẹp vào vùng)
    private float curiousTimer;

    public State CurrentState { get; private set; }
    public float LaneZ => laneZ;
    /// Đang ở ngoài khung hoặc đang bơi ra (đã quyết định rời khung nhưng cả thân chưa ra hết); trọng tài đếm để giữ tối đa MaxOutside con.
    public bool IsOutsideOrLeaving => CurrentState == State.Outside || leaving;
    public float DirX => Mathf.Sin(baseYaw * Mathf.Deg2Rad) >= 0f ? 1f : -1f; // +1 đang bơi sang phải, -1 sang trái

    private float HalfLength => (fishModel != null ? fishModel.LocalBounds.size.z : fallbackBodySize.x) * 0.5f * Mathf.Abs(transform.localScale.z);
    private float HalfHeight => (fishModel != null ? fishModel.LocalBounds.size.y : fallbackBodySize.y) * 0.5f * Mathf.Abs(transform.localScale.y);
    private float LimitX => Mathf.Max(0f, areaHalfSize.x - HalfLength); // tâm cá không vượt quá đây thì cả thân còn trong khung
    private float LimitY => Mathf.Max(0f, areaHalfSize.y - HalfHeight);
    private float HeightRange => Mathf.Max(0f, LimitY - edgeMargin);   // dải độ cao đích (né mép trên / dưới)

    // hook cho các partial file (không cài đặt thì lời gọi bị bỏ)
    partial void CheckOutsideOnRestart();
    partial void TickOutside(float dt);
    partial void BeginOutside();
    partial void TickFlee(float dt, Vector3 pos, Vector2 off);
    partial void TickCurious(float dt, Vector3 pos, Vector2 off);
    partial void TickCuriousPending(float dt, Vector3 pos);

    private void Reset()
    {
        fishModel = GetComponent<FishModel>();
    }

    /// Đặt vùng bơi (local space của parent) và trọng tài. Gọi lúc nào cũng được, cá đang bơi sẽ tự lái vào vùng mới; cá đang ở ngoài giữ trạng thái.
    public void SetArea(Vector2 center, Vector2 halfSize, float lane, IFishHomeArbiter arbiter = null)
    {
        areaCenter = center;
        areaHalfSize = halfSize;
        laneZ = lane;
        this.arbiter = arbiter;
        PickHeight();
    }

    private void OnEnable() => Restart();

    private void OnDisable()
    {
        transform.localPosition -= wobbleOffset;
        wobbleOffset = Vector3.zero;
        if (fishAnimator) fishAnimator.Speed = idleAnimSpeed;
    }

    private void Update() => Simulate(Time.deltaTime);

    /// Bơi tiếp từ vị trí / hướng hiện tại: đầu đang ngả về bên nào thì bơi về bên đó. OnEnable gọi; test gọi trực tiếp.
    /// Đang ở ngoài khung thì vào trạng thái Outside với timer ngắn (FishHomeSwimmer.Outside.cs).
    public void Restart()
    {
        fishAnimator = fishModel != null ? fishModel.Animator : GetComponent<FishProceduralAnimator>();
        if (fishAnimator) fishAnimator.SetIgnoreSimulationFrozen(true); // cá Home không thuộc bàn chơi, vẫn vẫy khi level đóng băng

        Vector3 f = transform.localRotation * Vector3.forward;
        float dirX = Mathf.Abs(f.x) > 1e-3f ? Mathf.Sign(f.x) : (Random.value < 0.5f ? 1f : -1f);
        baseYaw = dirX > 0f ? 90f : 270f;
        yaw = baseYaw + Mathf.DeltaAngle(baseYaw, transform.localEulerAngles.y);
        climb = 0f;
        bank = 0f;
        wobbleOffset = Vector3.zero;
        wobblePhase = Random.value * 10f;
        noiseSeed = Random.value * 1000f;
        turnAroundTimer = Random.Range(turnAroundInterval.x, turnAroundInterval.y);
        restTimer = Random.Range(0f, restInterval.y);     // lệch pha giữa các cá để không cùng nghỉ một lúc
        leaveTimer = Random.Range(0f, leaveInterval.y);   // tương tự cho rời khung
        calmTimer = 0f;
        stateTimer = 0f;
        lastYawRemaining = 0f;
        leaving = false;
        fleeIntensity = 0f;
        curiousPending = false;
        curiousLingering = false;
        PickSpeed();
        PickHeight();
        v = targetSpeed;
        CurrentState = State.Swim;
        CheckOutsideOnRestart();
    }

    /// Một bước mô phỏng dt giây. Update gọi với Time.deltaTime; test gọi tay.
    public void Simulate(float dt)
    {
        if (dt <= 0f) return;
        clock += dt;
        if (CurrentState == State.Outside)
        {
            TickOutside(dt);
            return;
        }

        Vector3 pos = transform.localPosition - wobbleOffset;
        Vector2 off = new Vector2(pos.x - areaCenter.x, pos.y - areaCenter.y);

        calmTimer -= dt;
        restTimer -= dt;
        leaveTimer -= dt;
        if (curiousPending && CurrentState != State.Flee) TickCuriousPending(dt, pos);

        switch (CurrentState)
        {
            case State.Swim: TickSwim(dt, pos, off); break;
            case State.Rest: TickRest(dt); break;
            case State.Flee: TickFlee(dt, pos, off); break;
            case State.Curious: TickCurious(dt, pos, off); break;
        }

        // hướng mong muốn theo trạng thái: nghỉ / ngắm chỉ khẽ xoay đầu, còn lại = hướng bơi + nhiễu lượn + kéo về làn; độ dốc theo độ cao đích + nhiễu
        float nz = (Mathf.PerlinNoise(clock * wanderFrequency, noiseSeed) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(noiseSeed, clock * wanderFrequency * 0.7f) - 0.5f) * 2f;
        float wantYaw, wantClimb, turnMul = 1f;
        bool hovering = CurrentState == State.Rest || (CurrentState == State.Curious && curiousLingering);
        if (hovering)
        {
            wantYaw = baseYaw + nz * restYawWander;
            wantClimb = 0f;
        }
        else
        {
            float laneYaw = Mathf.Clamp(DirX * (pos.z - laneZ) * laneGain, -30f, 30f); // bơi sang phải: yaw tăng thì đầu ngả về -Z
            float wander = CurrentState == State.Curious ? wanderAngle * 0.5f : wanderAngle; // tò mò thì bơi thẳng hơn
            wantYaw = baseYaw + nz * wander + laneYaw;
            wantClimb = Mathf.Clamp((targetY - off.y) * climbGain + ny * wanderPitch, -maxPitch, maxPitch);
            if (CurrentState == State.Flee) turnMul = fleeTurnMul;
        }

        // quay đầu mượt: bước quay tỉ lệ góc còn lại (ease-out) nhưng không quá turnSpeed
        float ease = 1f - Mathf.Exp(-turnResponse * turnMul * dt);
        float maxStep = Mathf.Max(turnSpeed * turnMul, 1f) * dt;
        float remaining = wantYaw - yaw;
        float yawStep = Mathf.Clamp(remaining * ease, -maxStep, maxStep);
        yaw += yawStep;
        climb += Mathf.Clamp((wantClimb - climb) * ease, -maxStep, maxStep);
        float bankTarget = Mathf.Clamp(-yawStep / maxStep, -1f, 1f) * bankAngle; // rẽ phải -> nghiêng phải (roll âm)
        bank = Mathf.Lerp(bank, bankTarget, 1f - Mathf.Exp(-4f * dt));
        lastYawRemaining = remaining;

        // tốc độ: chậm lại khi đang quay đầu
        float turnFactor = 1f - turnSlowdown * Mathf.Clamp01(Mathf.Abs(remaining) / 120f);
        v = Mathf.MoveTowards(v, targetSpeed * turnFactor, acceleration * dt);
        // loài theo nhịp (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed): quãng đường = tốc độ gốc × hệ số nhịp (1 khi idle); tốc độ gốc vẫn tăng / giảm dần và là Speed báo cho animator
        float move = fishAnimator && fishAnimator.IsPulseDriven ? v * fishAnimator.MoveSpeedMultiplier : v;
        Quaternion heading = Quaternion.Euler(-climb, yaw, 0f);
        pos += heading * Vector3.forward * (move * dt);

        // chốt chặn: cả thân luôn nằm trong khung, không rời làn quá xa. Đang rời khung thì nới ra; đang từ ngoài bơi vào thì chưa kẹp
        float offX = pos.x - areaCenter.x;
        float limitX = LimitX;
        if (leaving) limitX = areaHalfSize.x + HalfLength + LeaveExtraClamp;
        else if (Mathf.Abs(offX) > limitX && offX * DirX < 0f) limitX = Mathf.Abs(offX);
        pos.x = areaCenter.x + Mathf.Clamp(offX, -limitX, limitX);
        pos.y = areaCenter.y + Mathf.Clamp(pos.y - areaCenter.y, -LimitY, LimitY);
        pos.z = Mathf.Clamp(pos.z, laneZ - laneDepth, laneZ + laneDepth);

        wobblePhase += dt * wobbleFrequency * Mathf.PI * 2f;
        wobbleOffset = Vector3.up * (Mathf.Sin(wobblePhase) * wobbleAmplitude);

        transform.SetLocalPositionAndRotation(pos + wobbleOffset, heading * Quaternion.AngleAxis(bank, Vector3.forward));

        if (fishAnimator) fishAnimator.Speed = Mathf.Max(v / Mathf.Max(speed, 0.01f), idleAnimSpeed);

        if (leaving && FishHomeSwimMath.IsFullyOutside(pos.x - areaCenter.x, areaHalfSize.x, HalfLength)) BeginOutside();
    }

    private void TickSwim(float dt, Vector3 pos, Vector2 off)
    {
        retargetTimer -= dt;
        if (retargetTimer <= 0f)
        {
            PickSpeed();
            TryRest(off);
            if (CurrentState == State.Rest) return;
        }
        heightTimer -= dt;
        if (heightTimer <= 0f) PickHeight();
        TickEdge(dt, pos, off, leaveChance, true);
    }

    /// Mép ngang: sát mép phía đang bơi tới thì rời khung (nếu được) hoặc bắt buộc quay đầu; timedTurn = thi thoảng tự quay giữa chừng,
    /// trừ khi mép sau lưng còn quá gần (quay xong lại phải quay tiếp). Đang rời khung thì bỏ qua.
    private void TickEdge(float dt, Vector3 pos, Vector2 off, float leaveProb, bool timedTurn)
    {
        if (leaving) return;
        float turnX = Mathf.Max(0f, LimitX - edgeMargin);
        float ahead = off.x * DirX;
        if (timedTurn) turnAroundTimer -= dt;
        if (ahead > turnX)
        {
            if (!TryLeave(leaveProb)) TurnAround(pos);
        }
        else if (timedTurn && turnAroundTimer <= 0f)
        {
            if (ahead > -turnX * 0.5f) TurnAround(pos);
            else turnAroundTimer = 1f;
        }
    }

    /// Thử rời khung: cần trọng tài cho phép, đã qua leaveInterval và trúng xác suất prob.
    private bool TryLeave(float prob)
    {
        if (prob <= 0f || leaveTimer > 0f || arbiter == null || !arbiter.CanLeave() || Random.value >= prob) return false;
        leaving = true;
        Log("rời khung");
        return true;
    }

    /// Thử nghỉ (gọi mỗi lần đổi tốc độ): chỉ khi đang bơi thẳng, không sát mép phía trước, đã qua restInterval, không vừa hoảng, không đang rời khung.
    private void TryRest(Vector2 off)
    {
        if (leaving || restTimer > 0f || calmTimer > 0f || Random.value >= restChance) return;
        if (Mathf.Abs(lastYawRemaining) > RestMaxTurnRemaining) return;
        if (off.x * DirX > Mathf.Max(0f, LimitX - edgeMargin) * RestEdgeFraction) return;
        CurrentState = State.Rest;
        stateTimer = Random.Range(restDuration.x, restDuration.y);
        targetSpeed = hoverSpeed;
        Log("nghỉ");
    }

    private void TickRest(float dt)
    {
        stateTimer -= dt;
        if (stateTimer > 0f) return;
        EndRest();
    }

    private void EndRest()
    {
        CurrentState = State.Swim;
        restTimer = Random.Range(restInterval.x, restInterval.y);
        PickSpeed();
        PickHeight();
        Log("bơi tiếp");
    }

    /// Đổi hướng bơi ngang. Chiều lượn chọn theo làn: yaw tăng từ 90 (sang phải) qua 180 thì đầu cá hướng -Z nên cá dạt về -Z,
    /// từ 270 (sang trái) thì ngược lại; chọn phía kéo cá về lại làn, đang đúng làn thì ngẫu nhiên. Lưu ý: có gọi PickHeight.
    private void TurnAround(Vector3 pos)
    {
        float dz = pos.z - laneZ;
        float side = Mathf.Abs(dz) < 0.1f ? (Random.value < 0.5f ? 1f : -1f) : Mathf.Sign(dz) * DirX;
        baseYaw += side * 180f;

        // gom về quanh 0..360 cho số không lớn dần (yaw dời cùng lượng nên chiều lượn giữ nguyên)
        float wrap = Mathf.Floor(baseYaw / 360f) * 360f;
        baseYaw -= wrap;
        yaw -= wrap;

        turnAroundTimer = Random.Range(turnAroundInterval.x, turnAroundInterval.y);
        PickHeight();
    }

    private void PickSpeed()
    {
        targetSpeed = Mathf.Max(0.05f, speed * (1f + Random.Range(-speedVariation, speedVariation)));
        retargetTimer = Random.Range(retargetInterval.x, retargetInterval.y);
    }

    /// Độ cao đích mới (so với tâm vùng), né mép trên / dưới một khoảng edgeMargin.
    private void PickHeight()
    {
        float range = HeightRange;
        targetY = Random.Range(-range, range);
        heightTimer = Random.Range(changeHeightInterval.x, changeHeightInterval.y);
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log($"[FishHome] {name}: {message}", this);
    }
}
