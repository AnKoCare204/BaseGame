using NUnit.Framework;
using UnityEngine;

/// Test giới hạn hướng mặt trước của TumbleOrienter (2026-10-02): đồ vật đung đưa qua lại quanh trục Y nhưng mặt trước (+Z model) không bao
/// giờ lệch quá FacingLimit khỏi hướng về camera (−Z world), nên mặt lưng không bao giờ quay ra trước. FacingLimit ≥ 180 = xoay tự do như cũ.
/// Dùng [TestCase] (không [ValueSource]) để chạy được bằng bộ chạy reflection: LevelAddrTestMenu.RunTests("TumbleFacing", tag).
public class TumbleFacingTests
{
    private const float Dt = 1f / 60f;
    private const int TenMinutes = 36000;   // số bước 1/60 s
    private const float Limit = 75f;
    private const float Tolerance = 0.05f;

    private static TumbleOrienter Facing(int seed, float startYaw = 130f)
    {
        var orienter = new TumbleOrienter { FacingLimit = Limit };
        orienter.Reset(Quaternion.Euler(0f, startYaw, 0f), seed);
        return orienter;
    }

    /// Góc (độ, có dấu) trên mặt phẳng ngang giữa hướng về camera và mặt trước của model.
    private static float FacingAngle(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0f;
        return Vector3.SignedAngle(Vector3.back, forward, Vector3.up);
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(42)]
    [TestCase(1234)]
    [TestCase(-99)]
    [TestCase(20261002)]
    public void Step_TenMinutes_FrontStaysWithinFacingLimit(int seed)
    {
        TumbleOrienter orienter = Facing(seed);
        float worst = 0f;
        for (int i = 0; i < TenMinutes; i++) worst = Mathf.Max(worst, Mathf.Abs(FacingAngle(orienter.Step(Dt))));
        Assert.LessOrEqual(worst, Limit + Tolerance, "mặt trước lệch khỏi hướng camera quá giới hạn");
    }

    [TestCase(1)]
    [TestCase(42)]
    [TestCase(20261002)]
    public void Step_TenMinutes_BackNeverFacesCamera(int seed)
    {
        TumbleOrienter orienter = Facing(seed);
        float minDot = 1f;
        for (int i = 0; i < TenMinutes; i++) minDot = Mathf.Min(minDot, Vector3.Dot(orienter.Step(Dt) * Vector3.forward, Vector3.back));
        // lệch ngang tối đa 75°, ngóc / chúi tối đa 20° (TiltLimit) → mặt trước vẫn hướng về camera ít nhất cos 75° × cos 20°
        Assert.Greater(minDot, Mathf.Cos(Limit * Mathf.Deg2Rad) * Mathf.Cos(20f * Mathf.Deg2Rad) - 1e-3f, "mặt lưng quay ra phía camera");
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(42)]
    [TestCase(1234)]
    [TestCase(-99)]
    [TestCase(20261002)]
    public void Step_TenMinutes_SwingsToBothSides(int seed)
    {
        TumbleOrienter orienter = Facing(seed);
        float min = 0f, max = 0f;
        for (int i = 0; i < TenMinutes; i++)
        {
            float angle = FacingAngle(orienter.Step(Dt));
            min = Mathf.Min(min, angle);
            max = Mathf.Max(max, angle);
        }
        Assert.Less(min, -0.6f * Limit, "không đung đưa đủ xa sang một bên");
        Assert.Greater(max, 0.6f * Limit, "không đung đưa đủ xa sang bên kia");
    }

    [TestCase(1)]
    [TestCase(42)]
    [TestCase(20261002)]
    public void Step_AtSixtyFps_NeverJumps(int seed)
    {
        TumbleOrienter orienter = Facing(seed);
        Quaternion previous = orienter.Rotation;
        float worst = 0f;
        for (int i = 0; i < TenMinutes; i++)
        {
            Quaternion current = orienter.Step(Dt);
            worst = Mathf.Max(worst, Quaternion.Angle(previous, current));
            previous = current;
        }
        Assert.LessOrEqual(worst, 3f, "một khung hình xoay quá 3° (giật)");
    }

    [TestCase(130f)]
    [TestCase(-130f)]
    [TestCase(180f)]
    public void Reset_StartInsideLimit_KeepsShellFacing(float startYaw)
    {
        TumbleOrienter orienter = Facing(42, startYaw);
        float expected = FacingAngle(Quaternion.Euler(0f, startYaw, 0f));
        Assert.AreEqual(expected, FacingAngle(orienter.Rotation), 0.5f, "tư thế đầu phải theo hướng ngang của vỏ");
    }

    [TestCase(0f)]
    [TestCase(60f)]
    [TestCase(-80f)]
    public void Reset_StartFacingAway_ClampsIntoLimit(float startYaw)
    {
        TumbleOrienter orienter = Facing(7, startYaw);
        Assert.LessOrEqual(Mathf.Abs(FacingAngle(orienter.Rotation)), Limit + Tolerance);
    }

    [Test]
    public void FacingLimit180_StillFacesEveryDirection()
    {
        var orienter = new TumbleOrienter { FacingLimit = 180f };
        orienter.Reset(Quaternion.Euler(0f, 130f, 0f), 42);
        var seen = new bool[8];   // tám cung 45°
        for (int i = 0; i < TenMinutes; i++)
        {
            float angle = Mathf.Repeat(FacingAngle(orienter.Step(Dt)), 360f);
            seen[Mathf.Min((int)(angle / 45f), 7)] = true;
        }
        for (int sector = 0; sector < 8; sector++) Assert.IsTrue(seen[sector], $"cung {sector * 45}° chưa từng quay tới");
    }
}
