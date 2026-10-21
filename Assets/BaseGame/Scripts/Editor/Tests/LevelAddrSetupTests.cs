using System.IO;
using System.Linq;
using CoreData;
using CoreGame.GameGlobalConfig;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

/// Trạng thái Addressables sau khi chạy Setup: đủ entry, đúng address / label, settings ở layout App.
/// Các test này đọc asset thật của project; chạy menu Tools/FishSort/Level Addressables/Setup trước.
public class LevelAddrSetupTests
{
    private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.GetSettings(false);

    [Test]
    public void MainGroup_HasEveryBakedLevel()
    {
        AddressableAssetGroup main = Settings.FindGroup(LevelAddressableSetup.MainGroupName);
        Assert.IsNotNull(main, "chưa chạy Setup");
        int max = LevelGlobalConfig.Instance.LevelMaxInConfig;
        // Levels_Main còn chứa biến thể (Levels/Variant/...), chỉ đếm level production.
        Assert.AreEqual(max, main.entries.Count(e => !LevelAddress.IsVariantAddress(e.address)));
        for (int level = 1; level <= max; level++)
        {
            string address = LevelAddress.GetLevelAddress(level);
            AddressableAssetEntry entry = main.entries.FirstOrDefault(e => e.address == address);
            Assert.IsNotNull(entry, $"thiếu entry {address}");
            string[] labels = entry.labels.Where(l => l.StartsWith(LevelAddress.LabelPrefix)).ToArray();
            CollectionAssert.AreEqual(new[] { LevelAddress.GetChunkLabel(level) }, labels, address);
        }
    }

    [Test]
    public void MainGroup_VariantEntriesFollowConvention()
    {
        AddressableAssetGroup main = Settings.FindGroup(LevelAddressableSetup.MainGroupName);
        Assert.IsNotNull(main, "chưa chạy Setup");
        int max = LevelGlobalConfig.Instance.LevelMaxInConfig;
        foreach (AddressableAssetEntry entry in main.entries.Where(e => LevelAddress.IsVariantAddress(e.address)))
        {
            Assert.IsTrue(LevelAddress.TryParseVariantAddress(entry.address, out int level, out _), entry.address);
            Assert.IsFalse(LevelSourceRule.IsApkOnly(level), entry.address);
            Assert.LessOrEqual(level, max, entry.address);
            string[] labels = entry.labels.Where(l => l.StartsWith(LevelAddress.LabelPrefix)).ToArray();
            CollectionAssert.AreEqual(new[] { LevelAddress.GetChunkLabel(level) }, labels, entry.address);
        }
    }

    [Test]
    public void TestGroup_EntriesFollowConvention()
    {
        AddressableAssetGroup test = Settings.FindGroup(LevelAddressableSetup.TestGroupName);
        Assert.IsNotNull(test, "chưa chạy Setup");
        foreach (AddressableAssetEntry entry in test.entries)
        {
            Assert.IsTrue(LevelAddress.IsTestAddress(entry.address), entry.address);
            string expectedLabel = LevelAddress.GetChunkLabelForAddress(entry.address);
            Assert.IsNotNull(expectedLabel, entry.address);
            CollectionAssert.AreEqual(new[] { expectedLabel }, entry.labels.ToArray(), entry.address);
        }
    }

    [Test]
    public void Settings_AreInAppLayout()
    {
        AssertAppLayout();
    }

    [Test]
    public void ApplyLayout_LevelsThenApp_RestoresAppLayout()
    {
        try
        {
            LevelAddressableSetup.ApplyLayout(LevelAddressableSetup.Layout.Levels);
            Assert.AreEqual(LevelAddressableSetup.Layout.Levels, LevelAddressableSetup.CurrentLayout);
            Assert.IsTrue(Settings.BuildRemoteCatalog);
        }
        finally
        {
            LevelAddressableSetup.ApplyLayout(LevelAddressableSetup.Layout.App);
        }

        AssertAppLayout();
    }

    [Test]
    public void BuildThatThrows_RestoresAppLayout_AndLeavesNoMarker()
    {
        string marker = "Temp/LevelAddrTests/variant.txt";
        Directory.CreateDirectory("Temp/LevelAddrTests");
        File.WriteAllText(marker, "Cheat");
        try
        {
            bool ok = LevelAddressableSetup.RunInLevelsLayout(
                () => throw new System.InvalidOperationException("build hong"), marker, out string message, false);

            Assert.IsFalse(ok);
            StringAssert.Contains("build hong", message);
            Assert.IsFalse(File.Exists(marker), "marker cũ phải bị xoá để Upload từ chối ServerData của lần build lỗi");
            AssertAppLayout();
        }
        finally
        {
            if (Directory.Exists("Temp/LevelAddrTests")) Directory.Delete("Temp/LevelAddrTests", true);
        }
    }

    [Test]
    public void BuildThatReportsError_LeavesNoMarker()
    {
        string marker = "Temp/LevelAddrTests/variant.txt";
        Directory.CreateDirectory("Temp/LevelAddrTests");
        try
        {
            bool ok = LevelAddressableSetup.RunInLevelsLayout(() => "loi tu addressables", marker, out string message, false);
            Assert.IsFalse(ok);
            Assert.AreEqual("loi tu addressables", message);
            Assert.IsFalse(File.Exists(marker));
            AssertAppLayout();
        }
        finally
        {
            if (Directory.Exists("Temp/LevelAddrTests")) Directory.Delete("Temp/LevelAddrTests", true);
        }
    }

    private static void AssertAppLayout()
    {
        AddressableAssetSettings settings = Settings;
        Assert.AreEqual(LevelAddressableSetup.Layout.App, LevelAddressableSetup.CurrentLayout);
        Assert.IsFalse(settings.BuildRemoteCatalog);
        Assert.AreEqual("Default", settings.profileSettings.GetProfileName(settings.activeProfileId));

        BundledAssetGroupSchema main = settings.FindGroup(LevelAddressableSetup.MainGroupName).GetSchema<BundledAssetGroupSchema>();
        Assert.IsTrue(main.IncludeInBuild);
        Assert.AreEqual(AddressableAssetSettings.kLocalBuildPath, main.BuildPath.GetName(settings));
        Assert.AreEqual(BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel, main.BundleMode);
        Assert.AreEqual(3, main.RetryCount);
        Assert.AreEqual(10, main.Timeout);

        BundledAssetGroupSchema test = settings.FindGroup(LevelAddressableSetup.TestGroupName).GetSchema<BundledAssetGroupSchema>();
        Assert.IsFalse(test.IncludeInBuild, "bộ test không được bake vào bản app");

        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null || group.Name == LevelAddressableSetup.MainGroupName || group.Name == LevelAddressableSetup.TestGroupName) continue;
            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null) Assert.IsTrue(schema.IncludeInBuild, $"group {group.Name} phải được build trong layout App");
        }

        string remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);
        Assert.AreEqual(LevelCdn.BuildRemoteLoadPath(LevelCdn.VariantRelease), remoteLoadPath);
    }
}
