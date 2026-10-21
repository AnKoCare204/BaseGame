using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Một quy tắc import: khớp theo tiền tố đường dẫn (đường dẫn dùng '/'), quy tắc đứng trước thắng.
public sealed class TextureRule
{
    public string Name;
    public string[] PathPrefixes;          // khớp nếu đường dẫn bắt đầu bằng 1 trong các tiền tố
    public string[] ExcludePrefixes = Array.Empty<string>();
    public TextureImporterFormat Format;   // dùng cho Android và iPhone
    public int MaxSize;                    // 0 = giữ nguyên
    public bool? Mipmaps;                  // null = giữ nguyên
    public bool Uncompressed;              // nguồn của atlas: không nén ở importer, atlas mới nén (tránh nén 2 lần)
}

/// Quy tắc import texture của FishSort (spec 2026-09-25, mục A1): ASTC cho Android/iOS, Default platform Compressed, không crunch.
/// - OnPreprocessTexture: áp cho texture mới (chưa có tag trong userData), rồi ghi tag để không đè chỉnh tay về sau.
/// - Menu Tools/FishSort/Perf/Reapply Texture Import Rules: áp lại cho toàn bộ texture trong phạm vi.
public sealed class TextureImportRules : AssetPostprocessor
{
    public const string UserDataTag = "TextureImportRules:v1";

    private const string Sprites = "Assets/BaseGame/Graphics/Sprites/";

    private static readonly TextureRule[] Rules =
    {
        new TextureRule { Name = "UI ngoài atlas", PathPrefixes = new[] { Sprites + "UI/Backgrounds/", Sprites + "UI/TmpSpriteSheet/", Sprites + "UI/Raw/", Sprites + "Backgrounds/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 2048, Mipmaps = false },
        new TextureRule { Name = "Icon 512", PathPrefixes = new[] { Sprites + "FishInBubble/", Sprites + "Obstacle/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 512, Mipmaps = false },
        new TextureRule { Name = "Nguồn atlas UI", PathPrefixes = new[] { Sprites + "UI/" }, Format = TextureImporterFormat.RGBA32, Uncompressed = true, MaxSize = 2048, Mipmaps = false },
        new TextureRule { Name = "Nguồn atlas gameplay", PathPrefixes = new[] { Sprites + "InGame/", Sprites + "GamePlay/" }, Format = TextureImporterFormat.RGBA32, Uncompressed = true, MaxSize = 2048, Mipmaps = false },
        new TextureRule { Name = "Sprite khác", PathPrefixes = new[] { Sprites }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 2048, Mipmaps = false },
        new TextureRule { Name = "Spine", PathPrefixes = new[] { "Assets/BaseGame/Graphics/Spine/" }, Format = TextureImporterFormat.ASTC_4x4, MaxSize = 2048, Mipmaps = false },
        new TextureRule { Name = "Texture 3D", PathPrefixes = new[] { "Assets/BaseGame/Graphics/Textures/", "Assets/BaseGame/Graphics/FishdomFish/", "Assets/BaseGame/Graphics/Models/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 0, Mipmaps = null },
        new TextureRule { Name = "Particle", PathPrefixes = new[] { "Assets/BaseGame/Graphics/Particle/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 0, Mipmaps = null },
        new TextureRule { Name = "VFX", PathPrefixes = new[] { "Assets/VFX/", "Assets/AssetKits/ParticleImage/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 1024, Mipmaps = null },
        new TextureRule { Name = "SSU", PathPrefixes = new[] { "Assets/Sprite Shaders Ultimate/Textures/" }, Format = TextureImporterFormat.ASTC_6x6, MaxSize = 1024, Mipmaps = null },
    };

    private static readonly string[] Roots =
    {
        "Assets/BaseGame/Graphics", "Assets/VFX", "Assets/AssetKits/ParticleImage", "Assets/Sprite Shaders Ultimate/Textures",
    };

    private static readonly string[] Platforms = { "Android", "iPhone" };

    public static TextureRule Resolve(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        string path = assetPath.Replace('\\', '/');
        if (path.Contains("/Editor/")) return null;
        foreach (TextureRule rule in Rules)
        {
            bool match = false;
            foreach (string prefix in rule.PathPrefixes) if (path.StartsWith(prefix, StringComparison.Ordinal)) { match = true; break; }
            if (!match) continue;
            foreach (string ex in rule.ExcludePrefixes) if (path.StartsWith(ex, StringComparison.Ordinal)) { match = false; break; }
            if (match) return rule;
        }
        return null;
    }

    /// Áp quy tắc lên importer (không reimport). Trả true nếu có thay đổi.
    public static bool Apply(TextureImporter importer, TextureRule rule)
    {
        if (importer == null || rule == null) return false;
        bool changed = false;

        TextureImporterCompression compression = rule.Uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;

        TextureImporterPlatformSettings def = importer.GetDefaultPlatformTextureSettings();
        if (def.textureCompression != compression || def.crunchedCompression || (rule.MaxSize > 0 && def.maxTextureSize != rule.MaxSize))
        {
            def.textureCompression = compression;
            def.crunchedCompression = false;
            if (rule.MaxSize > 0) def.maxTextureSize = rule.MaxSize;
            importer.SetPlatformTextureSettings(def);
            changed = true;
        }

        foreach (string platform in Platforms)
        {
            TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings(platform);
            int maxSize = rule.MaxSize > 0 ? rule.MaxSize : ps.maxTextureSize;
            if (ps.overridden && ps.format == rule.Format && ps.maxTextureSize == maxSize && !ps.crunchedCompression
                && ps.textureCompression == compression && ps.compressionQuality == 50) continue;
            ps.overridden = true;
            ps.format = rule.Format;
            ps.maxTextureSize = maxSize;
            ps.textureCompression = compression;
            ps.compressionQuality = 50;
            ps.crunchedCompression = false;
            importer.SetPlatformTextureSettings(ps);
            changed = true;
        }

        if (rule.Mipmaps.HasValue && importer.mipmapEnabled != rule.Mipmaps.Value)
        {
            importer.mipmapEnabled = rule.Mipmaps.Value;
            changed = true;
        }

        if (!importer.userData.Contains(UserDataTag))
        {
            importer.userData = string.IsNullOrEmpty(importer.userData) ? UserDataTag : importer.userData + ";" + UserDataTag;
            changed = true;
        }

        return changed;
    }

    private void OnPreprocessTexture()
    {
        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null || importer.userData.Contains(UserDataTag)) return; // đã áp một lần, tôn trọng chỉnh tay
        TextureRule rule = Resolve(assetPath);
        if (rule == null) return;
        Apply(importer, rule);
    }

    [MenuItem("Tools/FishSort/Perf/Reapply Texture Import Rules")]
    public static void ReapplyAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", Roots);
        int changed = 0, skipped = 0;
        List<string> toReimport = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureRule rule = Resolve(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (rule == null || importer == null) { skipped++; continue; }
            if (Apply(importer, rule)) { toReimport.Add(path); changed++; }
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string path in toReimport) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[TextureImportRules] đã áp {changed} texture, bỏ qua {skipped} (ngoài phạm vi) trên {guids.Length}");
    }
}
