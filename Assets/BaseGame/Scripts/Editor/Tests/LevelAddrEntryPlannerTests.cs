using System.Collections.Generic;
using NUnit.Framework;

/// Phân loại file trong thư mục level: file nào thành entry Addressables, file nào bị bỏ qua.
public class LevelAddrEntryPlannerTests
{
    [Test]
    public void Main_WithinMax_GetsAddressAndChunkLabel()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyMain("LevelData_101", 150);
        Assert.AreEqual(LevelFileKind.Main, c.Kind);
        Assert.AreEqual(101, c.Level);
        Assert.AreEqual("Levels/Level_101", c.Address);
        Assert.AreEqual("lvl_1", c.Label);
    }

    [Test]
    public void Main_AboveMax_IsSkipped()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyMain("LevelData_151", 150);
        Assert.AreEqual(LevelFileKind.MainAboveMax, c.Kind);
        Assert.AreEqual(151, c.Level);
    }

    [TestCase("LevelData_12 1")]
    [TestCase("LevelData_")]
    [TestCase("LevelData_0")]
    [TestCase("Level_12")]
    [TestCase("")]
    [TestCase(null)]
    public void Main_BadName_IsIgnored(string name)
    {
        Assert.AreEqual(LevelFileKind.BadName, LevelEntryPlanner.ClassifyMain(name, 150).Kind);
    }

    [Test]
    public void Test_InVersionFolder_GetsTestAddressAndLabel()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyTest("VersionA/LevelData_5.json");
        Assert.AreEqual(LevelFileKind.Test, c.Kind);
        Assert.AreEqual(5, c.Level);
        Assert.AreEqual("VersionA", c.Version);
        Assert.AreEqual("Levels/TEST/VersionA/Level_5", c.Address);
        Assert.AreEqual("lvl_test_VersionA", c.Label);
    }

    [Test]
    public void Test_BackslashPath_IsAccepted()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyTest("VersionA\\LevelData_5.json");
        Assert.AreEqual(LevelFileKind.Test, c.Kind);
        Assert.AreEqual("VersionA", c.Version);
    }

    [Test]
    public void Test_AtRoot_IsIgnored()
    {
        Assert.AreEqual(LevelFileKind.TestAtRoot, LevelEntryPlanner.ClassifyTest("LevelData_5.json").Kind);
    }

    [TestCase("VersionA/LevelData_12 1.json")]
    [TestCase("VersionA/sub/LevelData_5.json")]
    [TestCase("VersionA/notes.json")]
    [TestCase("")]
    [TestCase(null)]
    public void Test_BadNameOrNested_IsIgnored(string relativePath)
    {
        Assert.AreEqual(LevelFileKind.BadName, LevelEntryPlanner.ClassifyTest(relativePath).Kind);
    }

    [Test]
    public void FindMissingLevels_ListsGapsUpToMax()
    {
        List<int> missing = LevelEntryPlanner.FindMissingLevels(new[] { 1, 2, 4, 7, 200 }, 6);
        CollectionAssert.AreEqual(new[] { 3, 5, 6 }, missing);
    }

    [Test]
    public void DescribeUnsavedScenes_NoDirtyScene_ReturnsNull()
    {
        Assert.IsNull(LevelEntryPlanner.DescribeUnsavedScenes(new List<string>()));
        Assert.IsNull(LevelEntryPlanner.DescribeUnsavedScenes(null));
    }

    [Test]
    public void DescribeUnsavedScenes_NamesEveryDirtyScene()
    {
        string message = LevelEntryPlanner.DescribeUnsavedScenes(new List<string> { "GamePlayScene", "" });
        StringAssert.Contains("GamePlayScene", message);
        StringAssert.Contains("(chưa đặt tên)", message);
        StringAssert.Contains("Build Levels", message);
    }

    [Test]
    public void FindMissingLevels_NothingPresent_ListsAll()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, LevelEntryPlanner.FindMissingLevels(new int[0], 3));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, LevelEntryPlanner.FindMissingLevels(null, 3));
    }
}
