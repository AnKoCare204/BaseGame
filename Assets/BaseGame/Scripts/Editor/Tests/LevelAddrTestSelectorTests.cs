using System.Collections.Generic;
using CoreData;
using NUnit.Framework;

/// Bộ test nào đang bật: ghi đè của cheat thắng Remote Config; "off" ép bản production.
public class LevelAddrTestSelectorTests
{
    [Test]
    public void NoOverride_FollowsRemote()
    {
        Assert.AreEqual("VersionA", LevelTestSelector.Resolve("", true, "VersionA"));
        Assert.AreEqual("", LevelTestSelector.Resolve("", false, "VersionA"));
        Assert.AreEqual("", LevelTestSelector.Resolve(null, false, "VersionA"));
    }

    [Test]
    public void OverrideOff_BeatsRemote()
    {
        Assert.AreEqual("", LevelTestSelector.Resolve("off", true, "VersionA"));
    }

    [Test]
    public void OverrideVersion_BeatsRemote()
    {
        Assert.AreEqual("VersionB", LevelTestSelector.Resolve("VersionB", true, "VersionA"));
        Assert.AreEqual("VersionB", LevelTestSelector.Resolve("VersionB", false, ""));
    }

    [Test]
    public void RemoteVersion_IsSanitised()
    {
        Assert.AreEqual("VersionA", LevelTestSelector.Resolve("", true, " /VersionA/ "));
        Assert.AreEqual("", LevelTestSelector.Resolve("", true, "../x"));
        Assert.AreEqual("", LevelTestSelector.Resolve("", true, ""));
    }

    [Test]
    public void NormalizeOverride_KeepsValidChoices()
    {
        List<string> available = new List<string> { "VersionA", "VersionB" };
        Assert.AreEqual("", LevelTestSelector.NormalizeOverride("", available));
        Assert.AreEqual("off", LevelTestSelector.NormalizeOverride("off", available));
        Assert.AreEqual("VersionB", LevelTestSelector.NormalizeOverride("VersionB", available));
    }

    [Test]
    public void NormalizeOverride_VersionRemovedFromCdn_FallsBackToRemote()
    {
        List<string> available = new List<string> { "VersionA" };
        Assert.AreEqual("", LevelTestSelector.NormalizeOverride("VersionGone", available));
        Assert.AreEqual("", LevelTestSelector.NormalizeOverride("VersionGone", new List<string>()));
        Assert.AreEqual("", LevelTestSelector.NormalizeOverride("VersionGone", null));
    }

    [Test]
    public void BuildCheatChoices_RemoteThenOffThenVersions()
    {
        List<string> choices = LevelTestSelector.BuildCheatChoices(new List<string> { "VersionA", "VersionB" });
        CollectionAssert.AreEqual(new[] { "", "off", "VersionA", "VersionB" }, choices);
    }

    [Test]
    public void BuildCheatChoices_NoVersions_StillHasRemoteAndOff()
    {
        CollectionAssert.AreEqual(new[] { "", "off" }, LevelTestSelector.BuildCheatChoices(null));
        CollectionAssert.AreEqual(new[] { "", "off" }, LevelTestSelector.BuildCheatChoices(new List<string>()));
    }

    [Test]
    public void BuildCheatChoices_SkipsReservedAndDuplicateNames()
    {
        List<string> choices = LevelTestSelector.BuildCheatChoices(new List<string> { "off", "", "VersionA", "VersionA" });
        CollectionAssert.AreEqual(new[] { "", "off", "VersionA" }, choices);
    }

    [Test]
    public void DescribeChoice_ReadableLabels()
    {
        Assert.AreEqual("Remote config", LevelTestSelector.DescribeChoice(""));
        Assert.AreEqual("Remote config", LevelTestSelector.DescribeChoice(null));
        Assert.AreEqual("Off", LevelTestSelector.DescribeChoice("off"));
        Assert.AreEqual("VersionA", LevelTestSelector.DescribeChoice("VersionA"));
    }

    [Test]
    public void SavedOverrideForDeletedVersion_MapsToRemoteConfigOption()
    {
        List<string> available = new List<string> { "VersionA" };
        List<string> choices = LevelTestSelector.BuildCheatChoices(available);
        string normalized = LevelTestSelector.NormalizeOverride("VersionGone", available);
        Assert.AreEqual(0, choices.IndexOf(normalized), "dropdown phải về option đầu (Remote config)");
        Assert.AreEqual("", LevelTestSelector.Resolve(normalized, false, ""), "không bộ test nào bật, level chơi bản production");
    }
}
