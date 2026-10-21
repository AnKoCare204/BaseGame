using CoreData;
using CoreGame.GameGlobalConfig;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

/// Nguồn của bản biến thể hiện trong log / cheat panel với hậu tố /v{x}; config mang số lần chơi lại vào request.
public class LevelAddrVariantLocationTests
{
    private static IResourceLocation Loc(string id) => new ResourceLocationBase("Levels/Variant/Level_25_3", id, "provider", typeof(TextAsset));

    [Test]
    public void DescribeSource_Variant_AddsSuffix()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        LevelLocation location = new LevelLocation(remote, local, true, "").WithVariant(3);

        Assert.AreEqual(3, location.Variant);
        Assert.AreEqual("cdn/v3", location.DescribeSource(remote));
        Assert.AreEqual("apk/v3", location.DescribeSource(local));
    }

    [Test]
    public void DescribeSource_NoVariant_Unchanged()
    {
        IResourceLocation remote = Loc("remote");
        LevelLocation location = new LevelLocation(remote, null, true, "");
        Assert.AreEqual(0, location.Variant);
        Assert.AreEqual(LevelSource.Cdn, location.DescribeSource(remote));
    }

    [Test]
    public void WithVariant_KeepsLocations()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        LevelLocation location = new LevelLocation(remote, local, false, "").WithVariant(2);
        Assert.AreSame(remote, location.Remote);
        Assert.AreSame(local, location.Local);
        Assert.IsFalse(location.RemoteCached);
        Assert.AreSame(local, location.Preferred);
    }

    [TestCase("editor-file", 2, "editor-file/v2")]
    [TestCase("cdn", 0, "cdn")]
    [TestCase("apk", -1, "apk")]
    public void WithVariant_Source(string source, int variant, string expected)
    {
        Assert.AreEqual(expected, LevelSource.WithVariant(source, variant));
    }

    [Test]
    public void LoadResult_DefaultVariant_IsZero()
    {
        Assert.AreEqual(0, new LevelLoadResult(null, "cdn").Variant);
        Assert.AreEqual(4, new LevelLoadResult(null, "cdn/v4", 4).Variant);
    }

    [Test]
    public void Config_ToRequest_CarriesRetryCount()
    {
        LevelConfig config = new LevelConfig { Level = 25, SourceLevel = 25, RetryCount = 3 };
        LevelRequest request = config.ToRequest();
        Assert.AreEqual(25, request.Level);
        Assert.AreEqual(25, request.SourceLevel);
        Assert.AreEqual(3, request.RetryCount);
    }
}
