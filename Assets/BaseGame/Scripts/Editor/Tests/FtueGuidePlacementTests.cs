using System.Collections.Generic;
using Core.Tutorial;
using NUnit.Framework;
using UnityEngine;

/// Chỗ đặt khung chữ (spec bản sửa mục 3): dưới phần sáng → trên → khoảng trống lớn nhất; kẹp ngang, lật linh vật; không vừa thì giao ít nhất.
public class FtueGuidePlacementTests
{
    private static readonly Rect Safe = new Rect(0f, 0f, 1000f, 2000f);
    private static readonly Vector2 Size = new Vector2(800f, 200f);
    private const float Gap = 20f;
    private static readonly List<Rect> None = new List<Rect>();

    [Test]
    public void Below_WhenThereIsRoom()
    {
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, new List<Rect> { new Rect(300f, 1000f, 200f, 200f) }, None, Gap);
        Assert.AreEqual(780f, r.Rect.yMin, 0.01f);
        Assert.AreEqual(0f, r.Rect.xMin, 0.01f);
        Assert.IsFalse(r.MascotOnRight);
    }

    [Test]
    public void Above_WhenBelowLeavesTheScreen()
    {
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, new List<Rect> { new Rect(300f, 100f, 200f, 200f) }, None, Gap);
        Assert.AreEqual(320f, r.Rect.yMin, 0.01f);
    }

    [Test]
    public void Above_WhenBelowHitsAvoidedRow()
    {
        var lit = new List<Rect> { new Rect(300f, 1000f, 200f, 200f) };
        var avoid = new List<Rect> { new Rect(0f, 700f, 1000f, 150f) };
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, lit, avoid, Gap);
        Assert.AreEqual(1220f, r.Rect.yMin, 0.01f);
    }

    [Test]
    public void LargestFreeBand_WhenAboveAndBelowBlocked()
    {
        var lit = new List<Rect> { new Rect(300f, 900f, 200f, 200f) };
        var avoid = new List<Rect> { new Rect(0f, 600f, 1000f, 250f), new Rect(0f, 1150f, 1000f, 300f) };
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, lit, avoid, Gap);
        Assert.AreEqual(200f, r.Rect.yMin, 0.01f);   // khoảng trống 0..600, khung giữa khoảng
    }

    [Test]
    public void ClampsRight_AndPutsMascotRight()
    {
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, new List<Rect> { new Rect(900f, 1000f, 100f, 100f) }, None, Gap);
        Assert.AreEqual(200f, r.Rect.xMin, 0.01f);
        Assert.IsTrue(r.MascotOnRight);
    }

    [Test]
    public void ClampsLeft_KeepsMascotLeft()
    {
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, new List<Rect> { new Rect(0f, 1000f, 100f, 100f) }, None, Gap);
        Assert.AreEqual(0f, r.Rect.xMin, 0.01f);
        Assert.IsFalse(r.MascotOnRight);
    }

    [Test]
    public void NothingFits_PicksLeastOverlap()
    {
        var safe = new Rect(0f, 0f, 1000f, 300f);
        GuidePlacementResult r = GuidePlacement.Choose(safe, Size, new List<Rect> { new Rect(300f, 100f, 200f, 100f) }, None, Gap);
        Assert.GreaterOrEqual(r.Rect.yMin, safe.yMin - 0.01f);
        Assert.LessOrEqual(r.Rect.yMax, safe.yMax + 0.01f);
        Assert.AreEqual(0f, r.Rect.yMin, 0.01f);   // dưới (kẹp lên mép) và trên giao bằng nhau: lấy chỗ thử trước
    }

    [Test]
    public void NoLit_UsesLargestFreeBand_CenteredOnScreen()
    {
        GuidePlacementResult r = GuidePlacement.Choose(Safe, Size, None, new List<Rect> { new Rect(0f, 1500f, 1000f, 500f) }, Gap);
        Assert.AreEqual(650f, r.Rect.yMin, 0.01f);
        Assert.AreEqual(100f, r.Rect.xMin, 0.01f);
    }
}
