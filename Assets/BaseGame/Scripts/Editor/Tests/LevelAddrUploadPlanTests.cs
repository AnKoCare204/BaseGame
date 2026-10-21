using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Kế hoạch upload: file nào được đẩy lên CDN theo từng phạm vi, và file nào trên CDN được coi là cũ.
public class LevelAddrUploadPlanTests
{
    private const string MainBundle = "levels_main_assets_lvl_0_0123456789abcdef.bundle";
    private const string TestBundle = "levels_test_assets_lvl_test_versiona_0123456789abcdef.bundle";
    private const string Catalog = "catalog_levels.bin";
    private const string CatalogHash = "catalog_levels.hash";

    [Test]
    public void Catalog_AlwaysIncluded()
    {
        foreach (LevelUploadScope scope in (LevelUploadScope[])Enum.GetValues(typeof(LevelUploadScope)))
        {
            Assert.IsTrue(LevelUploadPlan.IsIncluded(Catalog, scope), scope.ToString());
            Assert.IsTrue(LevelUploadPlan.IsIncluded(CatalogHash, scope), scope.ToString());
        }
    }

    [Test]
    public void VariantMarker_NeverIncluded()
    {
        foreach (LevelUploadScope scope in (LevelUploadScope[])Enum.GetValues(typeof(LevelUploadScope)))
        {
            Assert.IsFalse(LevelUploadPlan.IsIncluded(LevelUploadPlan.VariantMarkerFileName, scope), scope.ToString());
            Assert.IsFalse(LevelUploadPlan.IsIncluded("buildlogtep.json", scope), scope.ToString());
        }
    }

    [Test]
    public void Bundles_FollowScope()
    {
        Assert.IsTrue(LevelUploadPlan.IsIncluded(MainBundle, LevelUploadScope.All));
        Assert.IsTrue(LevelUploadPlan.IsIncluded(TestBundle, LevelUploadScope.All));
        Assert.IsTrue(LevelUploadPlan.IsIncluded(MainBundle, LevelUploadScope.OriginOnly));
        Assert.IsFalse(LevelUploadPlan.IsIncluded(TestBundle, LevelUploadScope.OriginOnly));
        Assert.IsFalse(LevelUploadPlan.IsIncluded(MainBundle, LevelUploadScope.TestOnly));
        Assert.IsTrue(LevelUploadPlan.IsIncluded(TestBundle, LevelUploadScope.TestOnly));
    }

    [Test]
    public void BundleFilterArgs_NeverLetNonBundleThrough()
    {
        foreach (LevelUploadScope scope in (LevelUploadScope[])Enum.GetValues(typeof(LevelUploadScope)))
        {
            string args = LevelUploadPlan.BundleFilterArgs(scope);
            StringAssert.EndsWith("--filter \"- *\"", args, "filter cuối phải loại mọi file còn lại (catalog, variant.txt)");
            StringAssert.Contains(".bundle", args);
        }
    }

    [Test]
    public void CanUpload_RequiresMatchingMarker()
    {
        Assert.IsTrue(LevelUploadPlan.CanUpload("Cheat", "Cheat", out string reason));
        Assert.IsFalse(LevelUploadPlan.CanUpload(null, "Cheat", out reason));
        StringAssert.Contains("Build Levels", reason);
        Assert.IsFalse(LevelUploadPlan.CanUpload("Release", "Cheat", out reason));
        StringAssert.Contains("Release", reason);
        Assert.IsFalse(LevelUploadPlan.CanUpload("Cheat", null, out reason));
    }

    [Test]
    public void FindStale_OnlyOldFilesMissingFromCurrentBuild()
    {
        DateTime now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        List<RemoteFileInfo> remote = new List<RemoteFileInfo>
        {
            new RemoteFileInfo { Path = MainBundle, Name = MainBundle, Size = 10, ModTime = "2026-09-01T00:00:00Z" },
            new RemoteFileInfo { Path = "old_a.bundle", Name = "old_a.bundle", Size = 10, ModTime = "2026-09-01T00:00:00Z" },
            new RemoteFileInfo { Path = "old_recent.bundle", Name = "old_recent.bundle", Size = 10, ModTime = "2026-09-27T23:00:00Z" },
            new RemoteFileInfo { Path = "old_baddate.bundle", Name = "old_baddate.bundle", Size = 10, ModTime = "khong phai ngay" },
        };
        List<string> current = new List<string> { MainBundle, Catalog };

        List<RemoteFileInfo> stale = LevelUploadPlan.FindStale(remote, current, 2, now);

        Assert.AreEqual(1, stale.Count);
        Assert.AreEqual("old_a.bundle", stale[0].Path);
    }

    [Test]
    public void FindStale_GraceZero_TakesEverythingUnused()
    {
        DateTime now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        List<RemoteFileInfo> remote = new List<RemoteFileInfo>
        {
            new RemoteFileInfo { Path = "old_recent.bundle", Name = "old_recent.bundle", Size = 10, ModTime = "2026-09-28T11:00:00Z" },
        };
        List<RemoteFileInfo> stale = LevelUploadPlan.FindStale(remote, new List<string>(), 0, now);
        Assert.AreEqual(1, stale.Count);
    }
}
