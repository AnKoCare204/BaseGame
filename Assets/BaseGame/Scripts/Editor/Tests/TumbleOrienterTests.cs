using NUnit.Framework;
using UnityEngine;

/// Test TumbleOrienter (đồ vật lắc lư đứng thẳng): mặt trên không bao giờ lệch quá giới hạn nghiêng, quanh trục Y thì quay được mọi phía.
/// Chạy qua menu Tools/FishSort/Run Tumble Tests hoặc cửa sổ Test Runner (EditMode).
public class TumbleOrienterTests
{
    private const float Dt = 1f / 60f;
    private const int TenMinutes = 36000;   // số bước 1/60 s

    // Nghiêng 20° quanh X rồi 20° quanh Z: mặt trên lệch acos(cos 20° × cos 20°) = 27.99° khỏi phương thẳng đứng.
    private const float WorstTilt = 27.99f;
    private const float Tolerance = 0.05f;

    // Tốc độ góc lớn nhất: 2 kênh quanh Y × 40°/s + 2 trục nghiêng × ~20°/s = 120°/s.
    private const float WorstSpeed = 120f;

    private static readonly int[] Seeds = { 1, 7, 42, 1234, -99, 20260928 };

    private static float Tilt(Quaternion rotation) => Vector3.Angle(rotation * Vector3.up, Vector3.up);

    /// Hướng mặt trước (+Z) của model trên mặt phẳng ngang, độ; 0 = +Z world, 90 = +X world.
    private static float Yaw(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private static TumbleOrienter Upright(int seed)
    {
        var orienter = new TumbleOrienter();
        orienter.Reset(Quaternion.Euler(0f, 130f, 0f), seed);
        return orienter;
    }

    [Test]
    public void Step_TenMinutes_TopStaysWithinTiltLimit([ValueSource(nameof(Seeds))] int seed)
    {
        TumbleOrienter orienter = Upright(seed);
        float worst = 0f;
        for (int i = 0; i < TenMinutes; i++) worst = Mathf.Max(worst, Tilt(orienter.Step(Dt)));
        Assert.LessOrEqual(worst, WorstTilt + Tolerance, "mặt trên lệch khỏi phương thẳng đứng quá giới hạn");
    }

    [Test]
    public void Step_TenMinutes_ActuallyRocks([ValueSource(nameof(Seeds))] int seed)
    {
        TumbleOrienter orienter = Upright(seed);
        float worst = 0f;
        for (int i = 0; i < TenMinutes; i++) worst = Mathf.Max(worst, Tilt(orienter.Step(Dt)));
        Assert.GreaterOrEqual(worst, 10f, "đồ vật gần như không nghiêng");
    }

    [Test]
    public void Step_TenMinutes_FacesEveryDirection([ValueSource(nameof(Seeds))] int seed)
    {
        TumbleOrienter orienter = Upright(seed);
        var seen = new bool[8];   // tám cung 45°
        for (int i = 0; i < TenMinutes; i++)
        {
            float yaw = Mathf.Repeat(Yaw(orienter.Step(Dt)), 360f);
            seen[Mathf.Min((int)(yaw / 45f), 7)] = true;
        }

        for (int sector = 0; sector < 8; sector++)
            Assert.IsTrue(seen[sector], $"mặt trước chưa từng quay về cung {sector * 45}°..{sector * 45 + 45}°");
    }

    [Test]
    public void Step_TiltLimitZero_StaysUpright()
    {
        var orienter = new TumbleOrienter { TiltLimit = 0f };
        orienter.Reset(Quaternion.Euler(0f, 130f, 0f), 42);
        float worst = 0f;
        for (int i = 0; i < 3600; i++) worst = Mathf.Max(worst, Tilt(orienter.Step(Dt)));
        Assert.LessOrEqual(worst, Tolerance);
    }

    [Test]
    public void Reset_PitchedStartPose_KeepsOnlyItsYaw()
    {
        var orienter = new TumbleOrienter();
        orienter.Reset(Quaternion.Euler(-70f, 130f, 0f), 42);   // vỏ đang ngóc 70° (bay lên bể)
        Assert.LessOrEqual(Tilt(orienter.Rotation), WorstTilt + Tolerance, "tư thế đầu đã nghiêng quá giới hạn");
        Assert.AreEqual(0f, Mathf.DeltaAngle(130f, Yaw(orienter.Rotation)), 0.1f, "mặt trước không theo hướng của vỏ");
    }

    [Test]
    public void Reset_StartPosePointingStraightUp_TakesYawFromItsSide()
    {
        var orienter = new TumbleOrienter();
        orienter.Reset(Quaternion.Euler(-90f, 40f, 0f), 42);   // mặt trước chĩa thẳng lên: hướng ngang lấy theo sườn phải
        Assert.LessOrEqual(Tilt(orienter.Rotation), WorstTilt + Tolerance);
        Assert.AreEqual(0f, Mathf.DeltaAngle(40f, Yaw(orienter.Rotation)), 0.1f);
    }

    [Test]
    public void Step_AtSixtyFps_NeverJumps([ValueSource(nameof(Seeds))] int seed)
    {
        TumbleOrienter orienter = Upright(seed);
        Quaternion previous = orienter.Rotation;
        float worst = 0f;
        for (int i = 0; i < TenMinutes; i++)
        {
            Quaternion current = orienter.Step(Dt);
            worst = Mathf.Max(worst, Quaternion.Angle(previous, current));
            previous = current;
        }

        Assert.LessOrEqual(worst, WorstSpeed * Dt * 1.5f, "góc giữa hai khung liền nhau quá lớn");
    }

    [Test]
    public void Step_FrameHitch_IsClampedToMaxDt()
    {
        TumbleOrienter orienter = Upright(42);
        for (int i = 0; i < 600; i++) orienter.Step(Dt);
        Quaternion before = orienter.Rotation;
        Quaternion after = orienter.Step(5f);   // khung rớt 5 giây chỉ được tính như MaxDt = 0.1 giây
        Assert.LessOrEqual(Quaternion.Angle(before, after), WorstSpeed * 0.1f);
    }

    [Test]
    public void Step_ZeroDt_KeepsRotation()
    {
        TumbleOrienter orienter = Upright(42);
        for (int i = 0; i < 600; i++) orienter.Step(Dt);
        Quaternion before = orienter.Rotation;
        Assert.AreEqual(0f, Quaternion.Angle(before, orienter.Step(0f)), 1e-4f);
    }

    [Test]
    public void Reset_DifferentSeeds_DriftApart()
    {
        TumbleOrienter a = Upright(1);
        TumbleOrienter b = Upright(2);
        float worst = 0f;
        for (int i = 0; i < 1200; i++) worst = Mathf.Max(worst, Quaternion.Angle(a.Step(Dt), b.Step(Dt)));
        Assert.GreaterOrEqual(worst, 5f, "hai đồ vật khác hạt giống mà xoay giống nhau");
    }
}
