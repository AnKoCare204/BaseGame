using CoreData;
using NUnit.Framework;

/// Editor đọc thẳng file biến thể trong thư mục của tool vẽ level.
public class LevelAddrVariantFileTests
{
    [Test]
    public void VariantPath_FollowsToolFileName()
    {
        Assert.AreEqual("Assets/BaseGame/TextAssets/LevelDataVariant/LevelData_25_3.json", LevelFileReader.GetVariantLevelPath(25, 3));
        Assert.AreEqual(LevelDesignData.GetVariantFileName(25, 3), System.IO.Path.GetFileName(LevelFileReader.GetVariantLevelPath(25, 3)));
    }

    [Test]
    public void VariantIndex_FromFolder_DoesNotThrow()
    {
        // Thư mục có thể chỉ có .gitkeep; chỉ cần đọc được và mọi số đều hợp lệ.
        LevelVariantIndex index = LevelFileReader.GetVariantIndex();
        Assert.IsNotNull(index);
        Assert.GreaterOrEqual(index.LevelCount, 0);
    }
}
