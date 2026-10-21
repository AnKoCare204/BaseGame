using NUnit.Framework;
using UnityEngine;

/// Test toán thuần của BG gameplay nhiều lớp. Chạy qua menu Tools/FishSort/Run Gameplay Bg Tests hoặc cửa sổ Test Runner (EditMode).
public class GameplayBgMathTests
{
    [Test]
    public void VisibleHalf_IsHalfOfSmallerSide()
    {
        Assert.AreEqual(540f, BgLayerMath.VisibleHalf(1080f, 1560f), 1e-4f, "điện thoại: màn hẹp hơn lớp");
        Assert.AreEqual(720f, BgLayerMath.VisibleHalf(1440f, 1440f), 1e-4f, "iPad: lớp vừa khít bề ngang");
        Assert.AreEqual(0f, BgLayerMath.VisibleHalf(-5f, 100f), 1e-4f, "kích thước âm thì 0");
    }

    [Test]
    public void InRange_ClampsU()
    {
        Assert.AreEqual(8f, BgLayerMath.InRange(new Vector2(8f, 15f), 0f), 1e-4f);
        Assert.AreEqual(15f, BgLayerMath.InRange(new Vector2(8f, 15f), 1f), 1e-4f);
        Assert.AreEqual(11.5f, BgLayerMath.InRange(new Vector2(8f, 15f), 0.5f), 1e-4f);
        Assert.AreEqual(15f, BgLayerMath.InRange(new Vector2(8f, 15f), 3f), 1e-4f);
    }

    [Test]
    public void Wave_QuarterPeriodIsAmplitude()
    {
        Assert.AreEqual(0f, BgLayerMath.Wave(0f, 6f, 0.5f, 0f), 1e-4f);
        Assert.AreEqual(6f, BgLayerMath.Wave(0.5f, 6f, 0.5f, 0f), 1e-4f, "t = 1/4 chu kỳ");
        Assert.AreEqual(-6f, BgLayerMath.Wave(0f, 6f, 0.5f, 0.75f), 1e-4f, "pha 3/4");
    }

    [Test]
    public void PickDirection_SplitsAtHalf()
    {
        Assert.AreEqual(1, BgFishSchoolMath.PickDirection(0.2f));
        Assert.AreEqual(-1, BgFishSchoolMath.PickDirection(0.7f));
    }

    [Test]
    public void EffectiveMargin_RaisedToHalfFish()
    {
        Assert.AreEqual(80f, BgFishSchoolMath.EffectiveMargin(80f, 100f, 1.1f), 1e-4f);
        Assert.AreEqual(55f, BgFishSchoolMath.EffectiveMargin(0f, 100f, 1.1f), 1e-4f, "lề 0 vẫn nâng lên nửa thân con lớn nhất");
    }

    [Test]
    public void StartX_IsBehindAndIsOutsideOnlyAhead()
    {
        Assert.AreEqual(-620f, BgFishSchoolMath.StartX(1, 540f, 80f, 0f), 1e-4f, "bơi sang phải: xuất phát ngoài mép trái");
        Assert.AreEqual(680f, BgFishSchoolMath.StartX(-1, 540f, 80f, 60f), 1e-4f, "bơi sang trái: ngoài mép phải, lùi thêm 60");
        Assert.AreEqual(-620f, BgFishSchoolMath.StartX(1, 540f, 80f, -30f), 1e-4f, "behind âm coi như 0");

        Assert.IsFalse(BgFishSchoolMath.IsOutside(-620f, 1, 540f, 80f), "điểm xuất phát không tính là đã ra");
        Assert.IsFalse(BgFishSchoolMath.IsOutside(620f, 1, 540f, 80f), "đúng mép chưa tính");
        Assert.IsTrue(BgFishSchoolMath.IsOutside(620.1f, 1, 540f, 80f));
        Assert.IsTrue(BgFishSchoolMath.IsOutside(-620.1f, -1, 540f, 80f));
        Assert.IsFalse(BgFishSchoolMath.IsOutside(620.1f, -1, 540f, 80f), "bơi sang trái thì mép phải là phía sau");
    }

    [Test]
    public void SchoolBaseY_MapsBandAroundCenter()
    {
        Assert.AreEqual(-117f, BgFishSchoolMath.SchoolBaseY(new Vector2(0.45f, 0.85f), 0f, 2340f), 1e-3f);
        Assert.AreEqual(819f, BgFishSchoolMath.SchoolBaseY(new Vector2(0.45f, 0.85f), 1f, 2340f), 1e-3f);
    }

    [Test]
    public void FacingSign_FlipsWhenSwimmingRight()
    {
        Assert.AreEqual(-1f, BgFishSchoolMath.FacingSign(1), "fish.png quay trái: bơi phải phải lật");
        Assert.AreEqual(1f, BgFishSchoolMath.FacingSign(-1));
    }

    [Test]
    public void RiseSpeed_BiggerIsFaster()
    {
        var size = new Vector2(14f, 36f);
        var speed = new Vector2(70f, 140f);
        Assert.AreEqual(70f, BgBubbleMath.RiseSpeedForSize(14f, size, speed), 1e-4f);
        Assert.AreEqual(140f, BgBubbleMath.RiseSpeedForSize(36f, size, speed), 1e-4f);
        Assert.Greater(BgBubbleMath.RiseSpeedForSize(30f, size, speed), BgBubbleMath.RiseSpeedForSize(20f, size, speed));
        Assert.AreEqual(140f, BgBubbleMath.RiseSpeedForSize(50f, size, speed), 1e-4f, "ngoài khoảng thì kẹp");
    }

    [Test]
    public void NormalizedY_BottomZeroTopOne()
    {
        Assert.AreEqual(0f, BgBubbleMath.NormalizedY(-1170f, 1170f), 1e-5f);
        Assert.AreEqual(1f, BgBubbleMath.NormalizedY(1170f, 1170f), 1e-5f);
        Assert.AreEqual(0.5f, BgBubbleMath.NormalizedY(0f, 1170f), 1e-5f);
        Assert.AreEqual(0f, BgBubbleMath.NormalizedY(10f, 0f), 1e-5f, "chiều cao 0 không chia cho 0");
    }

    [Test]
    public void AlphaAt_FadesInThenOutAtTop()
    {
        Assert.AreEqual(0f, BgBubbleMath.AlphaAt(0f, 0.3f, 0.4f, 0.4f, 0.25f), 1e-5f, "mới sinh: 0");
        Assert.AreEqual(0.2f, BgBubbleMath.AlphaAt(0.2f, 0.3f, 0.4f, 0.4f, 0.25f), 1e-5f, "nửa thời gian hiện");
        Assert.AreEqual(0.4f, BgBubbleMath.AlphaAt(1f, 0.5f, 0.4f, 0.4f, 0.25f), 1e-5f, "giữa màn: đủ đậm");
        Assert.AreEqual(0.2f, BgBubbleMath.AlphaAt(1f, 0.875f, 0.4f, 0.4f, 0.25f), 1e-5f, "giữa dải mờ trên cùng");
        Assert.AreEqual(0f, BgBubbleMath.AlphaAt(1f, 1f, 0.4f, 0.4f, 0.25f), 1e-5f, "mép trên: 0");
        Assert.AreEqual(0f, BgBubbleMath.AlphaAt(1f, 1.2f, 0.4f, 0.4f, 0.25f), 1e-5f, "trên mép: 0");
        Assert.AreEqual(0.4f, BgBubbleMath.AlphaAt(0f, -0.1f, 0.4f, 0f, 0.25f), 1e-5f, "fadeIn 0: hiện ngay");
        Assert.AreEqual(0f, BgBubbleMath.AlphaAt(1f, 1f, 0.4f, 0.4f, 0f), 1e-5f, "dải 0: tắt đúng ở mép");
    }

    [Test]
    public void ScaleAt_GrowsToTop()
    {
        Assert.AreEqual(1f, BgBubbleMath.ScaleAt(0f, 0.15f), 1e-5f);
        Assert.AreEqual(1.15f, BgBubbleMath.ScaleAt(1f, 0.15f), 1e-5f);
        Assert.AreEqual(1.15f, BgBubbleMath.ScaleAt(2f, 0.15f), 1e-5f, "kẹp");
        Assert.AreEqual(1f, BgBubbleMath.ScaleAt(-1f, 0.15f), 1e-5f, "dưới mép dưới");
    }

    [Test]
    public void Spawn_InsideWidthAndBelowBottom()
    {
        Assert.AreEqual(-500f, BgBubbleMath.SpawnX(0f, 540f, 40f), 1e-4f);
        Assert.AreEqual(500f, BgBubbleMath.SpawnX(1f, 540f, 40f), 1e-4f);
        Assert.AreEqual(0f, BgBubbleMath.SpawnX(1f, 30f, 40f), 1e-4f, "lề lớn hơn nửa bề ngang: sinh giữa");
        Assert.AreEqual(-1206f, BgBubbleMath.SpawnY(1170f, 36f), 1e-4f);
        Assert.IsFalse(BgBubbleMath.IsAboveTop(1206f, 1170f, 36f));
        Assert.IsTrue(BgBubbleMath.IsAboveTop(1206.1f, 1170f, 36f));
    }
}
