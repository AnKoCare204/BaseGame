using UnityEngine;

/// Phần phản ứng với chạm của FishHomeSwimmer: giật mình bỏ chạy (Flee) và tò mò bơi lại xem (Curious).
/// Điểm chạm là local XY của RendererGroup, manager đổi từ viewport của camera render (FishHomeRenderManager.Startle).
public partial class FishHomeSwimmer
{
    private const float MinStartle = 0.15f; // cường độ dưới mức này thì coi như không thấy

    /// Đang tò mò hoặc đã trúng tò mò đang chờ tới lượt (trọng tài đếm để giữ tối đa MaxCurious con).
    public bool IsCuriousOrPending => CurrentState == State.Curious || curiousPending;

    /// Giật mình vì điểm chạm trong bán kính radius với độ mạnh strength 0..1 (giảm dần theo khoảng cách).
    /// Điểm ở phía trước thì quay đầu chạy ngược, ở sau lưng thì giữ hướng; dạt dọc ra xa điểm; vọt tốc độ trong dưới một giây.
    /// Đang hoảng mà cú mới yếu hơn thì chỉ làm mới timer (vệt rê không làm cá giật qua lại). Ở ngoài khung thì bỏ qua.
    public void Startle(Vector2 point, float radius, float strength)
    {
        if (CurrentState == State.Outside) return;
        Vector3 pos = transform.localPosition - wobbleOffset;
        float dist = Vector2.Distance(new Vector2(pos.x, pos.y), point);
        float k = FishHomeSwimMath.StartleIntensity(dist, radius, strength);
        if (k < MinStartle) return;

        float duration = Mathf.Lerp(fleeDuration.x, fleeDuration.y, k);
        if (CurrentState == State.Flee && k <= fleeIntensity)
        {
            stateTimer = Mathf.Max(stateTimer, duration);
            return;
        }

        if (FishHomeSwimMath.IsAhead(pos.x, point.x, DirX)) TurnAround(pos); // gọi trước vì TurnAround chọn lại độ cao đích
        targetY = FishHomeSwimMath.FleeTargetY(pos.y - areaCenter.y, point.y - areaCenter.y, fleeVertical, k, HeightRange);
        targetSpeed = speed * Mathf.Lerp(1f, fleeSpeedMul, k);
        fleeIntensity = k;
        stateTimer = duration;
        curiousLingering = false;
        CurrentState = State.Flee;
        Log($"hoảng {k:0.00}");
    }

    partial void TickFlee(float dt, Vector3 pos, Vector2 off)
    {
        stateTimer -= dt;
        TickEdge(dt, pos, off, fleeLeaveChance, false); // chạy tới mép: ra ngoài luôn (nếu được) hoặc quay đầu
        if (stateTimer <= 0f) EndFlee();
    }

    private void EndFlee()
    {
        fleeIntensity = 0f;
        calmTimer = calmDuration;
        CurrentState = State.Swim;
        PickSpeed();
        Log("bình tĩnh lại");
    }

    /// Manager gọi sau mỗi cú nhấn (không tính vệt rê): cá gieo xác suất tò mò, trúng và trọng tài cho phép thì chờ curiousDelay rồi bơi lại.
    /// Đang tò mò / đang chờ thì chỉ đổi điểm đích. Ở ngoài khung hoặc đang rời khung thì bỏ qua.
    public void NoteTapPoint(Vector2 point)
    {
        if (CurrentState == State.Outside || leaving) return;
        Vector2 target = ClampToArea(point);
        if (CurrentState == State.Curious || curiousPending)
        {
            curiousTarget = target;
            return;
        }
        if (Random.value >= curiousChance || arbiter == null || !arbiter.CanBeCurious()) return;
        curiousPending = true;
        curiousTarget = target;
        curiousTimer = Random.Range(curiousDelay.x, curiousDelay.y);
        Log("sẽ tò mò");
    }

    /// Đếm chờ tò mò (file chính chỉ gọi khi không ở Flee, nên cá vừa hoảng thì tính từ lúc hết hoảng).
    partial void TickCuriousPending(float dt, Vector3 pos)
    {
        curiousTimer -= dt;
        if (curiousTimer > 0f) return;
        curiousPending = false;
        if (leaving) return;
        BeginCurious(pos);
    }

    private void BeginCurious(Vector3 pos)
    {
        if (CurrentState == State.Rest) restTimer = Random.Range(restInterval.x, restInterval.y); // nghỉ kết thúc sớm, tính như đã nghỉ xong
        CurrentState = State.Curious;
        curiousLingering = false;
        stateTimer = curiousTimeout;
        if (!FishHomeSwimMath.IsAhead(pos.x, curiousTarget.x, DirX)) TurnAround(pos); // quay đầu trước rồi mới đặt độ cao đích
        targetY = curiousTarget.y - areaCenter.y;
        targetSpeed = speed * curiousSpeedMul;
        Log("tò mò bơi lại");
    }

    partial void TickCurious(float dt, Vector3 pos, Vector2 off)
    {
        stateTimer -= dt;
        if (curiousLingering)
        {
            if (stateTimer <= 0f) EndCurious();
            return;
        }

        // tới nơi, hết giờ, hoặc tới mép ngang phía trước (điểm chạm sát mép mà độ cao chưa tới): dừng ngắm từ đây,
        // không áp luật quay đầu ở mép kẻo cá quay ra quay vào quanh điểm chạm
        float dist = Vector2.Distance(new Vector2(pos.x, pos.y), curiousTarget);
        bool atEdge = off.x * DirX > Mathf.Max(0f, LimitX - edgeMargin);
        if (dist <= curiousStopDistance || stateTimer <= 0f || atEdge)
        {
            curiousLingering = true;
            stateTimer = Random.Range(curiousLinger.x, curiousLinger.y);
            targetSpeed = hoverSpeed;
            Log("ngắm");
            return;
        }

        // điểm đích ở sau lưng (cú nhấn mới hoặc bơi quá đà) thì quay đầu; ở ngay trên / dưới thì không cần
        if (!FishHomeSwimMath.IsAhead(pos.x, curiousTarget.x, DirX) && Mathf.Abs(curiousTarget.x - pos.x) > curiousStopDistance) TurnAround(pos);
        targetY = curiousTarget.y - areaCenter.y;
    }

    private void EndCurious()
    {
        curiousLingering = false;
        CurrentState = State.Swim;
        PickSpeed();
        PickHeight();
        Log("hết tò mò");
    }

    /// Kẹp điểm chạm vào vùng cá tới được (tâm cá trong khung, độ cao né mép).
    private Vector2 ClampToArea(Vector2 point) => new Vector2(
        areaCenter.x + Mathf.Clamp(point.x - areaCenter.x, -LimitX, LimitX),
        areaCenter.y + Mathf.Clamp(point.y - areaCenter.y, -HeightRange, HeightRange));
}
