using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// Dựng atlas UI theo spec 2026-09-25 mục A2: chuyển background / texture dùng thô ra folder không pack, tạo 6 atlas V2 (đồng bộ packable mỗi lần chạy),
/// cấu hình 2 atlas cũ, pack và log số trang. Chạy lại được nhiều lần.
public static class UiAtlasBuilder
{
    private const string Sprites = "Assets/BaseGame/Graphics/Sprites/";
    private const string AtlasDir = "Assets/BaseGame/Graphics/Atlas/";

    private static readonly (string from, string to)[] Moves =
    {
        (Sprites + "UI/Home/bg_home.png", Sprites + "UI/Backgrounds/bg_home.png"),
        (Sprites + "UI/Loading/bg_loading.png", Sprites + "UI/Backgrounds/bg_loading.png"),
        (Sprites + "UI/Daily/bg_daily.png", Sprites + "UI/Backgrounds/bg_daily.png"),
        (Sprites + "UI/Daily/bg_decor_top.png", Sprites + "UI/Backgrounds/bg_decor_top.png"),
        (Sprites + "InGame/BGFix23.09.png", Sprites + "Backgrounds/BGFix23.09.png"),
        (Sprites + "NonAtlas/0.icon_coin 1.png", Sprites + "UI/Other/0.icon_coin 1.png"),
        // TMP dùng thẳng texture sprite sheet này, không được pack vào atlas (sẽ nhân đôi bộ nhớ)
        (Sprites + "UI/SpriteSheet/TMPSpriteAssetGenerator.png", Sprites + "UI/TmpSpriteSheet/TMPSpriteAssetGenerator.png"),
        (Sprites + "UI/SpriteSheet/TMPSpriteAssetGenerator.asset", Sprites + "UI/TmpSpriteSheet/TMPSpriteAssetGenerator.asset"),
        (Sprites + "UI/SpriteSheet/TMPSpriteAssetGenerator_TMPSpriteAsset.asset", Sprites + "UI/TmpSpriteSheet/TMPSpriteAssetGenerator_TMPSpriteAsset.asset"),
        // ParticleImage / material dùng thẳng Texture2D (không phải Sprite): để trong folder pack thì bản RGBA32 vào build và còn bị pack thừa
        (Sprites + "UI/Effect/Coin3.png", Sprites + "UI/Raw/Coin3.png"),
        (Sprites + "UI/Effect/light.png", Sprites + "UI/Raw/light.png"),
        (Sprites + "UI/Effect/star1.png", Sprites + "UI/Raw/star1.png"),
        (Sprites + "UI/Effect/blink.png", Sprites + "UI/Raw/blink.png"),
    };

    /// UI_Loading tách riêng: ActivityLoading được lồng trong LoadingManager.prefab của scene build-in nên atlas chứa nó bị nhân đôi
    /// scene + Addressables; atlas riêng chỉ nhân đôi 3 sprite nhỏ thay vì cả trang UI_InGame.
    private static readonly (string name, string[] folders, TextureImporterFormat format)[] NewAtlases =
    {
        ("UI_Common", new[] { "UI/SpriteSheet", "UI/Other", "UI/Profile", "UI/Setting" }, TextureImporterFormat.ASTC_6x6),
        ("UI_Home", new[] { "UI/Home", "UI/Daily" }, TextureImporterFormat.ASTC_6x6),
        ("UI_Shop", new[] { "UI/Shop", "UI/PopupBooster", "UI/NoAds" }, TextureImporterFormat.ASTC_6x6),
        ("UI_InGame", new[] { "UI/Gameplay", "UI/warning", "UI/Tutorial" }, TextureImporterFormat.ASTC_4x4),
        ("UI_Loading", new[] { "UI/Loading" }, TextureImporterFormat.ASTC_4x4),
        ("UI_Popup", new[] { "UI/WinLose", "UI/PopupGames", "UI/Effect" }, TextureImporterFormat.ASTC_6x6),
    };

    [MenuItem("Tools/FishSort/Perf/Rebuild UI Atlases")]
    public static void Rebuild()
    {
        MoveOutOfAtlases();
        foreach (var a in NewAtlases) CreateOrSync(AtlasDir + a.name + ".spriteatlasv2", a.folders, a.format, 2048);
        Configure(AtlasDir + "GamePlayAtlas.spriteatlasv2", TextureImporterFormat.ASTC_6x6, 2048); // 4 sprite 512² + padding không vừa 1024², 2048 cho 1 trang
        Configure(AtlasDir + "InGame.spriteatlasv2", TextureImporterFormat.ASTC_6x6, 2048);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var all = new List<SpriteAtlas>();
        foreach (string g in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasDir })) all.Add(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(g)));
        SpriteAtlasUtility.PackAtlases(all.ToArray(), EditorUserBuildSettings.activeBuildTarget);
        LogPages(all);
    }

    private static void MoveOutOfAtlases()
    {
        Directory.CreateDirectory(Sprites + "UI/Backgrounds");
        Directory.CreateDirectory(Sprites + "UI/TmpSpriteSheet");
        Directory.CreateDirectory(Sprites + "UI/Raw");
        Directory.CreateDirectory(Sprites + "Backgrounds");
        AssetDatabase.Refresh();
        foreach (var m in Moves)
        {
            if (!File.Exists(m.from)) continue; // đã chuyển ở lần chạy trước
            string err = AssetDatabase.MoveAsset(m.from, m.to);
            if (!string.IsNullOrEmpty(err)) Debug.LogError("[UiAtlasBuilder] " + err);
        }

        // Di chuyển giữ tag TextureImportRules trong userData nên postprocessor không áp lại; áp quy tắc của folder mới tại đây.
        foreach (var m in Moves)
        {
            TextureImporter importer = AssetImporter.GetAtPath(m.to) as TextureImporter;
            if (importer != null && TextureImportRules.Apply(importer, TextureImportRules.Resolve(m.to))) importer.SaveAndReimport();
        }
    }

    /// Tạo atlas nếu chưa có; luôn ghi lại danh sách packable theo bảng (giữ .meta nên GUID không đổi) rồi cấu hình importer.
    private static void CreateOrSync(string path, string[] folders, TextureImporterFormat format, int maxSize)
    {
        SpriteAtlasAsset asset = new SpriteAtlasAsset();
        var packables = new List<Object>();
        foreach (string f in folders)
        {
            Object folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Sprites + f);
            if (folder == null) Debug.LogError("[UiAtlasBuilder] thiếu folder " + Sprites + f); else packables.Add(folder);
        }
        asset.Add(packables.ToArray());
        SpriteAtlasAsset.Save(asset, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Configure(path, format, maxSize);
    }

    private static void Configure(string path, TextureImporterFormat format, int maxSize)
    {
        SpriteAtlasImporter imp = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
        if (imp == null) { Debug.LogError("[UiAtlasBuilder] không có importer cho " + path); return; }
        imp.packingSettings = new SpriteAtlasPackingSettings { padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = false, blockOffset = 1 };
        imp.textureSettings = new SpriteAtlasTextureSettings { generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear, readable = false, anisoLevel = 1 };
        imp.SetPlatformSettings(new TextureImporterPlatformSettings { name = "DefaultTexturePlatform", maxTextureSize = maxSize, textureCompression = TextureImporterCompression.Compressed, crunchedCompression = false, compressionQuality = 50 });
        foreach (string platform in new[] { "Android", "iPhone" })
            imp.SetPlatformSettings(new TextureImporterPlatformSettings { name = platform, overridden = true, format = format, maxTextureSize = maxSize, textureCompression = TextureImporterCompression.Compressed, crunchedCompression = false, compressionQuality = 50 });
        imp.includeInBuild = true;
        imp.SaveAndReimport();
    }

    private static void LogPages(List<SpriteAtlas> atlases)
    {
        var sb = new StringBuilder();
        foreach (SpriteAtlas atlas in atlases)
        {
            var sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);
            var pages = new Dictionary<string, int>();
            int unpacked = 0;
            foreach (Sprite s in sprites)
            {
                if (s == null) continue;
                if (!s.packed) { unpacked++; continue; }
                string key = s.texture.width + "x" + s.texture.height + " " + s.texture.format;
                pages[key] = pages.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            sb.Append(atlas.name).Append(": ").Append(atlas.spriteCount).Append(" sprite, ").Append(pages.Count).Append(" trang, chưa pack ").Append(unpacked).Append(" | ");
            foreach (var kv in pages) sb.Append(kv.Key).Append(" (").Append(kv.Value).Append(") ");
            sb.AppendLine();
        }
        Debug.Log("[UiAtlasBuilder]\n" + sb);
    }
}
