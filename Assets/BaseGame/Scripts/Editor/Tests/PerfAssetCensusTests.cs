using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

/// Kiểm kê asset sau đợt tối ưu 2026-09-25 (sửa theo review cuối):
/// - texture nguồn atlas (quy tắc Uncompressed, RGBA32) không được asset trong build dùng thẳng như Texture2D (fileID 2800000), vì khi đó
///   bản RGBA32 vào build (Coin3 5.6 MB) và còn bị pack thừa vào atlas;
/// - 3 activity booster không còn tham chiếu sprite settings_BG đã xoá;
/// - sprite UI/Loading nằm trong atlas riêng UI_Loading: ActivityLoading được lồng trong LoadingManager.prefab của scene build-in nên atlas
///   chứa chúng bị nhân đôi scene + Addressables, atlas riêng chỉ nhân đôi vài sprite nhỏ thay vì cả trang UI_InGame.
public class PerfAssetCensusTests
{
    private const string Sprites = "Assets/BaseGame/Graphics/Sprites/";
    private const string AtlasDir = "Assets/BaseGame/Graphics/Atlas/";

    private static bool IsSerializedText(string path)
    {
        return path.EndsWith(".prefab") || path.EndsWith(".mat") || path.EndsWith(".asset") || path.EndsWith(".unity") || path.EndsWith(".controller");
    }

    /// Điểm vào của build: scene bật trong Build Settings, mọi entry Addressables, mọi asset trong Resources. Prefab không được các gốc này kéo tới thì không vào build.
    private static List<string> ShippedRoots()
    {
        var roots = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) if (scene.enabled) roots.Add(scene.path);
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        Assert.IsNotNull(settings, "Addressables chưa cấu hình");
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            foreach (AddressableAssetEntry entry in group.entries) if (!string.IsNullOrEmpty(entry.AssetPath) && !entry.IsFolder) roots.Add(entry.AssetPath);
        }
        foreach (string g in AssetDatabase.FindAssets("", new[] { "Assets/Resources" })) roots.Add(AssetDatabase.GUIDToAssetPath(g));
        return roots;
    }

    [Test]
    public void ShippedAssets_DoNotUseUncompressedAtlasSourcesAsTexture2D()
    {
        List<string> roots = ShippedRoots();
        Assert.IsNotEmpty(roots);

        string[] deps = AssetDatabase.GetDependencies(roots.ToArray(), true);
        var texts = new List<(string path, string text)>();
        foreach (string d in deps) if (IsSerializedText(d) && !d.Contains("/Editor/")) texts.Add((d, File.ReadAllText(d)));

        var violations = new List<string>();
        foreach (string dep in deps)
        {
            TextureRule rule = TextureImportRules.Resolve(dep);
            if (rule == null || !rule.Uncompressed) continue;
            string needle = "fileID: 2800000, guid: " + AssetDatabase.AssetPathToGUID(dep);
            foreach ((string path, string text) in texts) if (text.Contains(needle)) violations.Add(dep + " <- " + path);
        }
        Assert.IsEmpty(violations, "Texture nguồn atlas (RGBA32) đang được dùng thẳng như Texture2D:\n" + string.Join("\n", violations));
    }

    [Test]
    public void BoosterActivities_HaveNoDanglingSettingsBgSprite()
    {
        const string deletedGuid = "5dfe5e66a302a8a46a07cabbdcb4a666";
        foreach (string name in new[] { "ActivityBoosterShuffle", "ActivityBoosterPackup", "ActivityBoosterFishingRod" })
        {
            string path = "Assets/BaseGame/Addressable/UI/" + name + ".prefab";
            Assert.IsTrue(File.Exists(path), path);
            StringAssert.DoesNotContain(deletedGuid, File.ReadAllText(path), path + " còn tham chiếu settings_BG.png đã xoá");
        }
    }

    [Test]
    public void LoadingSprites_PackedInOwnAtlas_NotInUiInGame()
    {
        string loadingFolderGuid = AssetDatabase.AssetPathToGUID(Sprites + "UI/Loading");
        Assert.IsNotEmpty(loadingFolderGuid);
        StringAssert.DoesNotContain("guid: " + loadingFolderGuid, File.ReadAllText(AtlasDir + "UI_InGame.spriteatlasv2"), "UI_InGame không được pack UI/Loading");
        string ownAtlas = AtlasDir + "UI_Loading.spriteatlasv2";
        Assert.IsTrue(File.Exists(ownAtlas), ownAtlas + " phải tồn tại");
        StringAssert.Contains("guid: " + loadingFolderGuid, File.ReadAllText(ownAtlas), "UI_Loading phải pack folder UI/Loading");
    }
}
