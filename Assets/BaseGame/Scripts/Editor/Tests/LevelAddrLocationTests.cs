using CoreData;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

/// Quy tắc chọn bản level: bản CDN đã cache, rồi bản APK, rồi bản CDN phải tải.
public class LevelAddrLocationTests
{
    private static IResourceLocation Loc(string id)
    {
        return new ResourceLocationBase("Levels/Level_1", id, "provider", typeof(TextAsset));
    }

    [Test]
    public void RemoteCached_WinsOverLocal()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        LevelLocation location = new LevelLocation(remote, local, true, "");
        Assert.AreSame(remote, location.Preferred);
        Assert.IsTrue(location.IsReadyOffline);
        Assert.AreEqual(LevelSource.Cdn, location.DescribeSource(location.Preferred));
    }

    [Test]
    public void RemoteNotCached_FallsBackToLocal()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        LevelLocation location = new LevelLocation(remote, local, false, "");
        Assert.AreSame(local, location.Preferred);
        Assert.IsTrue(location.IsReadyOffline);
        Assert.AreEqual(LevelSource.Apk, location.DescribeSource(location.Preferred));
    }

    [Test]
    public void OnlyRemote_NotCached_NeedsDownload()
    {
        IResourceLocation remote = Loc("remote");
        LevelLocation location = new LevelLocation(remote, null, false, "");
        Assert.AreSame(remote, location.Preferred);
        Assert.IsFalse(location.IsReadyOffline);
        Assert.IsTrue(location.HasAny);
    }

    [Test]
    public void Nothing_HasNoPreferred()
    {
        LevelLocation location = default;
        Assert.IsNull(location.Preferred);
        Assert.IsFalse(location.HasAny);
        Assert.IsFalse(location.IsReadyOffline);
        Assert.AreEqual("", location.DescribeSource(null));
    }

    [Test]
    public void TestLocation_DescribesVersion()
    {
        IResourceLocation remote = Loc("remote");
        LevelLocation location = new LevelLocation(remote, null, true, "VersionA");
        Assert.AreEqual("test:VersionA", location.DescribeSource(location.Preferred));
    }

    [Test]
    public void Request_LoopedLevel_DoesNotAllowTest()
    {
        LevelRequest normal = new LevelRequest(12, 12);
        LevelRequest looped = new LevelRequest(160, 73);
        Assert.IsTrue(normal.AllowTest);
        Assert.IsFalse(looped.AllowTest);
        Assert.AreEqual("Levels/Level_73", looped.Address);
        Assert.IsFalse(new LevelRequest(5, 0).IsValid);
    }
}
