using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class TMPFontMaterialCopierWindow : EditorWindow
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int GradientScaleId = Shader.PropertyToID("_GradientScale");
    private static readonly int TextureWidthId = Shader.PropertyToID("_TextureWidth");
    private static readonly int TextureHeightId = Shader.PropertyToID("_TextureHeight");

    [SerializeField] private TMP_FontAsset sourceFont;
    [SerializeField] private TMP_FontAsset targetFont;
    [SerializeField] private bool overwriteExisting = true;

    private Vector2 scroll;
    private string result;
    private MessageType resultType = MessageType.Info;

    [MenuItem("Tools/Fish Sort/Copy TMP Font Materials")]
    private static void Open()
    {
        var window = GetWindow<TMPFontMaterialCopierWindow>("Copy Font Materials");
        window.minSize = new Vector2(440f, 330f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Copy TMP Font Materials", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Copy toàn bộ material preset của Font 1 sang Font 2. Tool giữ style của material nguồn, " +
            "nhưng thay atlas và thông số SDF theo Font 2.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Font 1 (nguồn)", sourceFont,
            typeof(TMP_FontAsset), false);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Font 2 (đích)", targetFont,
            typeof(TMP_FontAsset), false);
        overwriteExisting = EditorGUILayout.ToggleLeft("Ghi đè material trùng tên (giữ nguyên GUID)",
            overwriteExisting);
        if (EditorGUI.EndChangeCheck()) result = string.Empty;

        EditorGUILayout.Space(6f);
        DrawPreview();
        EditorGUILayout.Space(8f);

        string validation = Validate();
        using (new EditorGUI.DisabledScope(validation != null))
        {
            if (GUILayout.Button("Copy Materials", GUILayout.Height(32f)))
                CopyMaterials();
        }

        if (validation != null)
            EditorGUILayout.HelpBox(validation, MessageType.Warning);
        else if (!string.IsNullOrEmpty(result))
            EditorGUILayout.HelpBox(result, resultType);
    }

    private void DrawPreview()
    {
        if (sourceFont == null) return;

        List<Material> materials = FindSourceMaterials(sourceFont);
        EditorGUILayout.LabelField($"Material tìm thấy: {materials.Count}", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(80f), GUILayout.MaxHeight(150f));
        if (materials.Count == 0)
        {
            EditorGUILayout.LabelField("Không có material preset cùng thư mục và bắt đầu bằng tên Font 1.");
        }
        else
        {
            foreach (Material material in materials)
            {
                string targetName = targetFont == null
                    ? material.name
                    : GetTargetMaterialName(sourceFont, targetFont, material);
                EditorGUILayout.LabelField($"{material.name}  →  {targetName}");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private string Validate()
    {
        if (sourceFont == null || targetFont == null) return "Hãy chọn đủ Font 1 và Font 2.";
        if (sourceFont == targetFont) return "Font nguồn và font đích phải khác nhau.";
        if (sourceFont.material == null || targetFont.material == null)
            return "Một trong hai font không có material mặc định.";
        if (targetFont.material.mainTexture == null)
            return "Font 2 không có atlas texture.";
        if (FindSourceMaterials(sourceFont).Count == 0)
            return "Không tìm thấy material preset của Font 1.";
        return null;
    }

    private void CopyMaterials()
    {
        string targetFontPath = AssetDatabase.GetAssetPath(targetFont);
        string targetFolder = Path.GetDirectoryName(targetFontPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(targetFolder)) return;

        List<Material> sources = FindSourceMaterials(sourceFont);
        int created = 0;
        int overwritten = 0;
        int skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (Material source in sources)
            {
                string targetName = GetTargetMaterialName(sourceFont, targetFont, source);
                string targetPath = $"{targetFolder}/{targetName}.mat";
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);

                if (existing != null)
                {
                    if (!overwriteExisting)
                    {
                        skipped++;
                        continue;
                    }

                    Undo.RecordObject(existing, "Overwrite TMP font material");
                    EditorUtility.CopySerialized(source, existing);
                    existing.name = targetName;
                    ApplyTargetFontAtlas(existing, targetFont);
                    EditorUtility.SetDirty(existing);
                    overwritten++;
                    continue;
                }

                var copy = new Material(source) { name = targetName };
                ApplyTargetFontAtlas(copy, targetFont);
                AssetDatabase.CreateAsset(copy, targetPath);
                created++;
            }
        }
        catch (Exception exception)
        {
            result = $"Copy thất bại: {exception.Message}";
            resultType = MessageType.Error;
            Debug.LogException(exception);
            return;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        result = $"Hoàn tất: tạo {created}, ghi đè {overwritten}, bỏ qua {skipped} material.";
        resultType = MessageType.Info;
    }

    private static List<Material> FindSourceMaterials(TMP_FontAsset font)
    {
        var result = new List<Material>();
        if (font == null) return result;

        string fontPath = AssetDatabase.GetAssetPath(font);
        string folder = Path.GetDirectoryName(fontPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) return result;

        string prefix = font.name + " ";
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.name.StartsWith(prefix, StringComparison.Ordinal))
                result.Add(material);
        }

        result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        return result;
    }

    private static string GetTargetMaterialName(TMP_FontAsset source, TMP_FontAsset target, Material material)
    {
        string suffix = material.name.Substring(source.name.Length);
        return target.name + suffix;
    }

    private static void ApplyTargetFontAtlas(Material material, TMP_FontAsset target)
    {
        Material targetDefault = target.material;
        Texture atlas = targetDefault.mainTexture;

        if (material.HasProperty(MainTexId))
            material.SetTexture(MainTexId, atlas);
        if (material.HasProperty(GradientScaleId) && targetDefault.HasProperty(GradientScaleId))
            material.SetFloat(GradientScaleId, targetDefault.GetFloat(GradientScaleId));
        if (material.HasProperty(TextureWidthId) && targetDefault.HasProperty(TextureWidthId))
            material.SetFloat(TextureWidthId, targetDefault.GetFloat(TextureWidthId));
        if (material.HasProperty(TextureHeightId) && targetDefault.HasProperty(TextureHeightId))
            material.SetFloat(TextureHeightId, targetDefault.GetFloat(TextureHeightId));

        ShaderUtilities.UpdateShaderRatios(material);
    }
}
