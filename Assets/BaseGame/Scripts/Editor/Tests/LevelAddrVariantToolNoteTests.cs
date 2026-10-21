using CoreData;
using CoreGame.GameGlobalConfig;
using NUnit.Framework;

/// Dòng nhắc sau khi "Lưu biến thể" trong tool vẽ level: level 1-2 game không dùng, level ngoài LevelMaxInConfig chưa vào build,
/// còn lại phải Setup / Build Levels.
public class LevelAddrVariantToolNoteTests
{
    [TestCase(1)]
    [TestCase(2)]
    public void FtueLevel_WarnsUnused(int level)
    {
        StringAssert.Contains("không dùng", LevelDesignDataEditor.VariantNote(level));
    }

    [Test]
    public void LevelInBuild_RemindsSetup()
    {
        StringAssert.Contains("Setup", LevelDesignDataEditor.VariantNote(3));
    }

    [Test]
    public void LevelOutsideBuild_RemindsMax()
    {
        int above = LevelGlobalConfig.Instance.LevelMaxInConfig + 1;
        StringAssert.Contains("LevelMaxInConfig", LevelDesignDataEditor.VariantNote(above));
    }

    [Test]
    public void ToolFolder_IsRuntimeFolder()
    {
        Assert.AreEqual(LevelAddress.VariantFolder, LevelDesignData.DefaultVariantFolder);
    }
}
