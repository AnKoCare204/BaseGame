using UnityEditor;
using UnityEngine;

/// Các sửa asset một lần của đợt tối ưu 2026-09-25 (spec mục A4): gỡ LightningSheet khỏi 3 material pattern, trỏ DesignConfig sang Pearl ngoài folder Editor.
public static class PerfAssetFixes
{
    private static readonly string[] PatternMaterials =
    {
        "Assets/BaseGame/Graphics/Materials/Pattern/Rotate.mat", "Assets/BaseGame/Graphics/Materials/Pattern/PatternScoll.mat", "Assets/BaseGame/Graphics/Sprites/MatTilingSprite.mat",
    };

    [MenuItem("Tools/FishSort/Perf/Clear Pattern Material Layers")]
    public static void ClearPatternMaterialLayers()
    {
        foreach (string path in PatternMaterials)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogError("[PerfAssetFixes] thiếu " + path); continue; }
            bool layerOn = mat.IsKeywordEnabled("_ENABLETEXTURELAYER1_ON") || mat.IsKeywordEnabled("_ENABLETEXTURELAYER2_ON") || mat.IsKeywordEnabled("_TEXTURELAYER1_ON") || mat.IsKeywordEnabled("_TEXTURELAYER2_ON");
            if (layerOn) { Debug.LogWarning("[PerfAssetFixes] " + path + " đang bật texture layer, giữ nguyên"); continue; }
            mat.SetTexture("_TextureLayer1Texture", null);
            mat.SetTexture("_TextureLayer2Texture", null);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            Debug.Log("[PerfAssetFixes] đã gỡ texture layer khỏi " + path);
        }
    }

    [MenuItem("Tools/FishSort/Perf/Apply Audio Import Settings")]
    public static void ApplyAudioImportSettings()
    {
        int music = 0, sfx = 0;
        foreach (string g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/BaseGame/AudioClips" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            AudioImporter ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) continue;
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            bool isMusic = path.Contains("/music_");
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            if (isMusic)
            {
                s.loadType = AudioClipLoadType.Streaming;   // không giải nén nguyên bài vào RAM, không kẹt main thread lúc đổi nhạc
                s.quality = 0.6f;
                s.preloadAudioData = false;
                music++;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.preloadAudioData = true;                 // giải nén ở màn loading thay vì lúc phát lần đầu
                sfx++;
            }
            ai.defaultSampleSettings = s;
            ai.ClearSampleSettingOverride("Android");
            ai.ClearSampleSettingOverride("iOS");
            ai.loadInBackground = true;
            ai.SaveAndReimport();
        }
        Debug.Log($"[PerfAssetFixes] audio: {music} nhạc streaming, {sfx} sfx preload");
    }

    [MenuItem("Tools/FishSort/Perf/Remove Null SetActive Feedbacks (ActivityFirstTut)")]
    public static void RemoveNullSetActiveFeedbacks()
    {
        string path = "Assets/BaseGame/Addressable/UI/ActivityFirstTut.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int removed = 0;
        foreach (MoreMountains.Feedbacks.MMF_Player player in root.GetComponentsInChildren<MoreMountains.Feedbacks.MMF_Player>(true))
            removed += player.FeedbacksList.RemoveAll(f => f is MoreMountains.Feedbacks.MMF_SetActive s && s.TargetGameObject == null);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[PerfAssetFixes] ActivityFirstTut: xoá " + removed + " MMF_SetActive không có target");
    }

    private const string VariantsPath = "Assets/BaseGame/Graphics/Shaders/FishSortShaderVariants.shadervariants";

    /// ShaderUtil.ClearCurrentShaderVariantCollection / SaveCurrentShaderVariantCollection là internal (nút "Save to asset" trong Graphics Settings dùng chúng).
    private static bool CallShaderUtil(string name, params object[] args)
    {
        System.Reflection.MethodInfo m = typeof(ShaderUtil).GetMethod(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (m == null) { Debug.LogError("[PerfAssetFixes] ShaderUtil." + name + " không có trong phiên bản Unity này"); return false; }
        m.Invoke(null, args);
        return true;
    }

    [MenuItem("Tools/FishSort/Perf/Shader Variants: Clear Tracked")]
    public static void ClearTrackedVariants()
    {
        if (CallShaderUtil("ClearCurrentShaderVariantCollection")) Debug.Log("[PerfAssetFixes] đã xoá danh sách variant đang theo dõi");
    }

    [MenuItem("Tools/FishSort/Perf/Shader Variants: Save Tracked + Assign")]
    public static void SaveTrackedVariants()
    {
        if (!CallShaderUtil("SaveCurrentShaderVariantCollection", VariantsPath)) return;
        AssetDatabase.ImportAsset(VariantsPath);
        ShaderVariantCollection svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
        Debug.Log($"[PerfAssetFixes] lưu {svc.shaderCount} shader / {svc.variantCount} variant vào {VariantsPath}");

        SerializedObject gfx = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
        SerializedProperty list = gfx.FindProperty("m_PreloadedShaders");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = svc;
        gfx.ApplyModifiedPropertiesWithoutUndo();

        string prefabPath = "Assets/BaseGame/Prefabs/Manager/LoadingManager.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        SerializedObject so = new SerializedObject(root.GetComponent<LoadingManager>());
        so.FindProperty("<ShaderVariants>k__BackingField").objectReferenceValue = svc;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[PerfAssetFixes] đã gán vào GraphicsSettings.m_PreloadedShaders và LoadingManager.ShaderVariants");
    }

    [MenuItem("Tools/FishSort/Perf/Repoint DesignConfig Pearl")]
    public static void RepointDesignConfigPearl()
    {
        Object config = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/GlobalConfig/DesignConfig.asset");
        Sprite target = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BaseGame/Graphics/Sprites/Obstacle/Pearl.png");
        if (config == null || target == null) { Debug.LogError("[PerfAssetFixes] thiếu DesignConfig hoặc Pearl.png"); return; }
        SerializedObject so = new SerializedObject(config);
        SerializedProperty p = so.GetIterator();
        int changed = 0;
        while (p.NextVisible(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
            string refPath = AssetDatabase.GetAssetPath(p.objectReferenceValue);
            if (!refPath.StartsWith("Assets/BaseGame/Editor/Png/Pearl.png")) continue;
            p.objectReferenceValue = p.objectReferenceValue is Sprite ? target : (Object)target.texture;
            changed++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        Debug.Log("[PerfAssetFixes] DesignConfig: đổi " + changed + " tham chiếu Pearl");
    }
}
