using System.Collections.Generic;
using CoreData;
using NUnit.Framework;

/// Bảng level gốc -> các số biến thể, dựng từ key catalog (CDN hoặc app) hoặc từ tên file trong thư mục biến thể (Editor).
public class LevelAddrVariantIndexTests
{
    [Test]
    public void Build_KeepsOnlyVariantAddresses()
    {
        List<object> keys = new List<object>
        {
            "Levels/Level_12",
            "Levels/TEST/VersionA/Level_12",
            "Levels/Variant/Level_12_5",
            "Levels/Variant/Level_12_3",
            "lvl_0",
            "Levels/Variant/Level_40_1",
            "Levels/Variant/Level_12_3",
            12345,
            "Levels/Variant/Level_12",
        };

        LevelVariantIndex index = LevelVariantIndex.Build(keys);

        CollectionAssert.AreEqual(new[] { 3, 5 }, index.GetVariants(12));
        CollectionAssert.AreEqual(new[] { 1 }, index.GetVariants(40));
        CollectionAssert.IsEmpty(index.GetVariants(13));
        Assert.AreEqual(2, index.LevelCount);
    }

    [Test]
    public void Build_NullKeys_IsEmpty()
    {
        Assert.AreEqual(0, LevelVariantIndex.Build(null).LevelCount);
        Assert.AreEqual(0, LevelVariantIndex.Empty.LevelCount);
        CollectionAssert.IsEmpty(LevelVariantIndex.Empty.GetVariants(12));
    }

    [Test]
    public void FromFileNames_ParsesToolFileNames()
    {
        LevelVariantIndex index = LevelVariantIndex.FromFileNames(new[]
        {
            "LevelData_12_3", "LevelData_12", "LevelData_12_3 (1)", "LevelData_7_2", "LevelData_7_1", null,
        });

        CollectionAssert.AreEqual(new[] { 3 }, index.GetVariants(12));
        CollectionAssert.AreEqual(new[] { 1, 2 }, index.GetVariants(7));
        Assert.AreEqual(2, index.LevelCount);
    }
}
