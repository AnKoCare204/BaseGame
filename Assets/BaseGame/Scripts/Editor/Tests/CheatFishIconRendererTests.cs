using NUnit.Framework;
using UnityEngine;

/// Test cách CheatFishIconRenderer khung hình model: camera orthographic ôm vừa hộp bao theo chiều nào chật hơn.
/// Chạy qua menu Tools/FishSort/Run Cheat Fish Tests hoặc cửa sổ Test Runner (EditMode).
public class CheatFishIconRendererTests
{
    [Test]
    public void FitOrthographicSize_WideModel_UsesHalfWidthOverAspect()
    {
        Assert.AreEqual(2f, CheatFishIconRenderer.FitOrthographicSize(new Vector3(4f, 1f, 1f), 1f, 1f), 1e-5f);
        Assert.AreEqual(1f, CheatFishIconRenderer.FitOrthographicSize(new Vector3(4f, 1f, 1f), 2f, 1f), 1e-5f);
    }

    [Test]
    public void FitOrthographicSize_TallModel_UsesHalfHeight()
    {
        Assert.AreEqual(1.5f, CheatFishIconRenderer.FitOrthographicSize(new Vector3(1f, 3f, 5f), 1f, 1f), 1e-5f);
    }

    [Test]
    public void FitOrthographicSize_PaddingScalesTheResult()
    {
        Assert.AreEqual(1.1f, CheatFishIconRenderer.FitOrthographicSize(new Vector3(2f, 2f, 2f), 1f, 1.1f), 1e-5f);
    }
}
