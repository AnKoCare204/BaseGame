using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Test quy tắc import texture: chọn đúng rule theo đường dẫn, bỏ qua texture ngoài phạm vi, áp 2 lần không đổi gì.
public class PerfTextureImportRulesTests
{
    private const string TempPng = "Assets/BaseGame/Graphics/Sprites/UI/Other/__perf_rules_test.png";

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(TempPng)) AssetDatabase.DeleteAsset(TempPng);
    }

    [Test]
    public void Resolve_AtlasSources_AreUncompressedNoMip()
    {
        // Sprite nằm trong atlas: nguồn để RGBA32, atlas mới nén (nén 2 lần làm hỏng icon).
        foreach (string path in new[]
                 {
                     "Assets/BaseGame/Graphics/Sprites/UI/Home/ic_home.png", "Assets/BaseGame/Graphics/Sprites/UI/warning/T_hard.png",
                     "Assets/BaseGame/Graphics/Sprites/UI/Loading/Logo.png", "Assets/BaseGame/Graphics/Sprites/InGame/FishTank.png",
                     "Assets/BaseGame/Graphics/Sprites/GamePlay/Tay512.png",
                 })
        {
            TextureRule rule = TextureImportRules.Resolve(path);
            Assert.IsNotNull(rule, path);
            Assert.IsTrue(rule.Uncompressed, path);
            Assert.AreEqual(TextureImporterFormat.RGBA32, rule.Format, path);
            Assert.AreEqual(2048, rule.MaxSize, path);
            Assert.AreEqual(false, rule.Mipmaps, path);
        }
    }

    [Test]
    public void Resolve_UnpackedUiAndSpine_AreCompressed()
    {
        TextureRule tmp = TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/UI/TmpSpriteSheet/TMPSpriteAssetGenerator.png");
        Assert.IsFalse(tmp.Uncompressed);
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, tmp.Format);
        Assert.AreEqual(TextureImporterFormat.ASTC_4x4, TextureImportRules.Resolve("Assets/BaseGame/Graphics/Spine/removeAds/removeAds.png").Format);
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/Common/ABILOGO.png").Format);
    }

    [Test]
    public void Resolve_IconFolders_Max512()
    {
        Assert.AreEqual(512, TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/FishInBubble/Fish_01.png").MaxSize);
        Assert.AreEqual(512, TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/Obstacle/Pearl.png").MaxSize);
    }

    [Test]
    public void Resolve_BackgroundsAndSsu_KeepMaxAndFormat()
    {
        TextureRule bg = TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/UI/Backgrounds/bg_home.png");
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, bg.Format);
        Assert.AreEqual(2048, bg.MaxSize);
        Assert.AreEqual(1024, TextureImportRules.Resolve("Assets/Sprite Shaders Ultimate/Textures/Patterns/LightningSheet.png").MaxSize);
        Assert.IsNull(TextureImportRules.Resolve("Assets/BaseGame/Graphics/Textures/T_SoftBubbleRingInner.png").Mipmaps, "giữ nguyên mipmap cho texture 3D");
    }

    [Test]
    public void Resolve_ParticleFolder_IsAstc6x6KeepMip()
    {
        TextureRule rule = TextureImportRules.Resolve("Assets/BaseGame/Graphics/Particle/confetti.png");
        Assert.IsNotNull(rule, "texture particle gameplay cũng phải nén ASTC");
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, rule.Format);
        Assert.IsNull(rule.Mipmaps);
    }

    [Test]
    public void Resolve_OutOfScope_IsNull()
    {
        Assert.IsNull(TextureImportRules.Resolve("Assets/Feel/MMTools/Foundation/MMFeedbacks/Textures/x.png"));
        Assert.IsNull(TextureImportRules.Resolve("Assets/BaseGame/Editor/Png/Pearl.png"));
        Assert.IsNull(TextureImportRules.Resolve("Assets/TextMesh Pro/Sprites/EmojiOne.png"));
        Assert.IsNull(TextureImportRules.Resolve("Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png"));
    }

    private static TextureImporter ImportTempPng()
    {
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        File.WriteAllBytes(TempPng, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TempPng, ImportAssetOptions.ForceUpdate);
        return (TextureImporter)AssetImporter.GetAtPath(TempPng);
    }

    [Test]
    public void OnPreprocessTexture_NewTextureGetsRulesAndTag()
    {
        TextureImporter importer = ImportTempPng();
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        Assert.IsTrue(android.overridden, "postprocessor phải áp quy tắc ngay lần import đầu");
        Assert.AreEqual(TextureImporterFormat.RGBA32, android.format, "UI/Other nằm trong atlas nên nguồn không nén");
        Assert.IsTrue(importer.userData.Contains(TextureImportRules.UserDataTag));
    }

    [Test]
    public void Apply_SetsPlatformsAndIsIdempotent()
    {
        TextureImporter importer = ImportTempPng();
        TextureRule rule = TextureImportRules.Resolve(TempPng);
        // Làm lệch cài đặt như một texture cũ chưa từng qua quy tắc.
        importer.userData = "";
        importer.mipmapEnabled = true;
        TextureImporterPlatformSettings def0 = importer.GetDefaultPlatformTextureSettings();
        def0.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(def0);
        TextureImporterPlatformSettings android0 = importer.GetPlatformTextureSettings("Android");
        android0.overridden = false;
        importer.SetPlatformTextureSettings(android0);

        bool first = TextureImportRules.Apply(importer, rule);
        bool second = TextureImportRules.Apply(importer, rule);

        Assert.IsTrue(first, "lần đầu phải đổi cài đặt");
        Assert.IsFalse(second, "lần hai không được đổi gì");
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
        TextureImporterPlatformSettings def = importer.GetDefaultPlatformTextureSettings();
        Assert.IsTrue(android.overridden);
        Assert.AreEqual(TextureImporterFormat.RGBA32, android.format);
        Assert.AreEqual(2048, android.maxTextureSize);
        Assert.IsFalse(android.crunchedCompression);
        Assert.AreEqual(TextureImporterFormat.RGBA32, ios.format);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, def.textureCompression);
        Assert.IsFalse(def.crunchedCompression);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.IsTrue(importer.userData.Contains(TextureImportRules.UserDataTag));
    }

    [Test]
    public void Resolve_UiRawFolder_IsCompressedNotAtlasSource()
    {
        // Sprites/UI/Raw: texture UI dung thang lam Texture2D (ParticleImage, material), khong pack vao atlas nen phai nen ASTC.
        TextureRule raw = TextureImportRules.Resolve("Assets/BaseGame/Graphics/Sprites/UI/Raw/Coin3.png");
        Assert.IsNotNull(raw);
        Assert.IsFalse(raw.Uncompressed);
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, raw.Format);
        Assert.AreEqual(2048, raw.MaxSize);
    }
}
