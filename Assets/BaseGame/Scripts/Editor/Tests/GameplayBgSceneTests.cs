using System.IO;
using NUnit.Framework;
using UnityEditor;

/// Kiểm GamePlayScene sau khi thay BG bằng GameplayBgLayers (đọc file scene, không mở scene).
/// Chạy qua menu Tools/FishSort/Run Gameplay Bg Tests.
public class GameplayBgSceneTests
{
    private const string ScenePath = "Assets/BaseGame/Scenes/GamePlayScene.unity";

    [Test]
    public void LayersPrefab_IsInScene()
    {
        string guid = AssetDatabase.AssetPathToGUID("Assets/BaseGame/Prefabs/Gameplay/GameplayBgLayers.prefab");
        Assert.IsNotEmpty(guid);
        Assert.IsTrue(File.ReadAllText(ScenePath).Contains(guid), "GamePlayScene chưa có GameplayBgLayers");
    }

    [Test]
    public void OldBackgroundTexture_NotReferenced()
    {
        // Object tắt vẫn giữ tham chiếu: Unity nạp texture cũ (~0.95 MB ASTC) cùng scene dù không bao giờ vẽ.
        string guid = AssetDatabase.AssetPathToGUID("Assets/BaseGame/Graphics/Sprites/Backgrounds/BGFix5.10.png");
        Assert.IsNotEmpty(guid);
        Assert.IsFalse(File.ReadAllText(ScenePath).Contains(guid), "ImageBg cũ vẫn tham chiếu BGFix5.10.png");
    }
}
