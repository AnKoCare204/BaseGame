using NUnit.Framework;
using Unity.Mathematics;

/// Test SupportCone (SoftBodyMath.cs): bóng có vật đỡ ngược lực ròng không, xét trong mặt phẳng 2D.
/// Quy ước: SupportDirection = hướng lực đỡ cần có (bóng nổi: (0, -1), tức là phải bị đè xuống); pháp tuyến = hướng từ vật chạm sang bóng;
/// pháp tuyến lệch góc t so với (0, -1) (ngược chiều kim đồng hồ là dương) = (sin t, -cos t). Kết quả tính tay trong từng test.
/// Chạy qua menu Tools/FishSort/Run Soft Bubble Tests.
public class SoftBubbleSupportConeTests
{
    private static readonly float2 Down = new(0f, -1f);

    private static bool Supported(float2 supportDirection, params (float2 normal, float friction)[] contacts)
    {
        var cone = new SupportCone(supportDirection);
        foreach ((float2 normal, float friction) in contacts) cone.Add(normal, friction);
        return cone.IsSupported;
    }

    [Test]
    public void NoContact_IsNotSupported()
    {
        Assert.IsFalse(Supported(Down));
    }

    [Test]
    public void ContactStraightAbove_IsSupported_EvenWithoutFriction()
    {
        Assert.IsTrue(Supported(Down, (new float2(0f, -1f), 0f)));
    }

    [Test]
    public void SingleContactTilted30Degrees_SlidesOff_WithLowFriction()
    {
        // atan(0.05) ~ 2.9 độ < 30 độ: không giữ được, bóng trượt khỏi.
        Assert.IsFalse(Supported(Down, (new float2(0.5f, -0.8660254f), 0.05f)));
    }

    [Test]
    public void SingleContactTilted30Degrees_IsHeldByFriction_WhenFrictionConeReachesDown()
    {
        // atan(0.7) ~ 35 độ >= 30 độ.
        Assert.IsTrue(Supported(Down, (new float2(0.5f, -0.8660254f), 0.7f)));
    }

    [Test]
    public void ArchOfTwoContacts_IsSupported()
    {
        // +-30 độ: tổng hai pháp tuyến = (0, -1.73), hướng thẳng xuống.
        Assert.IsTrue(Supported(Down, (new float2(0.5f, -0.8660254f), 0f), (new float2(-0.5f, -0.8660254f), 0f)));
    }

    [Test]
    public void TwoContactsOnTheSameSide_IsNotSupported()
    {
        // 30 và 60 độ cùng phía: mọi tổ hợp không âm đều có x > 0.
        Assert.IsFalse(Supported(Down, (new float2(0.5f, -0.8660254f), 0f), (new float2(0.8660254f, -0.5f), 0f)));
    }

    [Test]
    public void WedgeBetweenWallAndUpperBubble_IsSupported()
    {
        // Tường bên phải đẩy sang trái (-1, 0), bóng phía trên trái đẩy (0.894, -0.447): 0.894 * tường + 1 * bóng = (0, -0.447).
        Assert.IsTrue(Supported(Down, (new float2(-1f, 0f), 0f), (new float2(0.8944272f, -0.4472136f), 0f)));
    }

    [Test]
    public void VerticalWallAlone_IsNotSupported()
    {
        // Nón ma sát quanh pháp tuyến ngang chỉ lệch atan(0.4) ~ 22 độ, không tới hướng thẳng xuống.
        Assert.IsFalse(Supported(Down, (new float2(-1f, 0f), 0.4f)));
    }

    [Test]
    public void ContactsBelowAndBeside_GiveNoSupport()
    {
        Assert.IsFalse(Supported(Down, (new float2(0f, 1f), 0.2f), (new float2(1f, 0f), 0.2f)));
    }

    [Test]
    public void NearlyOppositeContacts_AcrossTheSupportDirection_AreSupported()
    {
        // t = 5 độ (0.0872, -0.9962) và t = -170 độ (-0.1736, 0.9848): cung qua 0 dài 175 độ.
        // Giải a * n1 + b * n2 = (0, -1): a = 1.99, b = 0.99, đều dương.
        Assert.IsTrue(Supported(Down, (new float2(0.0871557f, -0.9961947f), 0f), (new float2(-0.1736482f, 0.9848078f), 0f)));
    }

    [Test]
    public void NearlyOppositeContacts_SpanningMoreThanHalfATurn_AreNotSupported()
    {
        // t = 10 độ (0.1736, -0.9848) và t = -175 độ (-0.0872, 0.9962): cung qua 0 dài 185 độ.
        // Giải a * n1 + b * n2 = (0, -1): b = 1.99a, a = -1.0 < 0, không có tổ hợp không âm.
        Assert.IsFalse(Supported(Down, (new float2(0.1736482f, -0.9848078f), 0f), (new float2(-0.0871557f, 0.9961947f), 0f)));
    }

    [Test]
    public void ZeroLengthNormal_IsIgnored()
    {
        Assert.IsFalse(Supported(Down, (float2.zero, 1f)));
    }

    [Test]
    public void SinkingBody_IsSupportedByTheFloor()
    {
        // Vật chìm (lực ròng hướng xuống): cần lực đỡ hướng lên, sàn có pháp tuyến (0, 1).
        Assert.IsTrue(Supported(new float2(0f, 1f), (new float2(0f, 1f), 0f)));
        Assert.IsFalse(Supported(new float2(0f, 1f), (new float2(0f, -1f), 0f)));
    }
}
