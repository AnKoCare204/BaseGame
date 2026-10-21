using System.Linq;
using Core.GamePlay;
using NUnit.Framework;

/// BoardModel.FindMatchingHint: cá gợi ý phải khớp bể đang mở, ưu tiên bể thiếu ít nhất (spec mục 13).
public class FtueHintTests
{
    private static SoftBubbleData Bubble(params int[] ids) =>
        new SoftBubbleData { FishDataArray = ids.Select(id => new FishData { FishId = id }).ToArray(), Radius = 2.3f };

    private static BoardModel Model(int[] queue, params SoftBubbleData[] bubbles) =>
        new BoardModel(LevelData.Create(1, LevelDifficulty.Normal, queue, bubbles), 2, 4, 5, 7, 15);

    [Test]
    public void PicksFishOfTankMissingLeast()
    {
        BoardModel m = Model(new[] { 0, 7, 0, 7 }, Bubble(0, 0, 0), Bubble(7, 7, 7), Bubble(0, 0, 0), Bubble(7, 7, 7));
        TankEntry tank7 = m.Tanks.First(t => t.FishId == 7);
        m.ReserveTank(tank7);
        m.ArriveAtTank(tank7);   // bể 7 còn thiếu 2, bể 0 thiếu 3
        Assert.IsTrue(m.FindMatchingHint(null, out BubbleEntry bubble, out int index));
        Assert.AreEqual(7, bubble.FishIdAt(index));
    }

    [Test]
    public void NoMatchingFish_ReturnsFalse()
    {
        BoardModel m = Model(new[] { 0, 7, 11 }, Bubble(11, 11, 11));
        Assert.IsFalse(m.FindMatchingHint(null, out _, out _));
    }

    [Test]
    public void HiddenFish_NeverPicked()
    {
        var hidden = new SoftBubbleData { FishDataArray = new[] { new FishData { FishId = 0, isHidden = true } }, Radius = 2f };
        BoardModel m = Model(new[] { 0, 7 }, hidden);
        Assert.IsFalse(m.FindMatchingHint(null, out _, out _));
    }

    /// L1: bể của loài đang đủ người nhận (cá còn bay) hay đang thay đơn thì bấm loài đó là "chờ", không phải bấm sai.
    [Test]
    public void HasOrderFor_TrueWhileItsTankIsFull()
    {
        BoardModel m = Model(new[] { 0, 7 }, Bubble(0, 0, 0), Bubble(0, 7, 7));
        TankEntry tank0 = m.Tanks.First(t => t.FishId == 0);
        for (int i = 0; i < 3; i++) m.ReserveTank(tank0);
        Assert.AreEqual(0, tank0.Missing);
        Assert.IsTrue(m.HasOrderFor(0));
        Assert.IsTrue(m.HasOrderFor(7));
        Assert.IsFalse(m.HasOrderFor(11));
    }

    [Test]
    public void TankFilterRejectsAll_ReturnsFalse()
    {
        BoardModel m = Model(new[] { 0, 7 }, Bubble(0, 0, 0), Bubble(7, 7, 7));
        Assert.IsFalse(m.FindMatchingHint(_ => false, out _, out _));
    }
}
