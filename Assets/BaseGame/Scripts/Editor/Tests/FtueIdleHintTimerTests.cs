using Core.Tutorial;
using NUnit.Framework;

/// Đếm thời gian đứng yên của idle hint (spec mục 13): đủ delay thì gợi ý đúng một lần, rồi chờ lần chạm kế; lúc không gợi ý được
/// (pause, modal, kịch bản đang khóa...) thì đếm lại từ đầu.
public class FtueIdleHintTimerTests
{
    [Test]
    public void FiresOnceWhenIdleReachesDelay()
    {
        var timer = new IdleHintTimer();
        Assert.IsFalse(timer.Tick(4.9f, 5f));
        Assert.IsTrue(timer.Tick(0.2f, 5f));
        Assert.IsFalse(timer.Tick(10f, 5f));   // đã gợi ý: chờ chạm, không gợi ý lại
        Assert.IsTrue(timer.WaitingInput);
    }

    [Test]
    public void BlockedTimeDoesNotCount()
    {
        var timer = new IdleHintTimer();
        timer.Tick(4f, 5f);
        Assert.IsFalse(timer.Tick(1f, -1f));   // đang bị chặn: đếm lại
        Assert.IsFalse(timer.Tick(4f, 5f));
        Assert.IsTrue(timer.Tick(1f, 5f));
    }

    [Test]
    public void InputRearmsAndRestartsCount()
    {
        var timer = new IdleHintTimer();
        timer.Tick(6f, 5f);
        timer.OnInput();
        Assert.IsFalse(timer.WaitingInput);
        Assert.IsFalse(timer.Tick(4f, 5f));
        Assert.IsTrue(timer.Tick(1f, 5f));
    }
}
