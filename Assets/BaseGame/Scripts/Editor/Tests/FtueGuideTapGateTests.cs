using Core.Tutorial;
using NUnit.Framework;

/// Khung chặn bấm vừa hiện chưa nhận chạm ngay: chạm đúp vào "Tap to continue" không được đóng luôn câu kế tiếp (câu luật thua ở L2).
public class FtueGuideTapGateTests
{
    [TestCase(10f, 10f, false)]
    [TestCase(10f, 10.2f, false)]
    [TestCase(10f, 10.45f, true)]   // không đặt đúng 10.4f: số float đó nhỏ hơn 10 + 0.4 một chút
    [TestCase(10f, 13f, true)]
    public void Accepts_OnlyAfterTheMinimumShowTime(float shownAt, float now, bool expected) =>
        Assert.AreEqual(expected, GuideTapGate.Accepts(shownAt, now));
}
