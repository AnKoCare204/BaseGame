using System.IO;
using CoreData;
using NUnit.Framework;

/// Đọc file level: file hỏng phải trả null thay vì ném lỗi, để LevelManager huỷ vào level một cách sạch sẽ.
public class LevelAddrFileReaderTests
{
    private const string TempDir = "Temp/LevelAddrTests";
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

    [Test]
    public void Read_RealLevel_HasBubbles()
    {
        LevelData data = LevelFileReader.Read(RealLevel);
        Assert.IsNotNull(data);
        Assert.Greater(data.BubbleCount, 0);
    }

    [Test]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.IsNull(LevelFileReader.Read($"{TempDir}/khong_co.json"));
        Assert.IsNull(LevelFileReader.Read(null));
        Assert.IsNull(LevelFileReader.Read(""));
    }

    [Test]
    public void Decode_EmptyOrNull_Fails()
    {
        Assert.IsFalse(LevelDataCodec.TryDecode("", out LevelData data));
        Assert.IsNull(data);
        Assert.IsFalse(LevelDataCodec.TryDecode(null, out data));
        Assert.IsFalse(LevelDataCodec.TryDecode("   ", out data));
    }

    [Test]
    public void Decode_PlainJsonNotEncrypted_Fails()
    {
        Assert.IsFalse(LevelDataCodec.TryDecode("{\"Level\":1}", out LevelData data));
        Assert.IsNull(data);
    }

    [Test]
    public void Decode_GarbageBase64_Fails()
    {
        Assert.IsFalse(LevelDataCodec.TryDecode("QUJDREVGRw==", out LevelData data));
        Assert.IsNull(data);
    }

    [Test]
    public void Decode_EncryptedButTruncatedJson_FailsWithoutThrowing()
    {
        string payload = DataSerializer.Encrypt("{\"Level\":");
        bool ok = true;
        Assert.DoesNotThrow(() => ok = LevelDataCodec.TryDecode(payload, out _));
        Assert.IsFalse(ok);
    }

    [Test]
    public void Read_CorruptFile_ReturnsNull()
    {
        string path = $"{TempDir}/LevelData_9.json";
        File.WriteAllText(path, "khong phai level");
        Assert.IsNull(LevelFileReader.Read(path));
    }

    [Test]
    public void Write_ThenRead_Roundtrip()
    {
        LevelData source = LevelFileReader.Read(RealLevel);
        string path = $"{TempDir}/sub/LevelData_1.json";

        Assert.IsTrue(LevelFileReader.Write(path, source, out string message), message);
        LevelData back = LevelFileReader.Read(path);

        Assert.IsNotNull(back);
        Assert.AreEqual(source.BubbleCount, back.BubbleCount);
        Assert.AreEqual(source.Difficulty, back.Difficulty);
        Assert.AreEqual(source.OrderQueue.Length, back.OrderQueue.Length);
    }

    [Test]
    public void TestLevelPath_FollowsConvention()
    {
        Assert.AreEqual("Assets/BaseGame/TextAssets/TestLevel/VersionA/LevelData_5.json",
            LevelFileReader.GetTestLevelPath("VersionA", 5));
    }
}
