using CoreData;
using NUnit.Framework;

/// Quy ước address / label của level: phải khớp giữa runtime (tải chunk) và editor (gán label lúc Setup).
public class LevelAddrAddressTests
{
    [TestCase(1, "lvl_0")]
    [TestCase(100, "lvl_0")]
    [TestCase(101, "lvl_1")]
    [TestCase(200, "lvl_1")]
    [TestCase(201, "lvl_2")]
    [TestCase(0, "lvl_0")]
    [TestCase(-3, "lvl_0")]
    public void GetChunkLabel_Boundaries(int level, string expected)
    {
        Assert.AreEqual(expected, LevelAddress.GetChunkLabel(level));
    }

    [Test]
    public void Addresses_FollowConvention()
    {
        Assert.AreEqual("Levels/Level_12", LevelAddress.GetLevelAddress(12));
        Assert.AreEqual("Levels/TEST/VersionA/Level_12", LevelAddress.GetTestAddress(12, "VersionA"));
        Assert.AreEqual("lvl_test_VersionA", LevelAddress.GetTestLabel("VersionA"));
    }

    [Test]
    public void TestLabel_IsRecognisedAndVersionExtracted()
    {
        Assert.IsTrue(LevelAddress.IsTestLabel("lvl_test_VersionA"));
        Assert.IsFalse(LevelAddress.IsTestLabel("lvl_0"));
        Assert.IsFalse(LevelAddress.IsTestLabel(null));
        Assert.AreEqual("VersionA", LevelAddress.GetVersionFromTestLabel("lvl_test_VersionA"));
        Assert.AreEqual("", LevelAddress.GetVersionFromTestLabel("lvl_3"));
    }

    [TestCase("Levels/Level_1", "lvl_0")]
    [TestCase("Levels/Level_123", "lvl_1")]
    [TestCase("Levels/TEST/VersionB/Level_5", "lvl_test_VersionB")]
    [TestCase("Levels/TEST/Level_5", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    [TestCase("Levels/Level_abc", null)]
    public void GetChunkLabelForAddress(string address, string expected)
    {
        Assert.AreEqual(expected, LevelAddress.GetChunkLabelForAddress(address));
    }

    [Test]
    public void SyncOrder_CurrentThenAheadThenBehindThenTest()
    {
        int current = LevelAddress.SyncOrder("lvl_2", 2);
        int next = LevelAddress.SyncOrder("lvl_3", 2);
        int far = LevelAddress.SyncOrder("lvl_5", 2);
        int behind = LevelAddress.SyncOrder("lvl_1", 2);
        int test = LevelAddress.SyncOrder("lvl_test_VersionA", 2);
        int junk = LevelAddress.SyncOrder("lvl_xyz", 2);
        Assert.Less(current, next);
        Assert.Less(next, far);
        Assert.Less(far, behind);
        Assert.Less(behind, junk);
        Assert.Less(junk, test);
    }

    [TestCase("VersionA", "VersionA")]
    [TestCase("  VersionA  ", "VersionA")]
    [TestCase("/VersionA/", "VersionA")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase(null, "")]
    [TestCase("A/B", "")]
    [TestCase("A\\B", "")]
    [TestCase("..", "")]
    [TestCase("../Secret", "")]
    public void SanitizeVersion(string input, string expected)
    {
        Assert.AreEqual(expected, LevelAddress.SanitizeVersion(input));
    }

    [TestCase("LevelData_12", true, 12)]
    [TestCase("LevelData_1", true, 1)]
    [TestCase("LevelData_12 1", false, 0)]
    [TestCase("LevelData_", false, 0)]
    [TestCase("LevelData_0", false, 0)]
    [TestCase("LevelData_-4", false, 0)]
    [TestCase("Level_12", false, 0)]
    [TestCase(null, false, 0)]
    public void TryParseLevelFileName(string name, bool expectedOk, int expectedLevel)
    {
        bool ok = LevelAddress.TryParseLevelFileName(name, out int level);
        Assert.AreEqual(expectedOk, ok);
        Assert.AreEqual(expectedLevel, level);
    }

    [Test]
    public void CatalogUrl_FollowsSpec()
    {
        Assert.AreEqual("https://cdn-fishyjam.abi-games.com/FishSort/Cheat/Android/catalog_levels.bin",
            LevelCdn.BuildCatalogUrl("Cheat", "Android"));
        Assert.AreEqual("https://cdn-fishyjam.abi-games.com/FishSort/Release/iOS/catalog_levels.bin",
            LevelCdn.BuildCatalogUrl("Release", "iOS"));
        Assert.AreEqual("https://cdn-fishyjam.abi-games.com/FishSort/Release/[BuildTarget]", LevelCdn.BuildRemoteLoadPath("Release"));
        Assert.AreEqual("FishSort/Cheat", LevelCdn.BuildUploadPrefix("Cheat"));
    }

    [Test]
    public void Bucket_IsFishSortsOwn()
    {
        Assert.AreEqual("fishy-jam-levels", LevelCdn.BucketName);
        Assert.AreEqual("https://cdn-fishyjam.abi-games.com", LevelCdn.Host);
    }

    [Test]
    public void RcloneRemote_IsNotTheOneOtherGamesUse()
    {
        // Remote "r2" trên máy dev giữ token của game khác; ghi đè lên nó sẽ làm hỏng upload của game đó.
        Assert.AreNotEqual("r2", LevelCdn.RcloneRemote);
        Assert.AreEqual("r2fishsort", LevelCdn.RcloneRemote);
    }

    [Test]
    public void RemoteDest_PointsIntoOwnBucketAndPrefix()
    {
        Assert.AreEqual("r2fishsort:fishy-jam-levels/FishSort/Cheat/Android", LevelCdn.BuildRemoteDest("Cheat", "Android"));
        Assert.AreEqual("r2fishsort:fishy-jam-levels/FishSort/Release/iOS", LevelCdn.BuildRemoteDest("Release", "iOS"));
        Assert.AreEqual("r2fishsort:fishy-jam-levels", LevelCdn.RemoteBucket);
    }
}
