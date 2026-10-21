using Core.Manager;
using NUnit.Framework;

/// Bộ đếm phiên level cho level_background / level_complete / level_fail (spec mục 14).
public class FtueSessionStatsTests
{
    [Test]
    public void Reset_ClearsEverything()
    {
        var s = new LevelSessionStats { Taps = 4, DeniedTaps = 2, HoldingMax = 3, HoldingUsed = 1, IdleHints = 1, BackgroundSent = true };
        s.Reset(10f);
        Assert.AreEqual(0, s.Taps + s.DeniedTaps + s.HoldingMax + s.HoldingUsed + s.IdleHints);
        Assert.IsFalse(s.BackgroundSent);
        Assert.AreEqual(0, s.DurationSec(10f));
    }

    [Test]
    public void OnHolding_TracksCurrentAndMax()
    {
        var s = new LevelSessionStats();
        s.Reset(0f);
        s.OnHolding(2, 5);
        s.OnHolding(4, 5);
        Assert.AreEqual(1, s.HoldingUsed);
        Assert.AreEqual(3, s.HoldingMax);
    }

    [Test]
    public void DurationAndIdle_InWholeSeconds()
    {
        var s = new LevelSessionStats();
        s.Reset(100f);
        s.OnInput(130.4f);
        Assert.AreEqual(41, s.DurationSec(141.2f));
        Assert.AreEqual(10, s.IdleSec(141.2f));   // 10,8 s làm tròn xuống như DurationSec
    }
}
