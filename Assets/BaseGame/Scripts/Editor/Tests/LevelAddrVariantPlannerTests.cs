using NUnit.Framework;

/// File trong thư mục biến thể: file nào thành entry Levels_Main (chung chunk với level gốc), file nào bị bỏ qua.
public class LevelAddrVariantPlannerTests
{
    [Test]
    public void Variant_WithinMax_GetsVariantAddressAndBaseChunk()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyVariant("LevelData_101_3", 150);
        Assert.AreEqual(LevelFileKind.Variant, c.Kind);
        Assert.AreEqual(101, c.Level);
        Assert.AreEqual(3, c.Variant);
        Assert.AreEqual("Levels/Variant/Level_101_3", c.Address);
        Assert.AreEqual("lvl_1", c.Label);
    }

    [Test]
    public void Variant_OfLevelThree_IsPacked()
    {
        Assert.AreEqual(LevelFileKind.Variant, LevelEntryPlanner.ClassifyVariant("LevelData_3_1", 150).Kind);
    }

    [Test]
    public void Variant_AboveMax_IsSkipped()
    {
        LevelFileClass c = LevelEntryPlanner.ClassifyVariant("LevelData_151_2", 150);
        Assert.AreEqual(LevelFileKind.VariantAboveMax, c.Kind);
        Assert.AreEqual(151, c.Level);
    }

    [TestCase("LevelData_1_1")]
    [TestCase("LevelData_2_3")]
    public void Variant_OfFtueLevel_IsSkipped(string name)
    {
        Assert.AreEqual(LevelFileKind.VariantApkOnly, LevelEntryPlanner.ClassifyVariant(name, 150).Kind);
    }

    [TestCase("LevelData_12")]
    [TestCase("LevelData_12_3 (1)")]
    [TestCase("LevelData_12_0")]
    [TestCase("Level_12_3")]
    [TestCase("")]
    [TestCase(null)]
    public void Variant_BadName_IsIgnored(string name)
    {
        Assert.AreEqual(LevelFileKind.BadName, LevelEntryPlanner.ClassifyVariant(name, 150).Kind);
    }
}
