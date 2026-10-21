using System.IO;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using NUnit.Framework;
using UnityEditor;

/// LevelGlobalConfig sau khi chuyển level sang Addressables: không còn kéo file level vào Resources,
/// bảng cache độ khó khớp file, và config dựng đúng address.
public class LevelAddrConfigTests
{
    private const string ConfigAsset = "Assets/Resources/GlobalConfig/LevelGlobalConfig.asset";
    private const string DesignAsset = "Assets/Resources/GlobalConfig/LevelDesignData.asset";
    private const string TextAssetsRoot = "Assets/BaseGame/TextAssets/";

    [TestCase(ConfigAsset)]
    [TestCase(DesignAsset)]
    public void ResourcesAsset_DoesNotReferenceLevelFiles(string assetPath)
    {
        foreach (string dependency in AssetDatabase.GetDependencies(assetPath, true))
        {
            Assert.IsFalse(dependency.StartsWith(TextAssetsRoot),
                $"{assetPath} còn tham chiếu {dependency}: file level sẽ bị đóng gói vào Resources.");
        }
    }

    [Test]
    public void EveryBakedLevel_HasReadableFile()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        Assert.GreaterOrEqual(config.LevelMaxInConfig, 1);
        for (int level = 1; level <= config.LevelMaxInConfig; level++)
        {
            string path = config.GetLevelFilePath(level);
            Assert.IsTrue(File.Exists(path), $"thiếu {path}");
            LevelData data = LevelFileReader.Read(path);
            Assert.IsNotNull(data, $"{path} không giải mã được");
            Assert.Greater(data.BubbleCount, 0, $"{path} không có bóng");
        }
    }

    [Test]
    public void Cache_MatchesDifficultyInFiles()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        Assert.AreEqual(config.LevelMaxInConfig, config.LevelCache.Count, "chạy menu Rebuild Difficulty Cache");
        for (int level = 1; level <= config.LevelMaxInConfig; level++)
        {
            Assert.IsTrue(config.TryGetCachedDifficulty(level, out LevelDifficulty cached), $"level {level} chưa có trong cache");
            LevelData data = LevelFileReader.Read(config.GetLevelFilePath(level));
            Assert.AreEqual(data.Difficulty, cached, $"level {level}");
        }
    }

    [Test]
    public void Cache_UnknownLevel_ReturnsFalse()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        Assert.IsFalse(config.TryGetCachedDifficulty(0, out _));
        Assert.IsFalse(config.TryGetCachedDifficulty(-1, out _));
        Assert.IsFalse(config.TryGetCachedDifficulty(config.LevelMaxInConfig + 1000, out _));
    }

    [Test]
    public void GetLevelConfig_BuildsAddressWithoutLoadingData()
    {
        LevelConfig config = LevelGlobalConfig.Instance.GetLevelConfig(12);
        Assert.AreEqual(12, config.Level);
        Assert.AreEqual(12, config.SourceLevel);
        Assert.AreEqual("Levels/Level_12", config.LevelAddress);
        Assert.IsNull(config.LevelData);
        Assert.IsTrue(config.HasLevelAsset);

        LevelRequest request = config.ToRequest();
        Assert.AreEqual(12, request.Level);
        Assert.AreEqual(12, request.SourceLevel);
    }

    [Test]
    public void GetLevelConfig_PlaceholderDifficultyFollowsRule()
    {
        LevelDifficultRuleGlobalConfig rule = LevelDifficultRuleGlobalConfig.Instance;
        foreach (int level in new[] { 1, 10, 20, 35, 74, 80 })
        {
            Assert.AreEqual(rule.GetDifficulty(level), LevelGlobalConfig.Instance.GetLevelConfig(level).LevelType, $"level {level}");
        }
    }

    [Test]
    public void MaxPlayableLevel_InEditMode_IsBakedMax()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        Assert.AreEqual(config.LevelMaxInConfig, config.MaxPlayableLevel);
        Assert.IsTrue(config.IsLevelPlayable(1));
        Assert.IsTrue(config.IsLevelPlayable(config.LevelMaxInConfig));
        Assert.IsFalse(config.IsLevelPlayable(0));
        Assert.IsFalse(config.IsLevelPlayable(config.LevelMaxInConfig + 1));
    }

    [TestCase("Assets/BaseGame/TextAssets/LevelDataConverted", "Assets/BaseGame/TextAssets/LevelDataConverted", true)]
    [TestCase("Assets/BaseGame/TextAssets/LevelDataConverted/", "Assets\\BaseGame\\TextAssets\\LevelDataConverted", true)]
    [TestCase("assets/basegame/textassets/leveldataconverted", "Assets/BaseGame/TextAssets/LevelDataConverted", true)]
    [TestCase("Assets/BaseGame/TextAssets/TestLevel/VersionA", "Assets/BaseGame/TextAssets/LevelDataConverted", false)]
    [TestCase("", "Assets/BaseGame/TextAssets/LevelDataConverted", false)]
    [TestCase(null, "Assets/BaseGame/TextAssets/LevelDataConverted", false)]
    public void IsSameFolder(string a, string b, bool expected)
    {
        Assert.AreEqual(expected, LevelGlobalConfig.IsSameFolder(a, b));
    }
}
