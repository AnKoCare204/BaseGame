using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Menu Tools/FishSort/Fish Toon/...:
/// - Apply Toon Shader To Fish Materials: chuyển mọi material trong Graphics/Models/Fish Material sang FishSort/Fish Toon Outline
///   (_BaseMap / _BaseColor / _Cull cùng tên nên giữ nguyên giá trị) và thêm 2 pass của shader vào FishSortShaderVariants (preload).
/// - Outline Style Flat / Texture: đặt _OutlineTexMix = 0 / 1 cho mọi material đó, để so 2 kiểu màu viền. Chỉnh chi tiết thì chọn
///   nhiều material cùng lúc trong Project rồi sửa ở Inspector.
public static class FishToonMaterialTools
{
    public const string ShaderName = "FishSort/Fish Toon Outline";
    public const string MaterialFolder = "Assets/BaseGame/Graphics/Models/Fish Material";
    private const string VariantsPath = "Assets/BaseGame/Graphics/Shaders/FishSortShaderVariants.shadervariants";

    private static IEnumerable<Material> FishMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material != null) yield return material;
        }
    }

    [MenuItem("Tools/FishSort/Fish Toon/Apply Toon Shader To Fish Materials")]
    public static void Apply()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[FishToon] Không tìm thấy shader {ShaderName}");
            return;
        }

        int changed = 0;
        foreach (Material material in FishMaterials())
        {
            if (material.shader == shader) continue;
            material.shader = shader;
            material.renderQueue = -1; // theo Queue của shader (Geometry)
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            changed++;
        }

        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
        if (collection != null)
        {
            collection.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipelineDefaultUnlit));
            collection.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline));
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
        }
        else
        {
            Debug.LogError($"[FishToon] Không tìm thấy {VariantsPath}");
        }

        Debug.Log($"[FishToon] {changed} material chuyển sang {ShaderName}");
    }

    [MenuItem("Tools/FishSort/Fish Toon/Outline Style Flat")]
    public static void UseFlatColor() => SetOutlineTexMix(0f);

    [MenuItem("Tools/FishSort/Fish Toon/Outline Style Texture")]
    public static void UseTextureColor() => SetOutlineTexMix(1f);

    private static void SetOutlineTexMix(float mix)
    {
        foreach (Material material in FishMaterials())
        {
            if (material.shader.name != ShaderName) continue;
            material.SetFloat("_OutlineTexMix", mix);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
    }
}
