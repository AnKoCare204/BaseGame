using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// Liệt kê asset trong các folder mục tiêu mà không scene / prefab / material / ScriptableObject / anim nào (ngoài chính nó) tham chiếu,
/// không phải Addressable, không nằm trong Resources. Atlas không tính là gốc (để tìm được BG rác đang bị pack).
/// Kết quả ghi ra Library/UnreferencedAssets.txt và Console.
public static class UnreferencedAssetFinder
{
    private static readonly string[] TargetFolders =
    {
        "Assets/BaseGame/Graphics/Sprites", "Assets/BaseGame/Graphics/Spine", "Assets/BaseGame/Prefabs/Ingame/Effect",
        "Assets/BaseGame/Prefabs/UI/ModalWin", "Assets/BaseGame/Prefabs/UI", "Assets/VFX/prefabs", "Assets/BaseGame/Materials", "Assets/BaseGame",
    };

    private static readonly string[] SkipFragments = { "/Editor/", "/Demo/", "/Test/", "/Plugins/", "/Feel/", "Sprite Shaders Ultimate/Demo" };

    [MenuItem("Tools/FishSort/Perf/Find Unreferenced Assets")]
    public static void Run()
    {
        var referenced = new Dictionary<string, int>();
        string[] rootGuids = AssetDatabase.FindAssets("t:Scene t:Prefab t:Material t:ScriptableObject t:AnimationClip t:AnimatorController t:PlayableAsset");
        foreach (string g in rootGuids)
        {
            string root = AssetDatabase.GUIDToAssetPath(g);
            if (Skip(root)) continue;
            foreach (string dep in AssetDatabase.GetDependencies(root, true))
                if (dep != root) referenced[dep] = referenced.TryGetValue(dep, out int n) ? n + 1 : 1;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var sb = new StringBuilder();
        int total = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Texture2D t:Material t:Prefab t:ScriptableObject t:TextAsset t:Model", TargetFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (Skip(path) || path.Contains("/Resources/") || path.EndsWith(".cs")) continue;
            if (referenced.ContainsKey(path)) continue;
            if (settings != null && settings.FindAssetEntry(g) != null) continue;
            sb.AppendLine(path); total++;
        }

        string outPath = "Library/UnreferencedAssets.txt";
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[UnreferencedAssetFinder] {total} asset không tham chiếu, ghi ở {outPath}\n{sb}");
    }

    private static bool Skip(string path)
    {
        foreach (string f in SkipFragments) if (path.Contains(f)) return true;
        return false;
    }
}
