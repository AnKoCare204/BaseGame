using NUnit.Framework;
using UnityEngine;

/// Test toán thuần của cá Home. Chạy qua menu Tools/FishSort/Run FishHome Tests hoặc cửa sổ Test Runner (EditMode).
public class FishHomeSwimMathTests
{
    [Test]
    public void StartleIntensity_AtPoint_EqualsStrength()
    {
        Assert.AreEqual(1f, FishHomeSwimMath.StartleIntensity(0f, 3.5f, 1f), 1e-5f);
        Assert.AreEqual(0.5f, FishHomeSwimMath.StartleIntensity(0f, 3.5f, 0.5f), 1e-5f);
    }

    [Test]
    public void StartleIntensity_OutsideRadius_IsZero()
    {
        Assert.AreEqual(0f, FishHomeSwimMath.StartleIntensity(3.5f, 3.5f, 1f));
        Assert.AreEqual(0f, FishHomeSwimMath.StartleIntensity(10f, 3.5f, 1f));
        Assert.AreEqual(0f, FishHomeSwimMath.StartleIntensity(1f, 0f, 1f), "bán kính 0 thì không giật mình");
    }

    [Test]
    public void StartleIntensity_HalfRadius_FallsOffAsSqrt()
    {
        Assert.AreEqual(Mathf.Sqrt(0.5f), FishHomeSwimMath.StartleIntensity(1.75f, 3.5f, 1f), 1e-5f);
    }

    [Test]
    public void IsAhead_DependsOnSwimDirection()
    {
        Assert.IsTrue(FishHomeSwimMath.IsAhead(0f, 1f, 1f), "bơi sang phải, điểm bên phải = phía trước");
        Assert.IsFalse(FishHomeSwimMath.IsAhead(0f, 1f, -1f), "bơi sang trái, điểm bên phải = sau lưng");
        Assert.IsFalse(FishHomeSwimMath.IsAhead(0f, 0f, 1f), "cùng X thì không tính là phía trước");
    }

    [Test]
    public void FleeTargetY_MovesAwayFromPointAndClamps()
    {
        Assert.AreEqual(1.5f, FishHomeSwimMath.FleeTargetY(0f, -1f, 1.5f, 1f, 6f), 1e-5f, "điểm ở dưới thì dạt lên");
        Assert.AreEqual(-1.5f, FishHomeSwimMath.FleeTargetY(0f, 1f, 1.5f, 1f, 6f), 1e-5f, "điểm ở trên thì dạt xuống");
        Assert.AreEqual(0.75f, FishHomeSwimMath.FleeTargetY(0f, -1f, 1.5f, 0.5f, 6f), 1e-5f, "cường độ nửa thì dạt nửa");
        Assert.AreEqual(6f, FishHomeSwimMath.FleeTargetY(5.5f, -1f, 1.5f, 1f, 6f), 1e-5f, "kẹp trong dải");
        Assert.AreEqual(1.5f, FishHomeSwimMath.FleeTargetY(0f, 0f, 1.5f, 1f, 6f), 1e-5f, "cùng độ cao thì dạt lên");
    }

    [Test]
    public void IsFullyOutside_WhenBodyPastFrame()
    {
        Assert.IsFalse(FishHomeSwimMath.IsFullyOutside(3.9f, 3.9f, 1f), "tâm ở mép: nửa thân còn trong");
        Assert.IsTrue(FishHomeSwimMath.IsFullyOutside(4.9f, 3.9f, 1f));
        Assert.IsTrue(FishHomeSwimMath.IsFullyOutside(-5f, 3.9f, 1f));
    }

    [Test]
    public void Entry_PositionAndHeadingFaceInward()
    {
        Assert.AreEqual(5.1f, FishHomeSwimMath.EntryOffsetX(1f, 3.9f, 1f, 0.2f), 1e-5f);
        Assert.AreEqual(-5.1f, FishHomeSwimMath.EntryOffsetX(-1f, 3.9f, 1f, 0.2f), 1e-5f);
        Assert.AreEqual(270f, FishHomeSwimMath.EntryBaseYaw(1f), "vào từ mép phải thì bơi sang trái");
        Assert.AreEqual(90f, FishHomeSwimMath.EntryBaseYaw(-1f), "vào từ mép trái thì bơi sang phải");
    }

    [Test]
    public void LaneZ_SlotsAreSymmetricAroundZero()
    {
        Assert.AreEqual(-4f, FishHomeSwimMath.LaneZ(0, 5, 2f), 1e-5f);
        Assert.AreEqual(0f, FishHomeSwimMath.LaneZ(2, 5, 2f), 1e-5f);
        Assert.AreEqual(4f, FishHomeSwimMath.LaneZ(4, 5, 2f), 1e-5f);
        Assert.AreEqual(-1f, FishHomeSwimMath.LaneZ(0, 2, 2f), 1e-5f, "số chẵn slot thì lệch nửa spacing");
    }

    [Test]
    public void InitialSlot_CentersFishAndLeavesSparesAtEnds()
    {
        // 4 cá, 6 slot: cá 0..3 nhận slot 1..4, slot 0 và 5 dư
        Assert.AreEqual(1, FishHomeSwimMath.InitialSlot(0, 4, 6));
        Assert.AreEqual(4, FishHomeSwimMath.InitialSlot(3, 4, 6));
        Assert.AreEqual(0, FishHomeSwimMath.InitialSlot(0, 3, 3), "không có slot dư thì giữ nguyên chỉ số");
    }

    [Test]
    public void PickFreeSlot_SkipsOccupiedAndCurrent()
    {
        bool[] occupied = { true, false, true, false, false };
        // current = 3: ứng viên là slot 1 và 4
        Assert.AreEqual(1, FishHomeSwimMath.PickFreeSlot(occupied, 3, 0f));
        Assert.AreEqual(4, FishHomeSwimMath.PickFreeSlot(occupied, 3, 0.99f));
    }

    [Test]
    public void PickFreeSlot_NoFreeSlot_ReturnsCurrent()
    {
        bool[] occupied = { true, true, false };
        Assert.AreEqual(2, FishHomeSwimMath.PickFreeSlot(occupied, 2, 0.5f), "slot trống duy nhất là slot hiện tại");
        Assert.AreEqual(1, FishHomeSwimMath.PickFreeSlot(new[] { true, true }, 1, 0.5f));
    }

    [Test]
    public void LocalToViewport_MapsRectAndUvRect()
    {
        Rect rect = new Rect(-540f, -960f, 1080f, 1920f); // RawImage pivot giữa
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        Vector2 center = FishHomeSwimMath.LocalToViewport(Vector2.zero, rect, uv);
        Assert.AreEqual(0.5f, center.x, 1e-5f);
        Assert.AreEqual(0.5f, center.y, 1e-5f);
        Vector2 corner = FishHomeSwimMath.LocalToViewport(new Vector2(540f, 960f), rect, uv);
        Assert.AreEqual(1f, corner.x, 1e-5f);
        Assert.AreEqual(1f, corner.y, 1e-5f);
        Vector2 cropped = FishHomeSwimMath.LocalToViewport(Vector2.zero, rect, new Rect(0.25f, 0f, 0.5f, 1f));
        Assert.AreEqual(0.5f, cropped.x, 1e-5f, "uvRect cắt ngang: giữa rect vẫn là giữa ảnh");
        Vector2 croppedLeft = FishHomeSwimMath.LocalToViewport(new Vector2(-540f, 0f), rect, new Rect(0.25f, 0f, 0.5f, 1f));
        Assert.AreEqual(0.25f, croppedLeft.x, 1e-5f, "mép trái rect = mép trái uvRect");
    }

    [Test]
    public void LocalToViewport_OutsideRect_ExtrapolatesWithoutClamp()
    {
        Rect rect = new Rect(-540f, -960f, 1080f, 1920f);
        Vector2 outside = FishHomeSwimMath.LocalToViewport(new Vector2(-1080f, 2880f), rect, new Rect(0f, 0f, 1f, 1f));
        Assert.AreEqual(-0.5f, outside.x, 1e-5f);
        Assert.AreEqual(2f, outside.y, 1e-5f);
        Vector2 degenerate = FishHomeSwimMath.LocalToViewport(Vector2.zero, new Rect(0f, 0f, 0f, 0f), new Rect(0f, 0f, 1f, 1f));
        Assert.AreEqual(0.5f, degenerate.x, 1e-5f, "rect rỗng thì trả về giữa, không chia cho 0");
    }
}
