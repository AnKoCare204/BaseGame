using CoreData;
using NUnit.Framework;

/// Quy ước tên của biến thể level: file LevelData_{n}_{x} (tool vẽ level ghi) và address Levels/Variant/Level_{n}_{x}
/// (Setup đóng gói). Biến thể nằm chung chunk với level gốc n.
public class LevelAddrVariantAddressTests
{
    [Test]
    public void VariantAddress_RoundTrips()
    {
        string address = LevelAddress.GetVariantAddress(12, 3);
        Assert.AreEqual("Levels/Variant/Level_12_3", address);
        Assert.IsTrue(LevelAddress.IsVariantAddress(address));
        Assert.IsTrue(LevelAddress.TryParseVariantAddress(address, out int level, out int variant));
        Assert.AreEqual(12, level);
        Assert.AreEqual(3, variant);
    }

    [TestCase("Levels/Level_12")]
    [TestCase("Levels/TEST/VersionA/Level_12")]
    [TestCase("Levels/Variant/Level_12")]
    [TestCase("Levels/Variant/Level_12_")]
    [TestCase("Levels/Variant/Level__3")]
    [TestCase("Levels/Variant/Level_12_0")]
    [TestCase("Levels/Variant/Level_0_3")]
    [TestCase("Levels/Variant/Level_12_3_4")]
    [TestCase("Levels/Variant/Level_12_3 1")]
    [TestCase("")]
    [TestCase(null)]
    public void VariantAddress_RejectsOtherAddresses(string address)
    {
        Assert.IsFalse(LevelAddress.TryParseVariantAddress(address, out _, out _));
    }

    [TestCase("LevelData_12_3", 12, 3)]
    [TestCase("LevelData_150_10", 150, 10)]
    [TestCase("LevelData_3_1", 3, 1)]
    public void VariantFileName_Parses(string name, int expectedLevel, int expectedVariant)
    {
        Assert.IsTrue(LevelAddress.TryParseVariantFileName(name, out int level, out int variant));
        Assert.AreEqual(expectedLevel, level);
        Assert.AreEqual(expectedVariant, variant);
    }

    [TestCase("LevelData_12")]
    [TestCase("LevelData_12_3 (1)")]
    [TestCase("LevelData_12_0")]
    [TestCase("LevelData_12_")]
    [TestCase("LevelData__3")]
    [TestCase("LevelData_12_x")]
    [TestCase("Level_12_3")]
    [TestCase("")]
    [TestCase(null)]
    public void VariantFileName_RejectsBadNames(string name)
    {
        Assert.IsFalse(LevelAddress.TryParseVariantFileName(name, out _, out _));
    }

    [Test]
    public void ProductionFileName_IsNotAVariant()
    {
        Assert.IsFalse(LevelAddress.TryParseLevelFileName("LevelData_12_3", out _));
        Assert.IsFalse(LevelAddress.TryParseVariantFileName("LevelData_12", out _, out _));
    }

    [Test]
    public void ChunkLabel_OfVariant_IsChunkOfBaseLevel()
    {
        Assert.AreEqual("lvl_0", LevelAddress.GetChunkLabelForAddress("Levels/Variant/Level_12_3"));
        Assert.AreEqual("lvl_1", LevelAddress.GetChunkLabelForAddress("Levels/Variant/Level_101_150"));
        Assert.IsNull(LevelAddress.GetChunkLabelForAddress("Levels/Variant/Level_12"));
        Assert.AreEqual("lvl_0", LevelAddress.GetChunkLabelForAddress("Levels/Level_12"), "level thường không đổi");
    }

    [Test]
    public void VariantFolder_IsTheToolFolder()
    {
        Assert.AreEqual("Assets/BaseGame/TextAssets/LevelDataVariant", LevelAddress.VariantFolder);
    }
}
