using System.Threading;
using Core.Manager;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

/// <summary>
/// Bản bay thứ hai, theo cảm giác của Fish Sort Puzzle - Bubble Jam (đo từng khung trên gif tham chiếu 30 fps). Khác FishFlight: không có
/// pha nhấc đi trước, cá lấy đà TẠI CHỖ (phóng to, quay sang tư thế bay) rồi vọt đi ngay, cả chuyến ngắn hơn hẳn (≈ 0.5 s).
///  • Fly (ô chờ), progress 0..3: 0..1 = lấy đà (Flight2AnticipationDuration), 1..2 = bay (Flight2TravelBaseDuration + quãng bay /
///    Flight2Speed, kẹp [Min, Max]), 2..3 = hạ (Flight2LandDuration). Gif: bay gần như thẳng, nhịp OutQuad (vọt nhanh, tới ô thì chậm),
///    cá nằm ngang và giữ to ≈ 1.3× suốt chuyến, tới ô rồi mới thu nhỏ tại chỗ.
///  • FlyIntoTank, progress 0..3: 0..1 = lấy đà, 1..2 = lên tới jumpPoint (Flight2TankRiseBaseDuration + quãng lên / Flight2TankRiseSpeed,
///    kẹp [Min, Max]), 2..3 = rơi + nhún nước (Flight2TankFallDuration). Gif: lên với tốc độ gần đều (chỉ chậm nhẹ ở đỉnh), cá to ≈ 1.6×,
///    mũi chúi theo đường bay; lượn qua đỉnh rồi cắm xuống bể rất nhanh, vừa rơi vừa thu nhỏ.
/// Mọi curve nằm trong GamePlayGlobalConfig nhóm Flight2*; giá trị của MoveCurve là tỉ lệ quãng đường thật của từng chuyến nên curve chỉ
/// mô tả nhịp, không phụ thuộc hình học. Khoảng cách tính theo phần chiều cao màn hình (CameraManager.ScreenHeightWorld).
/// API giống hệt FishFlight; BoardManager chọn bản theo GamePlayGlobalConfig.UseFishFlight2.
/// </summary>
public static class FishFlight2
{
    /// <summary>Progress cuối của cả hai kiểu bay (3 pha).</summary>
    private const float EndProgress = 3f;

    /// <summary>
    /// Bay tới target (anchor; trượt theo ô chờ khi dồn hàng thì đường được dựng lại), qua điểm via nếu có. Kết thúc: cá đúng vị trí anchor,
    /// scale targetScale, nằm ngang nhìn về phía đã bay để đàn cá quay dần về hướng nghỉ.
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
        }
        finally
        {
#if UNITY_EDITOR
            BezierPathGizmos.Remove(flight.Path);
#endif
        }
    }

    /// <summary>
    /// Bay vào bể qua miệng bể: lấy đà tại chỗ, vọt lên jumpPoint (đỉnh cung, phía trước-trên miệng bể) mũi chúi theo đường bay, lượn qua
    /// đỉnh rồi cắm qua dropPoint (miệng bể) xuống target (anchor trong bể), lún quá một chút rồi nổi lại (nhún nước). Thiếu jumpPoint /
    /// dropPoint (prefab bể chưa gán) thì bay như Fly. Kết thúc như Fly.
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
        }
        finally
        {
#if UNITY_EDITOR
            BezierPathGizmos.Remove(flight.RisePath);
            BezierPathGizmos.Remove(flight.FallPath);
#endif
        }
    }

    /// <summary>Trạng thái một chuyến bay tới ô chờ: đường, mốc thời gian, tư thế; Apply đặt transform theo thời gian đã trôi.</summary>
    private sealed class Flight
    {
        private readonly GamePlayGlobalConfig cfg = GamePlayGlobalConfig.Instance;
        private readonly Fish fish;
        private readonly Transform target;
        private readonly Vector3[] points;
        private readonly float side;
        private readonly float anticipationDuration;
        private readonly float travelDuration;
        private readonly float landDuration;
        private readonly float startScale;
        private readonly float targetScale;
        private readonly Quaternion startRot;

        public BezierPath Path { get; }
        public float Duration => anticipationDuration + travelDuration + landDuration;

        public Flight(Fish fish, Transform target, float targetScale, Vector3? via)
        {
            this.fish = fish;
            this.target = target;
            this.targetScale = targetScale;
            Transform tr = fish.transform;
            Vector3 start = tr.position;
            Vector3 end = target.position;
            points = via.HasValue ? new[] { start, via.Value, end } : new[] { start, end };
            side = Side(tr, end.x - start.x);
            startRot = tr.rotation;
            startScale = tr.localScale.x;

            Path = new BezierPath(points, LaunchForward(points, cfg.Flight2LaunchUp));
            float screen = CameraManager.Instance.ScreenHeightWorld(start);
            anticipationDuration = Mathf.Max(0.01f, cfg.Flight2AnticipationDuration);
            travelDuration = Mathf.Clamp(cfg.Flight2TravelBaseDuration + Path.Length / screen / Mathf.Max(0.01f, cfg.Flight2Speed),
                cfg.Flight2TravelMinDuration, Mathf.Max(cfg.Flight2TravelMinDuration, cfg.Flight2TravelMaxDuration));
            landDuration = Mathf.Max(0.01f, cfg.Flight2LandDuration);
        }

        public void Apply(float time)
        {
            if (fish == null) return;
            if (target != null && (target.position - points[^1]).sqrMagnitude > 1e-6f)
            {
                points[^1] = target.position;
                Path.Set(points, LaunchForward(points, cfg.Flight2LaunchUp));
            }

            float p = Progress(time, anticipationDuration, travelDuration, landDuration);
            float s = Mathf.Clamp01(cfg.Flight2MoveCurve.Evaluate(p));
            Transform tr = fish.transform;
            tr.position = Path.GetPoint(s) + Vector3.back * cfg.Flight2DepthCurve.Evaluate(p);
            tr.rotation = Pose(startRot, side, cfg.Flight2RotateCurve.Evaluate(p), Path.GetTangent(s), cfg.Flight2Lean, cfg.Flight2CarryYaw * YawWeight(p));

            float scale = Mathf.LerpUnclamped(startScale, targetScale, cfg.Flight2ScaleCurve.Evaluate(p)) * cfg.Flight2ScaleOffsetCurve.Evaluate(p);
            tr.localScale = Vector3.one * scale;
        }

        /// <summary>Chốt đúng điểm hạ và scale đích, phòng curve không kết thúc đúng (3, 1).</summary>
        public void Finish()
        {
            Transform tr = fish.transform;
            tr.position = target != null ? target.position : points[^1];
            tr.localScale = Vector3.one * targetScale;
        }
    }

    /// <summary>
    /// Trạng thái một chuyến bay vào bể: đường lên (chỗ cá → JumpPoint, tới đỉnh thì đi ngang về phía bể) nối đường rơi (JumpPoint →
    /// DropPoint → anchor, cắm thẳng xuống). Như FishFlight, hai đường dựng phẳng trên mặt XY (z = 0) vì camera nhìn dọc Z; Z nội suy
    /// riêng theo các mốc, chỉ quyết định cá vẽ trước hay sau kính bể: tới DropPoint là đã nằm trong lòng bể.
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
        private readonly float side;
        private readonly Vector3 sideDir;
        private readonly float startZ;
        private readonly float anticipationDuration;
        private readonly float riseDuration;
        private readonly float fallDuration;
        private readonly float startScale;
        private readonly float targetScale;
        private readonly Quaternion startRot;
        private Vector3 jumpPos;
        private Vector3 dropPos;
        private Vector3 endPos;
        private float dropFraction;

        public BezierPath RisePath { get; }
        public BezierPath FallPath { get; }
        public float Duration => anticipationDuration + riseDuration + fallDuration;

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
            side = Side(tr, jumpPos.x - start.x);
            sideDir = new Vector3(side, 0f, 0f);
            startRot = tr.rotation;
            startScale = tr.localScale.x;

            risePoints[0] = Flat(start);
            RisePath = new BezierPath { Tension = cfg.Flight2TankRiseTension };
            FallPath = new BezierPath { Tension = cfg.Flight2TankApexRoundness };
            Rebuild();

            float screen = CameraManager.Instance.ScreenHeightWorld(start);
            anticipationDuration = Mathf.Max(0.01f, cfg.Flight2AnticipationDuration);
            riseDuration = Mathf.Clamp(cfg.Flight2TankRiseBaseDuration + RisePath.Length / screen / Mathf.Max(0.01f, cfg.Flight2TankRiseSpeed),
                cfg.Flight2TankRiseMinDuration, Mathf.Max(cfg.Flight2TankRiseMinDuration, cfg.Flight2TankRiseMaxDuration));
            fallDuration = Mathf.Max(0.01f, cfg.Flight2TankFallDuration);
        }

        public void Apply(float time)
        {
            if (fish == null) return;
            if (Moved(jump, ref jumpPos) | Moved(drop, ref dropPos) | Moved(target, ref endPos)) Rebuild();

            float p = Progress(time, anticipationDuration, riseDuration, fallDuration);
            float v = cfg.Flight2TankMoveCurve.Evaluate(p);
            Vector3 pos;
            Vector3 tangent;
            if (v < 1f)
            {
                float s = Mathf.Clamp01(v);
                pos = RisePath.GetPoint(s);
                tangent = RisePath.GetTangent(s);
                pos.z = Mathf.Lerp(startZ, jumpPos.z, Mathf.SmoothStep(0f, 1f, s));
            }
            else
            {
                // Quá 1 = lún quá điểm đậu theo hướng cắm (nhún nước), curve tự kéo về 1.
                float f = v - 1f;
                float s = Mathf.Clamp01(f);
                pos = FallPath.GetPoint(s);
                tangent = FallPath.GetTangent(s);
                if (f > 1f) pos += tangent * ((f - 1f) * FallPath.Length);
                pos.z = s < dropFraction
                    ? Mathf.Lerp(jumpPos.z, dropPos.z, s / dropFraction)
                    : Mathf.Lerp(dropPos.z, endPos.z, (s - dropFraction) / Mathf.Max(1e-4f, 1f - dropFraction));
            }

            Transform tr = fish.transform;
            tr.position = pos + Vector3.back * cfg.Flight2TankDepthCurve.Evaluate(p);
            tr.rotation = Pose(startRot, side, cfg.Flight2TankRotateCurve.Evaluate(p), tangent, cfg.Flight2TankLeanCurve.Evaluate(p), cfg.Flight2CarryYaw * YawWeight(p));

            float scale = Mathf.LerpUnclamped(startScale, targetScale, cfg.Flight2TankScaleCurve.Evaluate(p)) * cfg.Flight2TankScaleOffsetCurve.Evaluate(p);
            tr.localScale = Vector3.one * scale;
        }

        /// <summary>Chốt đúng điểm đậu và scale đích, phòng curve không kết thúc đúng (3, 2) / (3, 1).</summary>
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
            RisePath.Set(risePoints, LaunchForward(risePoints, cfg.Flight2TankLaunchUp), sideDir);
            FallPath.Set(fallPoints, sideDir, Vector3.down);
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

    /// <summary>Đổi thời gian đã trôi sang progress 0..3 theo thời lượng ba pha.</summary>
    private static float Progress(float time, float first, float second, float third) =>
        time < first ? time / first
        : time < first + second ? 1f + (time - first) / second
        : Mathf.Min(EndProgress, 2f + (time - first - second) / third);

    /// <summary>Phía ngang (trục X màn hình) của đích so với cá: +1 phải, -1 trái; đích ngay trên / dưới thì giữ phía cá đang nhìn.</summary>
    private static float Side(Transform fish, float dx) =>
        Mathf.Abs(dx) > 1e-3f ? Mathf.Sign(dx) : Mathf.Abs(fish.forward.x) > 1e-3f ? Mathf.Sign(fish.forward.x) : 1f;

    /// <summary>
    /// Hướng vọt đi: trộn hướng dây cung (chỗ cá → điểm kế) với hướng thẳng lên theo launchUp (0 = đi thẳng theo dây cung, 1 = bốc thẳng
    /// đứng rồi mới ngoặt). Trả zero (đầu tự do, đường thẳng) khi launchUp = 0 hoặc hai hướng triệt tiêu nhau.
    /// </summary>
    private static Vector3 LaunchForward(Vector3[] points, float launchUp)
    {
        if (launchUp <= 0f) return Vector3.zero;
        Vector3 chord = (points[1] - points[0]).normalized;
        Vector3 forward = chord * (1f - launchUp) + Vector3.up * launchUp;
        return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.zero;
    }

    /// <summary>Độ ngoảnh về camera mạnh dần trong pha lấy đà, giữ suốt pha giữa, về 0 trong pha cuối để cá đậu đúng tư thế nhìn ngang.</summary>
    private static float YawWeight(float p) => Mathf.Clamp01(Mathf.Min(p, EndProgress - p));

    /// <summary>
    /// Tư thế cá: từ tư thế lúc xuất phát quay sang tư thế bay (nhìn ngang về phía side, ngoảnh yaw độ về camera = -Z) theo trọng số pose,
    /// rồi chúi quanh trục ngang của tư thế bay (không lăn) theo góc dốc của tiếp tuyến × lean. Góc dốc lấy từ asin(tangent.y) nên luôn
    /// trong ±90°: đường rơi móc ngược về phía sau cũng không làm cá lật ngửa.
    /// </summary>
    private static Quaternion Pose(Quaternion startRot, float side, float pose, Vector3 tangent, float lean, float yaw)
    {
        pose = Mathf.Clamp01(pose);
        float rad = yaw * Mathf.Deg2Rad;
        Quaternion carry = Quaternion.LookRotation(new Vector3(side * Mathf.Cos(rad), 0f, -Mathf.Sin(rad)), Vector3.up);
        float pitch = -Mathf.Asin(Mathf.Clamp(tangent.y, -1f, 1f)) * Mathf.Rad2Deg * lean * pose;
        return Quaternion.AngleAxis(pitch, carry * Vector3.right) * Quaternion.Slerp(startRot, carry, pose);
    }
}
