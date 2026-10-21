using CoreData;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

/// Level 1-2 (tutorial FTUE) luôn chơi bản bake trong app (Android và iOS), không lấy bản CDN hay bộ test: data hướng dẫn phải khớp
/// code của bản app đang chạy. Level 3 không còn tutorial nên theo luật thường.
public class LevelAddrApkOnlyTests
{
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(201, false)]
    [TestCase(0, false)]
    [TestCase(-1, false)]
    public void IsApkOnly_OnlyLevelsOneToTwo(int sourceLevel, bool expected) =>
        Assert.AreEqual(expected, LevelSourceRule.IsApkOnly(sourceLevel));

    [Test]
    public void ApkOnlyLocation_PlaysTheBakedCopy()
    {
        IResourceLocation local = new ResourceLocationBase("Levels/Level_2", "local", "provider", typeof(TextAsset));
        LevelLocation location = LevelLocation.ApkOnly(local);

        Assert.AreSame(local, location.Preferred);
        Assert.IsNull(location.Remote);
        Assert.IsTrue(location.IsReadyOffline);
        Assert.AreEqual("", location.TestVersion);
        Assert.AreEqual(LevelSource.Apk, location.DescribeSource(location.Preferred));
    }
}
