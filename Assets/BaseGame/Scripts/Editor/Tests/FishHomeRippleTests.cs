using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Test EditMode cho event RippleSpawned của UIWaterRipple (cầu nối gợn sóng -> cá): nhấn bắn một event nhấn, rê tay bắn event vệt
/// theo TrailSpacing. Không cần canvas (không có canvas thì scale = 1) và không cần Targets.
public class FishHomeRippleTests
{
    private GameObject go;
    private UIWaterRipple ripple;
    private readonly List<(Vector2 position, bool isTrail)> received = new();

    [SetUp]
    public void SetUp()
    {
        received.Clear();
        go = new GameObject("Ripple");
        go.AddComponent<Image>();
        ripple = go.AddComponent<UIWaterRipple>();
        ripple.RippleSpawned += (position, isTrail) => received.Add((position, isTrail));
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    private static PointerEventData Pointer(Vector2 position, int id = 0) => new PointerEventData(null) { position = position, pointerId = id };

    [Test]
    public void PointerDown_RaisesTapEvent()
    {
        ripple.OnPointerDown(Pointer(new Vector2(100f, 200f)));
        Assert.AreEqual(1, received.Count);
        Assert.AreEqual(new Vector2(100f, 200f), received[0].position);
        Assert.IsFalse(received[0].isTrail);
    }

    [Test]
    public void Drag_RaisesTrailEventsEveryTrailSpacing()
    {
        ripple.OnPointerDown(Pointer(new Vector2(100f, 200f)));
        ripple.OnDrag(Pointer(new Vector2(200f, 200f))); // TrailSpacing mặc định 40 -> gợn ở 140 và 180
        Assert.AreEqual(3, received.Count, "một nhấn + hai gợn vệt");
        Assert.IsTrue(received[1].isTrail);
        Assert.AreEqual(new Vector2(140f, 200f), received[1].position);
        Assert.IsTrue(received[2].isTrail);
        Assert.AreEqual(new Vector2(180f, 200f), received[2].position);
    }
}
