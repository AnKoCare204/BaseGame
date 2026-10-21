using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CoreData;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

/// Các lỗi do lượt review cuối tìm ra (I1-I8). Mỗi nhóm test tái hiện một lỗi.
public class LevelAddrReviewFixTests
{
    private const string TempDir = "Temp/LevelAddrReviewTests";
    private const string RealLevel = "Assets/BaseGame/TextAssets/LevelDataConverted/LevelData_1.json";

    [SetUp]
    public void SetUp()
    {
        Directory.CreateDirectory(TempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
    }

    private static IResourceLocation Loc(string id)
    {
        return new ResourceLocationBase("Levels/Level_1", id, "provider", typeof(TextAsset));
    }

    // ---------------------------------------------------------------- I1: bản CDN hỏng, level rỗng

    [Test]
    public void I1_Decode_LevelWithoutBubbles_IsRejected()
    {
        string payload = DataSerializer.Encrypt("{}");
        Assert.IsFalse(LevelDataCodec.TryDecode(payload, out LevelData data), "level không có bóng sẽ dựng ra bàn rỗng");
        Assert.IsNull(data);
    }

    [Test]
    public void I1_IsPlayable_RealLevelYes_EmptyNo()
    {
        Assert.IsTrue(LevelDataCodec.IsPlayable(LevelFileReader.Read(RealLevel)));
        Assert.IsFalse(LevelDataCodec.IsPlayable(null));
        Assert.IsFalse(LevelDataCodec.IsPlayable(new LevelData()));
    }

    [Test]
    public void I1_Fallback_RemoteFails_TriesApkCopy()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        LevelLocation location = new LevelLocation(remote, local, true, "");
        Assert.AreSame(local, location.FallbackFor(remote));
    }

    [Test]
    public void I1_Fallback_NothingLeftToTry()
    {
        IResourceLocation remote = Loc("remote");
        IResourceLocation local = Loc("local");
        Assert.IsNull(new LevelLocation(remote, local, false, "").FallbackFor(local), "bản APK hỏng thì không quay lại bản CDN chưa tải");
        Assert.IsNull(new LevelLocation(remote, null, true, "").FallbackFor(remote));
        Assert.IsNull(new LevelLocation(remote, null, true, "VersionA").FallbackFor(remote));
        Assert.IsNull(default(LevelLocation).FallbackFor(null));
    }

    [Test]
    public void I1_Setup_FindsFilesThatCannotBePlayed()
    {
        string good = $"{TempDir}/LevelData_1.json";
        string junk = $"{TempDir}/LevelData_2.json";
        string empty = $"{TempDir}/LevelData_3.json";
        File.Copy(RealLevel, good);
        File.WriteAllText(junk, "<<<<<<< HEAD\nkhong phai level\n=======");
        File.WriteAllText(empty, DataSerializer.Encrypt("{}"));

        List<string> invalid = LevelAddressableSetup.FindUnreadableLevelFiles(new[] { good, junk, empty });

        CollectionAssert.AreEquivalent(new[] { junk, empty }, invalid);
    }

    [Test]
    public void I1_Report_WithInvalidFile_IsNotComplete()
    {
        LevelEntryReport report = new LevelEntryReport { MaxLevel = 3, MainCount = 3 };
        Assert.IsTrue(report.IsComplete);
        report.InvalidFiles.Add("Assets/x/LevelData_2.json");
        Assert.IsFalse(report.IsComplete);
        StringAssert.Contains("Assets/x/LevelData_2.json", report.Describe());
    }

    // ---------------------------------------------------------------- I2: exception trong lúc dựng level

    [Test]
    public void I2_Guard_BodyThrows_ReturnsFalseAndReports()
    {
        Exception seen = null;
        bool ok = LevelLoadGuard.RunAsync(() => throw new InvalidOperationException("board hong"), e => seen = e)
            .GetAwaiter().GetResult();
        Assert.IsFalse(ok);
        Assert.IsNotNull(seen);
        StringAssert.Contains("board hong", seen.Message);
    }

    [Test]
    public void I2_Guard_FaultedTask_ReturnsFalse()
    {
        bool ok = LevelLoadGuard.RunAsync(() => UniTask.FromException(new InvalidOperationException("x")), null)
            .GetAwaiter().GetResult();
        Assert.IsFalse(ok);
    }

    [Test]
    public void I2_Guard_BodySucceeds_ReturnsTrue()
    {
        bool reported = false;
        bool ok = LevelLoadGuard.RunAsync(() => UniTask.CompletedTask, e => reported = true).GetAwaiter().GetResult();
        Assert.IsTrue(ok);
        Assert.IsFalse(reported);
    }

    [Test]
    public void I2_Guard_NullBody_ReturnsFalse()
    {
        Assert.IsFalse(LevelLoadGuard.RunAsync(null, null).GetAwaiter().GetResult());
    }

    // ---------------------------------------------------------------- I3: gate không được phạt level đã có trên máy

    [Test]
    public void I3_Gate_BakedLevel_ConfigRead_NoTest_DoesNotWait()
    {
        LevelGateWait wait = LevelGateRule.Decide(online: true, readyOffline: true, remoteConfigRead: true,
            testActive: false, catalogLoaded: false, catalogRetryAllowed: true);
        Assert.AreEqual(LevelGateWait.None, wait, "CDN hỏng không được làm chậm level đã nằm trong APK");
    }

    [Test]
    public void I3_Gate_Offline_NeverWaits()
    {
        Assert.AreEqual(LevelGateWait.None, LevelGateRule.Decide(false, false, false, true, false, true));
    }

    [Test]
    public void I3_Gate_ConfigNotRead_WaitsForConfigOnly()
    {
        LevelGateWait wait = LevelGateRule.Decide(true, true, false, false, false, true);
        Assert.AreEqual(LevelGateWait.RemoteConfig, wait);
    }

    [Test]
    public void I3_Gate_TestActive_NeedsCatalog()
    {
        Assert.AreEqual(LevelGateWait.Catalog, LevelGateRule.Decide(true, true, true, true, false, true));
        Assert.AreEqual(LevelGateWait.None, LevelGateRule.Decide(true, true, true, true, true, true), "catalog đã nạp thì không đợi gì");
    }

    [Test]
    public void I3_Gate_LevelMissing_NeedsCatalog_UnlessItJustFailed()
    {
        Assert.AreEqual(LevelGateWait.Catalog, LevelGateRule.Decide(true, false, true, false, false, true));
        Assert.AreEqual(LevelGateWait.None, LevelGateRule.Decide(true, false, true, false, false, false));
    }

    [Test]
    public void I3_Gate_ConfigNotReadAndLevelMissing_WaitsForBoth()
    {
        LevelGateWait wait = LevelGateRule.Decide(true, false, false, false, false, true);
        Assert.AreEqual(LevelGateWait.RemoteConfig | LevelGateWait.Catalog, wait);
    }

    [TestCase(-1d, true, TestName = "I3_CatalogRetry_NeverFailed")]
    [TestCase(5d, false, TestName = "I3_CatalogRetry_FailedJustNow")]
    [TestCase(44.9d, false, TestName = "I3_CatalogRetry_StillBackingOff")]
    [TestCase(45d, true, TestName = "I3_CatalogRetry_BackoffOver")]
    public void I3_CatalogRetry(double secondsSinceFailure, bool expected)
    {
        Assert.AreEqual(expected, LevelGateRule.IsCatalogRetryAllowed(secondsSinceFailure));
    }

    // ---------------------------------------------------------------- I4: dọn CDN phải so với catalog đang chạy

    [Test]
    public void I4_Clean_SameHash_Allowed()
    {
        Assert.IsTrue(LevelUploadPlan.CanClean("abc123", " ABC123\n", out string reason), reason);
    }

    [Test]
    public void I4_Clean_LocalBuildNotUploaded_Refused()
    {
        Assert.IsFalse(LevelUploadPlan.CanClean("newhash", "oldhash", out string reason));
        StringAssert.Contains("Upload", reason);
    }

    [TestCase(null, "abc")]
    [TestCase("abc", null)]
    [TestCase("", "abc")]
    [TestCase("abc", "  ")]
    public void I4_Clean_UnknownHash_Refused(string local, string remote)
    {
        Assert.IsFalse(LevelUploadPlan.CanClean(local, remote, out string reason));
        Assert.IsNotEmpty(reason);
    }

    // ---------------------------------------------------------------- I5: ghi đè cheat không bị xoá khi chưa có catalog

    [Test]
    public void I5_Override_ListNotAuthoritative_KeepsSavedVersion()
    {
        Assert.AreEqual("VersionA", LevelTestSelector.NormalizeOverride("VersionA", new List<string>(), false));
        Assert.AreEqual("VersionA", LevelTestSelector.NormalizeOverride("VersionA", null, false));
    }

    [Test]
    public void I5_Override_ListAuthoritative_DropsMissingVersion()
    {
        Assert.AreEqual("", LevelTestSelector.NormalizeOverride("VersionA", new List<string> { "VersionB" }, true));
    }

    [Test]
    public void I5_Choices_IncludeSavedVersionWhenListIsEmpty()
    {
        List<string> choices = LevelTestSelector.BuildCheatChoices(new List<string>(), "VersionA");
        CollectionAssert.AreEqual(new[] { "", "off", "VersionA" }, choices);
        CollectionAssert.AreEqual(new[] { "", "off" }, LevelTestSelector.BuildCheatChoices(new List<string>(), "off"));
        CollectionAssert.AreEqual(new[] { "", "off" }, LevelTestSelector.BuildCheatChoices(new List<string>(), ""));
    }

    // ---------------------------------------------------------------- I6: Version không có trong catalog

    [Test]
    public void I6_UserProperty_VersionMissingFromCatalog_IsOff()
    {
        Assert.AreEqual("off", TestLevelRemoteConfigData.DescribeUserProperty(true, "versiona", false));
    }

    [Test]
    public void I6_UserProperty_KnownOrNotYetChecked_IsVersion()
    {
        Assert.AreEqual("VersionA", TestLevelRemoteConfigData.DescribeUserProperty(true, "VersionA", true));
        Assert.AreEqual("VersionA", TestLevelRemoteConfigData.DescribeUserProperty(true, "VersionA", null));
    }

    [Test]
    public void I6_UserProperty_Disabled_IsOff()
    {
        Assert.AreEqual("off", TestLevelRemoteConfigData.DescribeUserProperty(false, "VersionA", true));
        Assert.AreEqual("off", TestLevelRemoteConfigData.DescribeUserProperty(true, "", null));
    }

    [Test]
    public void I6_IsVersionAvailable_IsCaseSensitive()
    {
        List<string> available = new List<string> { "VersionA" };
        Assert.IsTrue(LevelTestSelector.IsVersionAvailable("VersionA", available));
        Assert.IsFalse(LevelTestSelector.IsVersionAvailable("versiona", available));
        Assert.IsFalse(LevelTestSelector.IsVersionAvailable("VersionA", null));
        Assert.IsFalse(LevelTestSelector.IsVersionAvailable("", available));
    }

    // ---------------------------------------------------------------- I7: .hash mới không được đi trước .bin cũ

    [Test]
    public void I7_UploadSteps_BundlesThenCatalogBinThenHash()
    {
        List<string> steps = LevelUploadPlan.BuildUploadSteps("C:/p/ServerData/Android", "r2:bucket/FishSort/Cheat/Android", LevelUploadScope.All);

        Assert.AreEqual(3, steps.Count);
        StringAssert.Contains(".bundle", steps[0]);
        StringAssert.Contains("immutable", steps[0]);
        StringAssert.Contains("catalog_*.bin", steps[1]);
        StringAssert.Contains("no-cache", steps[1]);
        StringAssert.Contains("catalog_*.hash", steps[2]);
        StringAssert.Contains("max-age=300", steps[2]);
        foreach (string step in steps)
        {
            StringAssert.StartsWith("copy \"C:/p/ServerData/Android\" \"r2:bucket/FishSort/Cheat/Android\"", step);
            StringAssert.DoesNotContain("variant.txt", step);
        }
    }

    [Test]
    public void I7_UploadSteps_CatalogBinIsNeverCachedAtTheEdge()
    {
        foreach (LevelUploadScope scope in (LevelUploadScope[])Enum.GetValues(typeof(LevelUploadScope)))
        {
            List<string> steps = LevelUploadPlan.BuildUploadSteps("s", "d", scope);
            StringAssert.DoesNotContain("max-age=300", steps[1], "client thấy hash mới thì phải tải được bin mới");
        }
    }

    // ---------------------------------------------------------------- I8: bộ chạy test không được xanh giả

    private abstract class Samples
    {
        [Test] public void Fine() { }
        [Test] public int ReturnsValue() => 1;
        [Test] public void TakesArguments(int value) { }
        [TestCase(1, ExpectedResult = 1)] public int WithExpectedResult(int value) => value;
        [TestCaseSource(nameof(Source))] public void FromSource(int value) { }
        [Test] private void Hidden() { }
        [TestCase(1)] public void FineCase(int value) { }
        public void NotATest() { }
        private static int[] Source = { 1 };
    }

    private static MethodInfo Sample(string name)
    {
        return typeof(Samples).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    [TestCase("Fine")]
    [TestCase("FineCase")]
    [TestCase("NotATest")]
    public void I8_Runner_SupportedMethods(string name)
    {
        Assert.IsNull(LevelAddrTestMenu.DescribeUnsupported(Sample(name)));
    }

    [TestCase("ReturnsValue")]
    [TestCase("TakesArguments")]
    [TestCase("WithExpectedResult")]
    [TestCase("FromSource")]
    [TestCase("Hidden")]
    public void I8_Runner_UnsupportedMethods_AreReported(string name)
    {
        Assert.IsNotNull(LevelAddrTestMenu.DescribeUnsupported(Sample(name)), $"{name} sẽ bị bỏ qua mà không ai biết");
    }
}
