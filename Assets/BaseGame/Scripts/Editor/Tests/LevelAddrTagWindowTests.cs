using System.Collections.Generic;
using System.IO;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using NUnit.Framework;

/// Tool Check Tag Level với nguồn Test Level: quét thư mục con theo Version, chỉ so file với luật, sửa chỉ ghi file.
public class LevelAddrTagWindowTests
{
    private const string Root = "Temp/LevelAddrTests/TestLevel";
    private const string RealLevel = "Assets/BaseGame/TextAssets/LevelDataConverted/LevelData_1.json";

    [SetUp]
    public void SetUp()
    {
        WriteLevel($"{Root}/VersionA/LevelData_20.json", 20, LevelDifficulty.Normal);     // luật: SuperHard
        WriteLevel($"{Root}/VersionA/LevelData_3.json", 3, LevelDifficulty.Normal);       // luật: Normal
        WriteLevel($"{Root}/VersionB/LevelData_15.json", 15, LevelDifficulty.Hard);       // luật: Hard
        File.WriteAllText($"{Root}/VersionB/LevelData_7.json", "khong phai level");
        File.WriteAllText($"{Root}/VersionB/notes.json", "{}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists("Temp/LevelAddrTests")) Directory.Delete("Temp/LevelAddrTests", true);
    }

    private static void WriteLevel(string path, int level, LevelDifficulty difficulty)
    {
        LevelData data = LevelFileReader.Read(RealLevel);
        data.EditorSetLevel(level);
        data.EditorSetDifficulty(difficulty);
        Assert.IsTrue(LevelFileReader.Write(path, data, out string message), message);
    }

    [Test]
    public void ScanTest_Recursive_FindsEveryVersion()
    {
        List<LevelTagWindow.Row> rows = LevelTagWindow.ScanFolder(Root, LevelTagWindow.LevelSource.TestLevel, true);

        Assert.AreEqual(4, rows.Count, "notes.json không phải file level, không được tính");
        LevelTagWindow.Row wrong = rows.Find(r => r.Version == "VersionA" && r.Level == 20);
        Assert.IsNotNull(wrong);
        Assert.IsTrue(wrong.FileWrong);
        Assert.AreEqual(LevelDifficulty.SuperHard, wrong.Expected);
        Assert.IsFalse(wrong.HasConfigEntry, "bộ test không có cache trong config");
        Assert.IsFalse(wrong.ConfigWrong);

        Assert.IsFalse(rows.Find(r => r.Version == "VersionA" && r.Level == 3).Wrong);
        Assert.IsFalse(rows.Find(r => r.Version == "VersionB" && r.Level == 15).Wrong);
        Assert.IsNotNull(rows.Find(r => r.Version == "VersionB" && r.Level == 7).ReadError);
    }

    [Test]
    public void ScanTest_SingleVersion_OnlyThatFolder()
    {
        List<LevelTagWindow.Row> rows = LevelTagWindow.ScanFolder($"{Root}/VersionA", LevelTagWindow.LevelSource.TestLevel, false);
        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows.TrueForAll(r => r.Version == ""), "quét một thư mục thì Version tính từ thư mục đó là rỗng");
    }

    [Test]
    public void FixTest_WritesOnlyTheFile()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        Assert.IsTrue(config.TryGetCachedDifficulty(20, out LevelDifficulty cachedBefore));

        List<LevelTagWindow.Row> rows = LevelTagWindow.ScanFolder(Root, LevelTagWindow.LevelSource.TestLevel, true);
        LevelTagWindow.Row wrong = rows.Find(r => r.Version == "VersionA" && r.Level == 20);

        Assert.IsTrue(LevelTagWindow.FixRow(wrong, LevelTagWindow.LevelSource.TestLevel, out string message), message);

        Assert.AreEqual(LevelDifficulty.SuperHard, LevelFileReader.Read(wrong.Path).Difficulty);
        Assert.IsTrue(config.TryGetCachedDifficulty(20, out LevelDifficulty cachedAfter));
        Assert.AreEqual(cachedBefore, cachedAfter, "sửa bộ test không được đụng cache của level production");

        List<LevelTagWindow.Row> again = LevelTagWindow.ScanFolder(Root, LevelTagWindow.LevelSource.TestLevel, true);
        Assert.IsFalse(again.Find(r => r.Version == "VersionA" && r.Level == 20).Wrong);
    }

    [Test]
    public void FixRow_UnreadableFile_Fails()
    {
        List<LevelTagWindow.Row> rows = LevelTagWindow.ScanFolder(Root, LevelTagWindow.LevelSource.TestLevel, true);
        LevelTagWindow.Row broken = rows.Find(r => r.Level == 7);
        Assert.IsFalse(LevelTagWindow.FixRow(broken, LevelTagWindow.LevelSource.TestLevel, out _));
    }

    [Test]
    public void ScanMain_RealFolder_HasConfigEntries()
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        List<LevelTagWindow.Row> rows = LevelTagWindow.ScanFolder(config.LevelFolder, LevelTagWindow.LevelSource.MainLevel, false);
        Assert.GreaterOrEqual(rows.Count, config.LevelMaxInConfig);
        LevelTagWindow.Row first = rows.Find(r => r.Level == 1);
        Assert.IsTrue(first.HasConfigEntry);
        Assert.AreEqual("", first.Version);
    }

    [Test]
    public void ScanMissingFolder_ReturnsEmpty()
    {
        Assert.AreEqual(0, LevelTagWindow.ScanFolder("Temp/LevelAddrTests/khong_co", LevelTagWindow.LevelSource.TestLevel, true).Count);
    }
}
