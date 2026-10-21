using UnityEngine;

/// Phần rời khung của FishHomeSwimmer: trạng thái Outside (đứng yên ngoài khung camera, camera không thấy nên không cần ẩn) và vào lại.
/// Quyết định rời khung nằm ở TryLeave (file chính); ở đây chỉ xử lý từ lúc cả thân đã ra ngoài.
public partial class FishHomeSwimmer
{
    private const float EntryPad = 0.2f;                       // mũi cá cách mép khung bấy nhiêu khi vào lại
    private const float EntryRetargetDelay = 1.5f;             // sau bấy nhiêu giây thì đổi tốc độ vào về bình thường
    private static readonly Vector2 RestartOutsideWait = new Vector2(0.5f, 2f); // bật lại khi đang ở ngoài: chờ bấy nhiêu rồi vào

    /// Bật lại (Restart) khi cá đang ở ngoài khung: cả thân đã ra ngoài thì coi là Outside với timer ngắn để màn hình đầy nhanh;
    /// mới ló một phần (đang rời hoặc đang vào lúc Home đóng) thì không đóng băng nửa thân trong khung: đầu hướng ra thì bơi nốt ra
    /// (thành Outside như thường), đầu hướng vào thì cứ bơi vào (chốt chặn X đã chừa cho cá từ ngoài bơi vào).
    partial void CheckOutsideOnRestart()
    {
        float offX = transform.localPosition.x - areaCenter.x;
        if (Mathf.Abs(offX) <= LimitX) return;
        if (!FishHomeSwimMath.IsFullyOutside(offX, areaHalfSize.x, HalfLength))
        {
            if (offX * DirX > 0f)
            {
                leaving = true;
                Log("bật lại khi đang ló nửa ngoài, bơi nốt ra");
            }
            return;
        }
        CurrentState = State.Outside;
        stateTimer = Random.Range(RestartOutsideWait.x, RestartOutsideWait.y);
        Log("bật lại khi đang ngoài khung, chờ vào");
    }

    /// Cả thân đã ra khỏi khung: đứng yên chờ.
    partial void BeginOutside()
    {
        leaving = false;
        CurrentState = State.Outside;
        stateTimer = Random.Range(outsideDuration.x, outsideDuration.y);
        v = 0f;
        curiousPending = false;
        curiousLingering = false;
        if (fishAnimator) fishAnimator.Speed = idleAnimSpeed;
        Log("ra ngoài khung");
    }

    partial void TickOutside(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f) EnterFrame();
    }

    /// Vào lại khung: phía ngẫu nhiên (cùng phía hay đối diện đều 50%), độ cao mới, làn mới qua trọng tài, hướng vào trong,
    /// tốc độ cao rồi tự hạ; sau đó phải qua leaveInterval mới được rời khung lần nữa.
    private void EnterFrame()
    {
        float side = Random.value < 0.5f ? 1f : -1f;
        if (arbiter != null) laneZ = arbiter.PickLane(this);
        PickHeight();

        baseYaw = FishHomeSwimMath.EntryBaseYaw(side);
        yaw = baseYaw;
        climb = 0f;
        bank = 0f;
        wobbleOffset = Vector3.zero;
        lastYawRemaining = 0f;

        Vector3 pos = new Vector3(
            areaCenter.x + FishHomeSwimMath.EntryOffsetX(side, areaHalfSize.x, HalfLength, EntryPad),
            areaCenter.y + targetY,
            laneZ);
        transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        targetSpeed = speed * enterSpeedMul;
        v = targetSpeed;
        retargetTimer = EntryRetargetDelay;
        leaveTimer = Random.Range(leaveInterval.x, leaveInterval.y);
        turnAroundTimer = Random.Range(turnAroundInterval.x, turnAroundInterval.y);
        calmTimer = 0f;
        CurrentState = State.Swim;
        Log($"vào lại từ mép {(side > 0f ? "phải" : "trái")}, làn {laneZ}");
    }
}
