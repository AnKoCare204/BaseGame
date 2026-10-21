using System;
using System.Threading;
using Core.Manager;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

/// <summary>
/// Cá bay từ bóng tới bể / ô chờ theo cảm giác nhặt-và-bay của Match Factory (đo từ gif tham chiếu): nhấc lên phóng to và xoay sang tư thế
/// cầm, rồi lao theo MỘT đường cong cắm thẳng vào ô, thu nhỏ dần, chạm ô bẹp nhẹ. Nhịp / scale / xoay / depth lấy từ curve trong
/// GamePlayGlobalConfig, trục curve là progress 0..2: 0..1 = pha nhấc (FlightLiftDuration giây, cố định), 1..2 = pha bay
/// (FlightTravelBaseDuration + quãng bay / FlightSpeed giây, kẹp [Min, Max]). Quy ước từng curve:
///  • FlightMoveCurve: giá trị 0..1 = phần quãng nhấc (FlightLiftHeight đầu tiên trên đường), 1..2 = phần quãng bay (2 = ô). Code quy về
///    tỉ lệ quãng đường thật của từng chuyến nên curve không phụ thuộc hình học, chỉ mô tả nhịp.
///  • FlightScaleCurve: trọng số lerp scale bóng → scale ô. FlightScaleOffsetCurve: hệ số nhân thêm (pop).
///  • FlightRotateCurve: trọng số xoay từ tư thế trong bóng sang tư thế cầm; khi đang bay (progress ≥ 1) chúi thêm theo tiếp tuyến × FlightLean.
///  • FlightDepthCurve: kéo về phía camera (đơn vị world), hai đầu nên = 0 để không lệch điểm hạ.
/// Khoảng cách tính theo phần chiều cao màn hình (CameraManager.ScreenHeightWorld) để giữ tỉ lệ trên mọi độ phóng camera.
/// Fly dùng cho ô chờ; vào bể dùng FlyIntoTank (lên miệng bể rồi mới rơi xuống, bộ curve TankFlight* riêng trên progress 0..3).
/// TestFly = bay đơn giản qua các điểm cho trước (Shuffle, cá vào túi), bộ curve SimpleFlight* riêng trên trục 0..1.
/// </summary>
public static class FishFlight
{
    /// <summary>Bẹp lúc chạm ô: scale local của Graphic (X rộng, Y thấp, Z dài) rồi bật về như cũ.</summary>
    private static readonly Vector3 SquashScale = new(1.12f, 0.85f, 1.12f);

    private const float SquashInDuration = 0.04f;
    private const float SquashOutDuration = 0.08f;

    /// <summary>
    /// Bay tới target (anchor; trượt theo ô chờ khi dồn hàng thì đường được dựng lại), qua điểm via nếu có (DropPoint của bể). Kết thúc:
    /// cá đúng vị trí anchor, scale targetScale, giữ tư thế lúc hạ để đàn cá quay dần về hướng nghỉ; Graphic bẹp nhẹ rồi bật lại (không chờ).
    /// </summary>
    public static async UniTask Fly(Fish fish, Transform target, float targetScale, CancellationToken ct, Vector3? via = null)
    {
        if (fish == null || target == null) return;
        Flight flight = new Flight(fish, target, targetScale, via);
#if UNITY_EDITOR
        BezierPathGizmos.Add(flight.Path);
#endif
        try
        {
            await LMotion.Create(0f, flight.Duration, flight.Duration)
                .WithEase(Ease.Linear)
                .Bind(flight, static (time, f) => f.Apply(time))
                .AddTo(fish)
                .ToUniTask(ct);
            if (fish == null) return;
            flight.Finish();
            PlaySquash(fish.Graphic);
        }
        finally
        {
#if UNITY_EDITOR
            BezierPathGizmos.Remove(flight.Path);
#endif
        }
    }

    /// <summary>
    /// Bay vào bể qua miệng bể thay vì cắm thẳng vào điểm đậu (Fly đi xuyên thành bể): nhấc lên như Fly, vòng lên jumpPoint (đỉnh cung,
    /// phía trước-trên miệng bể) chậm dần, lướt qua đỉnh rồi giữ nằm ngang rơi nhanh dần qua dropPoint (miệng bể) xuống target (anchor
    /// trong bể), lún quá một chút rồi nổi lại (nhún nước). Nhịp / scale / xoay / chúi / depth lấy từ các curve TankFlight* trong
    /// GamePlayGlobalConfig, trục curve là progress 0..3: 0..1 = pha nhấc, 1..2 = pha lên (thời gian tính như pha bay của Fly),
    /// 2..3 = pha rơi (TankFlightFallDuration giây). Thiếu jumpPoint / dropPoint (prefab bể chưa gán) thì bay như Fly. Kết thúc như Fly.
    /// </summary>
    public static async UniTask FlyIntoTank(Fish fish, Transform jumpPoint, Transform dropPoint, Transform target, float targetScale, CancellationToken ct)
    {
        if (jumpPoint == null || dropPoint == null)
        {
            await Fly(fish, target, targetScale, ct);
            return;
        }

        if (fish == null || target == null) return;
        TankFlight flight = new TankFlight(fish, jumpPoint, dropPoint, target, targetScale);
#if UNITY_EDITOR
        BezierPathGizmos.Add(flight.RisePath);
        BezierPathGizmos.Add(flight.FallPath);
#endif
        try
        {
            await LMotion.Create(0f, flight.Duration, flight.Duration)
                .WithEase(Ease.Linear)
                .Bind(flight, static (time, f) => f.Apply(time))
                .AddTo(fish)
                .ToUniTask(ct);
            if (fish == null) return;
            flight.Finish();
            PlaySquash(fish.Graphic);
        }
        finally
        {
#if UNITY_EDITOR
            BezierPathGizmos.Remove(flight.RisePath);
            BezierPathGizmos.Remove(flight.FallPath);
#endif
        }
    }

    public static UniTask TestFly(Fish fish, Transform target, Vector3[] points, float targetScale, float duration,
        CancellationToken ct)
    {
        return TestFly(fish, target, points, targetScale, duration, ct, Vector3.down);
    }

    /// <summary>
    /// Bay đơn giản qua các điểm cho trước trong duration giây, đầu quay theo hướng bay: Shuffle (cá đổi bóng) và cá từ ô chờ vào túi.
    /// Nhịp / scale / depth lấy từ các curve SimpleFlight* (trục 0..1) trong GamePlayGlobalConfig, không dùng chung nhóm Flight* (trục 0..2).
    /// endForward = hướng đầu cá lúc tới đích: mặc định chúi xuống, bóng (Shuffle) nằm ngang theo chiều bay để FishSchool nhận cá đúng
    /// tư thế nghỉ, không phải xoay lại. Đích dịch chuyển trong lúc bay (bóng mềm trôi) thì điểm cuối kéo dần theo target.
    /// </summary>
    public static async UniTask TestFly(Fish fish, Transform target, Vector3[] points, float targetScale, float duration,
        CancellationToken ct, Vector3 endForward)
    {
        float startScale = fish.transform.localScale.x;
        Vector3 forward1 = fish.transform.forward;
        Vector3 forward4 = endForward.sqrMagnitude > 1e-6f ? endForward.normalized : Vector3.down;
        for (int i = 0; i < points.Length; i++)
        {
            points[i].z = target.position.z;
        }
        Vector3 endAtStart = target.position;
        BezierPath bezierPath = new BezierPath(points, forward1, forward4);
#if UNITY_EDITOR
        BezierPathGizmos.Add(bezierPath);
#endif
        AnimationCurve flightMoveCurve = GamePlayGlobalConfig.Instance.SimpleFlightMoveCurve;
        AnimationCurve flightScaleCurve = GamePlayGlobalConfig.Instance.SimpleFlightScaleCurve;
        AnimationCurve flightDepthCurve = GamePlayGlobalConfig.Instance.SimpleFlightDepthCurve;
        MotionHandle handle = LMotion.Create(0f, 1f, duration)
            .WithEase(Ease.Linear)
            .Bind(FollowBezierPath)
            .AddTo(fish);
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
        }
        finally
        {
#if UNITY_EDITOR
            BezierPathGizmos.Remove(bezierPath);
#endif
        }

        // Delay và tween đếm thời gian riêng nên có thể lệch một frame: chốt tween về đúng điểm cuối (progress 1) trước khi bàn giao cho
        // đàn / bể. Không chốt thì cá còn cách đích một đoạn theo trục sâu (SimpleFlightDepthCurve dốc ở cuối) và đàn phải kéo về, nhìn khựng.
        if (fish != null && handle.IsActive()) handle.TryComplete();
        return;
        void FollowBezierPath(float progress)
        {
            float moveProgress = flightMoveCurve.Evaluate(progress);
            float depthProgress = flightDepthCurve.Evaluate(progress);
            Vector3 drift = target != null ? (target.position - endAtStart) * Mathf.Clamp01(moveProgress) : Vector3.zero;   // đích trôi thì kéo dần theo
            fish.transform.position = bezierPath.GetPoint(moveProgress) + Vector3.back * depthProgress + drift;
            fish.transform.rotation = bezierPath.GetDirection(moveProgress);
            float scaleProgress  = flightScaleCurve.Evaluate(progress);
            fish.transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, targetScale, scaleProgress);
        }
    }

    /// <summary>Trạng thái một chuyến bay: đường, mốc thời gian, tư thế cầm; Apply đặt transform theo thời gian đã trôi.</summary>
    private sealed class Flight
    {
        private readonly GamePlayGlobalConfig cfg = GamePlayGlobalConfig.Instance;
        private readonly Fish fish;
        private readonly Transform target;
        private readonly Vector3[] points;
        private readonly Vector3 startForward;
        private readonly float liftLength;
        private readonly float liftDuration;
        private readonly float travelDuration;
        private readonly float startScale;
        private readonly float targetScale;
        private readonly Quaternion startRot;
        private readonly Quaternion carryRot;
        private readonly Vector3 carryRight;
        private float liftFraction;

        public BezierPath Path { get; }
        public float Duration => liftDuration + travelDuration;

        public Flight(Fish fish, Transform target, float targetScale, Vector3? via)
        {
            this.fish = fish;
            this.target = target;
            this.targetScale = targetScale;
            Transform tr = fish.transform;
            Vector3 start = tr.position;
            Vector3 end = target.position;
            points = via.HasValue ? new[] { start, via.Value, end } : new[] { start, end };

            // Phía ngang (trục X màn hình) của ô so với cá; ô ngay trên / dưới thì giữ phía cá đang nhìn.
            float dx = end.x - start.x;
            float side = Mathf.Abs(dx) > 1e-3f ? Mathf.Sign(dx) : Mathf.Abs(tr.forward.x) > 1e-3f ? Mathf.Sign(tr.forward.x) : 1f;
            Vector3 sideDir = new(side, 0f, 0f);
            startForward = (Vector3.up + sideDir * cfg.FlightLiftTilt).normalized;
            carryRot = Quaternion.LookRotation(sideDir, Vector3.up);
            carryRight = carryRot * Vector3.right;
            startRot = tr.rotation;
            startScale = tr.localScale.x;

            float screen = CameraManager.Instance.ScreenHeightWorld(start);
            liftLength = Mathf.Max(0.01f, cfg.FlightLiftHeight * screen);
            Path = new BezierPath(points, startForward, EndForward(points), LiftTension(points, liftLength));
            liftFraction = LiftFraction(Path, liftLength);
            liftDuration = Mathf.Max(0.01f, cfg.FlightLiftDuration);
            float travelScreens = Mathf.Max(0f, Path.Length - liftLength) / screen;
            travelDuration = Mathf.Clamp(cfg.FlightTravelBaseDuration + travelScreens / Mathf.Max(0.01f, cfg.FlightSpeed),
                cfg.FlightTravelMinDuration, Mathf.Max(cfg.FlightTravelMinDuration, cfg.FlightTravelMaxDuration));
        }

        public void Apply(float time)
        {
            if (fish == null) return;
            if (target != null && (target.position - points[^1]).sqrMagnitude > 1e-6f)
            {
                points[^1] = target.position;
                Path.Set(points, startForward, EndForward(points));
                liftFraction = LiftFraction(Path, liftLength);
            }

            float p = time < liftDuration ? time / liftDuration : 1f + (time - liftDuration) / travelDuration;
            float v = cfg.FlightMoveCurve.Evaluate(p);
            float s = v < 1f ? liftFraction * Mathf.Clamp01(v) : liftFraction + (1f - liftFraction) * Mathf.Clamp01(v - 1f);
            Transform tr = fish.transform;
            tr.position = Path.GetPoint(s) + Vector3.back * cfg.FlightDepthCurve.Evaluate(p);

            // Chúi quanh trục ngang của tư thế cầm (không lăn): đi ngang ≈ 0°, đi thẳng đứng = 90° × FlightLean.
            float pose = Mathf.Clamp01(cfg.FlightRotateCurve.Evaluate(p));
            float pitch = p >= 1f ? -Path.GetTangent(s).y * 90f * cfg.FlightLean * pose : 0f;
            tr.rotation = Quaternion.AngleAxis(pitch, carryRight) * Quaternion.Slerp(startRot, carryRot, pose);

            float scale = Mathf.LerpUnclamped(startScale, targetScale, cfg.FlightScaleCurve.Evaluate(p)) * cfg.FlightScaleOffsetCurve.Evaluate(p);
            tr.localScale = Vector3.one * scale;
        }

        /// <summary>Chốt đúng điểm hạ và scale đích, phòng curve không kết thúc đúng (2, 2) / (2, 1).</summary>
        public void Finish()
        {
            Transform tr = fish.transform;
            tr.position = target != null ? target.position : points[^1];
            tr.localScale = Vector3.one * targetScale;
        }
    }

    /// <summary>
    /// Trạng thái một chuyến bay vào bể: đường lên (chỗ cá → JumpPoint, tới đỉnh thì đi ngang về phía bể) nối đường rơi (JumpPoint →
    /// DropPoint → anchor, cắm thẳng xuống). Hai đường dựng phẳng trên mặt XY (z = 0): camera nhìn dọc Z nên quãng Z không thấy trên màn
    /// hình, để chung vào đường thì nó ăn bớt nhịp bay (JumpPoint lệch hẳn về phía camera). Z nội suy riêng theo các mốc, chỉ quyết định
    /// cá vẽ trước hay sau kính bể: tới DropPoint là đã nằm trong lòng bể.
    /// </summary>
    private sealed class TankFlight
    {
        private readonly GamePlayGlobalConfig cfg = GamePlayGlobalConfig.Instance;
        private readonly Fish fish;
        private readonly Transform jump;
        private readonly Transform drop;
        private readonly Transform target;
        private readonly Vector3[] risePoints = new Vector3[2];
        private readonly Vector3[] fallPoints = new Vector3[3];
        private readonly Vector3 sideDir;
        private readonly Vector3 startForward;
        private readonly float startZ;
        private readonly float liftLength;
        private readonly float liftDuration;
        private readonly float riseDuration;
        private readonly float fallDuration;
        private readonly float startScale;
        private readonly float targetScale;
        private readonly Quaternion startRot;
        private readonly Quaternion carryRot;
        private readonly Vector3 carryRight;
        private Vector3 jumpPos;
        private Vector3 dropPos;
        private Vector3 endPos;
        private float liftFraction;
        private float dropFraction;

        public BezierPath RisePath { get; }
        public BezierPath FallPath { get; }
        public float Duration => liftDuration + riseDuration + fallDuration;

        public TankFlight(Fish fish, Transform jump, Transform drop, Transform target, float targetScale)
        {
            this.fish = fish;
            this.jump = jump;
            this.drop = drop;
            this.target = target;
            this.targetScale = targetScale;
            Transform tr = fish.transform;
            Vector3 start = tr.position;
            jumpPos = jump.position;
            dropPos = drop.position;
            endPos = target.position;
            startZ = start.z;

            // Phía ngang (trục X màn hình) của miệng bể so với cá; bể ngay trên đầu thì giữ phía cá đang nhìn.
            float dx = jumpPos.x - start.x;
            float side = Mathf.Abs(dx) > 1e-3f ? Mathf.Sign(dx) : Mathf.Abs(tr.forward.x) > 1e-3f ? Mathf.Sign(tr.forward.x) : 1f;
            sideDir = new Vector3(side, 0f, 0f);
            startForward = (Vector3.up + sideDir * cfg.FlightLiftTilt).normalized;
            carryRot = Quaternion.LookRotation(sideDir, Vector3.up);
            carryRight = carryRot * Vector3.right;
            startRot = tr.rotation;
            startScale = tr.localScale.x;

            float screen = CameraManager.Instance.ScreenHeightWorld(start);
            liftLength = Mathf.Max(0.01f, cfg.FlightLiftHeight * screen);
            risePoints[0] = Flat(start);
            risePoints[1] = Flat(jumpPos);
            RisePath = new BezierPath { Tension = LiftTension(risePoints, liftLength) };
            FallPath = new BezierPath { Tension = cfg.TankFlightApexRoundness };
            Rebuild();

            liftDuration = Mathf.Max(0.01f, cfg.FlightLiftDuration);
            float riseScreens = Mathf.Max(0f, RisePath.Length - liftLength) / screen;
            riseDuration = Mathf.Clamp(cfg.FlightTravelBaseDuration + riseScreens / Mathf.Max(0.01f, cfg.FlightSpeed),
                cfg.FlightTravelMinDuration, Mathf.Max(cfg.FlightTravelMinDuration, cfg.FlightTravelMaxDuration));
            fallDuration = Mathf.Max(0.01f, cfg.TankFlightFallDuration);
        }

        public void Apply(float time)
        {
            if (fish == null) return;
            if (Moved(jump, ref jumpPos) | Moved(drop, ref dropPos) | Moved(target, ref endPos)) Rebuild();

            float p = time < liftDuration ? time / liftDuration
                : time < liftDuration + riseDuration ? 1f + (time - liftDuration) / riseDuration
                : 2f + (time - liftDuration - riseDuration) / fallDuration;
            float v = cfg.TankFlightMoveCurve.Evaluate(p);
            Vector3 pos;
            Vector3 tangent;
            if (v < 2f)
            {
                float s = v < 1f ? liftFraction * Mathf.Clamp01(v) : liftFraction + (1f - liftFraction) * Mathf.Clamp01(v - 1f);
                pos = RisePath.GetPoint(s);
                tangent = RisePath.GetTangent(s);
                pos.z = Mathf.Lerp(startZ, jumpPos.z, Mathf.SmoothStep(0f, 1f, s));
            }
            else
            {
                // Quá 1 = lún quá điểm đậu theo hướng cắm (nhún nước), curve tự kéo về 1.
                float f = v - 2f;
                float s = Mathf.Clamp01(f);
                pos = FallPath.GetPoint(s);
                tangent = FallPath.GetTangent(s);
                if (f > 1f) pos += tangent * ((f - 1f) * FallPath.Length);
                pos.z = s < dropFraction
                    ? Mathf.Lerp(jumpPos.z, dropPos.z, s / dropFraction)
                    : Mathf.Lerp(dropPos.z, endPos.z, (s - dropFraction) / Mathf.Max(1e-4f, 1f - dropFraction));
            }

            Transform tr = fish.transform;
            tr.position = pos + Vector3.back * cfg.TankFlightDepthCurve.Evaluate(p);

            // Chúi quanh trục ngang của tư thế cầm (không lăn): đi ngang ≈ 0°, đi thẳng đứng = 90° × lean (lean = 0 ở pha rơi: nằm ngang rơi xuống).
            float pose = Mathf.Clamp01(cfg.TankFlightRotateCurve.Evaluate(p));
            float pitch = -tangent.y * 90f * cfg.TankFlightLeanCurve.Evaluate(p) * pose;
            tr.rotation = Quaternion.AngleAxis(pitch, carryRight) * Quaternion.Slerp(startRot, carryRot, pose);

            float scale = Mathf.LerpUnclamped(startScale, targetScale, cfg.TankFlightScaleCurve.Evaluate(p)) * cfg.TankFlightScaleOffsetCurve.Evaluate(p);
            tr.localScale = Vector3.one * scale;
        }

        /// <summary>Chốt đúng điểm đậu và scale đích, phòng curve không kết thúc đúng (3, 3) / (3, 1).</summary>
        public void Finish()
        {
            Transform tr = fish.transform;
            tr.position = target != null ? target.position : endPos;
            tr.localScale = Vector3.one * targetScale;
        }

        /// <summary>Dựng lại hai đường theo vị trí mốc hiện tại (bể / anchor xê dịch giữa chuyến bay).</summary>
        private void Rebuild()
        {
            risePoints[1] = Flat(jumpPos);
            fallPoints[0] = risePoints[1];
            fallPoints[1] = Flat(dropPos);
            fallPoints[2] = Flat(endPos);
            RisePath.Set(risePoints, startForward, sideDir);
            FallPath.Set(fallPoints, sideDir, Vector3.down);
            liftFraction = LiftFraction(RisePath, liftLength);
            dropFraction = Mathf.Clamp(FallPath.GetTAt(1), 1e-4f, 1f);
        }

        /// <summary>Mốc còn sống và đã xê dịch thì cập nhật vị trí nhớ; mốc bị hủy giữa chừng thì giữ vị trí cuối.</summary>
        private static bool Moved(Transform mark, ref Vector3 cached)
        {
            if (mark == null || (mark.position - cached).sqrMagnitude <= 1e-6f) return false;
            cached = mark.position;
            return true;
        }

        private static Vector3 Flat(Vector3 point) => new(point.x, point.y, 0f);
    }

    /// <summary>Hướng cắm vào ô: thẳng đứng, theo chiều từ điểm ngay trước tới ô (ô chờ trên bàn → đi lên; bể có DropPoint trên miệng → rơi xuống).</summary>
    private static Vector3 EndForward(Vector3[] points) => points[^1].y >= points[^2].y ? Vector3.up : Vector3.down;

    /// <summary>
    /// Độ căng cho BezierPath sao cho điểm điều khiển đầu cách chỗ cá ≈ 2 × liftLength theo hướng nhấc (đường vươn lên cỡ quãng nhấc rồi mới
    /// ngoặt về ô); kẹp ≤ 0.5 dây cung để đường không thắt vòng khi ô ở gần.
    /// </summary>
    private static float LiftTension(Vector3[] points, float liftLength)
    {
        float chord = Mathf.Max(1e-3f, (points[1] - points[0]).magnitude);
        return Mathf.Clamp(2f * liftLength / chord, 0.05f, 0.5f);
    }

    /// <summary>Tỉ lệ quãng đường ứng với hết pha nhấc: liftLength đầu tiên trên đường, tối đa nửa đường.</summary>
    private static float LiftFraction(BezierPath path, float liftLength) =>
        Mathf.Clamp(liftLength / Mathf.Max(1e-3f, path.Length), 0.01f, 0.5f);

    /// <summary>Bẹp trên Graphic (không đụng scale gốc vì đàn cá đặt scale đó ngay sau khi hạ), chạy nền không chờ.</summary>
    private static void PlaySquash(Transform graphic)
    {
        Vector3 baseScale = graphic.localScale;
        Vector3 squashed = Vector3.Scale(baseScale, SquashScale);
        LSequence.Create()
            .Append(LMotion.Create(baseScale, squashed, SquashInDuration).WithEase(Ease.OutQuad).BindToLocalScale(graphic))
            .Append(LMotion.Create(squashed, baseScale, SquashOutDuration).WithEase(Ease.OutQuad).BindToLocalScale(graphic))
            .Run()
            .AddTo(graphic);
    }
}
