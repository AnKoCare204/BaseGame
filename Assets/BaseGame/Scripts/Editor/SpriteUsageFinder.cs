
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpriteUsageFinder : EditorWindow
{
    private Sprite targetSprite;
    private Vector2 scrollPosition;

    private readonly List<UsageInfo> results = new List<UsageInfo>();
    private int totalAssets;
    private bool searchCompleted;

    private static readonly HashSet<string> SearchExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab",
        ".unity",
        ".asset",
        ".mat",
        ".anim",
        ".controller",
        ".overrideController",
        ".playable",
        ".spriteatlas",
        ".mask",
        ".renderTexture",
        ".terrainlayer",
        ".lighting",
        ".guiskin"
    };

    private static readonly Regex HeaderRegex = new Regex(
        @"^--- !u!(?<type>\d+) &(?<id>-?\d+)",
        RegexOptions.Compiled
    );

    private static readonly Regex ReferenceRegex = new Regex(
        @"\{[^{}\r\n]*\bfileID:\s*(?<id>-?\d+)" +
        @"[^{}\r\n]*\bguid:\s*(?<guid>[0-9a-fA-F]{32})" +
        @"[^{}\r\n]*\}",
        RegexOptions.Compiled
    );

    private static readonly Regex GameObjectRegex = new Regex(
        @"^  m_GameObject:\s*\{fileID:\s*(?<id>-?\d+)",
        RegexOptions.Compiled
    );

    private static readonly Regex NameRegex = new Regex(
        @"^  m_Name:\s*(?<name>.*)$",
        RegexOptions.Compiled
    );

    private class ObjectInfo
    {
        public long FileId;
        public int TypeId;
        public long GameObjectId;
        public string Name;

        public readonly List<int> ReferenceLines = new List<int>();
    }

    private class UsageInfo
    {
        public string ObjectName;
        public string ComponentName;
        public string AssetPath;

        public long GameObjectId;
        public int Line;
    }

    [MenuItem("Tools/Sprite Usage Finder")]
    private static void OpenWindow()
    {
        GetWindow<SpriteUsageFinder>("Sprite Usage Finder");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "SPRITE USAGE FINDER",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Tim Sprite trong Project. Nhan Select de mo " +
            "Prefab/Scene va chon GameObject su dung Sprite.",
            MessageType.Info
        );

        EditorGUI.BeginChangeCheck();

        targetSprite = (Sprite)EditorGUILayout.ObjectField(
            "Target Sprite",
            targetSprite,
            typeof(Sprite),
            false
        );

        if (EditorGUI.EndChangeCheck())
        {
            results.Clear();
            totalAssets = 0;
            searchCompleted = false;
        }

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(targetSprite == null))
        {
            if (GUILayout.Button(
                "Find Sprite Usage",
                GUILayout.Height(35)))
            {
                FindSpriteUsage();
            }
        }

        if (!searchCompleted)
            return;

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            $"Completed - {results.Count} references / " +
            $"{totalAssets} assets",
            EditorStyles.boldLabel
        );

        GUILayout.Space(5);

        DrawResults();
    }

    private void DrawResults()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition
        );

        foreach (UsageInfo usage in results)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(
                usage.ObjectName,
                EditorStyles.boldLabel
            );

            EditorGUILayout.LabelField(
                $"{usage.ComponentName} | Line {usage.Line}",
                EditorStyles.miniLabel
            );

            EditorGUILayout.LabelField(
                usage.AssetPath,
                EditorStyles.wordWrappedMiniLabel
            );

            GUILayout.Space(3);

            if (GUILayout.Button(
                "Select Object",
                GUILayout.Height(25)))
            {
                OpenAndSelect(usage);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void FindSpriteUsage()
    {
        if (targetSprite == null)
            return;

        results.Clear();
        totalAssets = 0;
        searchCompleted = false;

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                targetSprite,
                out string targetGuid,
                out long targetFileId))
        {
            Debug.LogError(
                "[Sprite Usage] Cannot get Sprite GUID/File ID."
            );
            return;
        }

        if (EditorSettings.serializationMode !=
            SerializationMode.ForceText)
        {
            Debug.LogWarning(
                "[Sprite Usage] Please set Asset Serialization " +
                "to Force Text."
            );
        }

        string projectRoot = Path.GetDirectoryName(
            Application.dataPath
        );

        string[] assetPaths = AssetDatabase
            .GetAllAssetPaths()
            .Where(path =>
                path.StartsWith(
                    "Assets/",
                    StringComparison.OrdinalIgnoreCase
                )
                && SearchExtensions.Contains(
                    Path.GetExtension(path)
                )
            )
            .ToArray();

        bool cancelled = false;

        try
        {
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];

                if (i % 25 == 0)
                {
                    cancelled =
                        EditorUtility.DisplayCancelableProgressBar(
                            "Sprite Usage Finder",
                            assetPath,
                            (float)i / assetPaths.Length
                        );

                    if (cancelled)
                        break;
                }

                string fullPath = Path.Combine(
                    projectRoot,
                    assetPath
                );

                if (!File.Exists(fullPath))
                    continue;

                try
                {
                    List<UsageInfo> usages = ScanAsset(
                        fullPath,
                        assetPath,
                        targetGuid,
                        targetFileId
                    );

                    if (usages.Count == 0)
                        continue;

                    totalAssets++;
                    results.AddRange(usages);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[Sprite Usage] Cannot scan {assetPath}: " +
                        ex.Message
                    );
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (cancelled)
        {
            results.Clear();
            Debug.LogWarning("[Sprite Usage] Search cancelled.");
            return;
        }

        searchCompleted = true;

        // Log each result with the actual GameObject
        // as context whenever it can be resolved.
        foreach (UsageInfo usage in results)
        {
            UnityEngine.Object context = GetLogContext(usage);

            Debug.Log(
                $"[Sprite Usage] FOUND\n" +
                $"Sprite: {targetSprite.name}\n" +
                $"Object: {usage.ObjectName}\n" +
                $"Component: {usage.ComponentName}\n" +
                $"Asset: {usage.AssetPath}\n" +
                $"Line: {usage.Line}",
                context
            );
        }

        Debug.Log(
            $"[Sprite Usage] SEARCH COMPLETED\n" +
            $"Sprite: {targetSprite.name}\n" +
            $"Total references: {results.Count}\n" +
            $"Total assets: {totalAssets}\n" +
            $"Objects:\n" +
            string.Join(
                "\n",
                results
                    .Select(x => x.ObjectName)
                    .Distinct()
                    .Select(x => "- " + x)
            )
        );

        Repaint();
    }

    // -------------------------------------------------
    // SCAN YAML
    // -------------------------------------------------

    private static List<UsageInfo> ScanAsset(
        string fullPath,
        string assetPath,
        string targetGuid,
        long targetFileId)
    {
        List<ObjectInfo> objects = new List<ObjectInfo>();

        ObjectInfo currentObject = null;
        int lineNumber = 0;

        foreach (string line in File.ReadLines(fullPath))
        {
            lineNumber++;

            Match headerMatch = HeaderRegex.Match(line);

            if (headerMatch.Success)
            {
                currentObject = new ObjectInfo
                {
                    FileId = long.Parse(
                        headerMatch.Groups["id"].Value
                    ),
                    TypeId = int.Parse(
                        headerMatch.Groups["type"].Value
                    ),
                    Name = ""
                };

                objects.Add(currentObject);
                continue;
            }

            if (currentObject == null)
            {
                currentObject = new ObjectInfo
                {
                    Name = ""
                };

                objects.Add(currentObject);
            }

            Match nameMatch = NameRegex.Match(line);

            if (nameMatch.Success &&
                string.IsNullOrEmpty(currentObject.Name))
            {
                currentObject.Name =
                    nameMatch.Groups["name"].Value
                        .Trim()
                        .Trim('"');
            }

            Match gameObjectMatch =
                GameObjectRegex.Match(line);

            if (gameObjectMatch.Success)
            {
                currentObject.GameObjectId = long.Parse(
                    gameObjectMatch.Groups["id"].Value
                );
            }

            if (line.IndexOf(
                    targetGuid,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            foreach (Match match in ReferenceRegex.Matches(line))
            {
                if (!string.Equals(
                        match.Groups["guid"].Value,
                        targetGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!long.TryParse(
                        match.Groups["id"].Value,
                        out long foundFileId))
                {
                    continue;
                }

                if (foundFileId != targetFileId)
                    continue;

                currentObject.ReferenceLines.Add(lineNumber);
            }
        }

        Dictionary<long, string> gameObjectNames =
            new Dictionary<long, string>();

        foreach (ObjectInfo obj in objects)
        {
            if (obj.TypeId == 1)
            {
                gameObjectNames[obj.FileId] =
                    string.IsNullOrEmpty(obj.Name)
                        ? $"GameObject ({obj.FileId})"
                        : obj.Name;
            }
        }

        List<UsageInfo> usages = new List<UsageInfo>();

        string assetName =
            Path.GetFileNameWithoutExtension(assetPath);

        foreach (ObjectInfo obj in objects)
        {
            if (obj.ReferenceLines.Count == 0)
                continue;

            long gameObjectId = obj.TypeId == 1
                ? obj.FileId
                : obj.GameObjectId;

            string objectName = assetName;

            if (gameObjectId != 0 &&
                gameObjectNames.TryGetValue(
                    gameObjectId,
                    out string foundName))
            {
                objectName = foundName;
            }
            else if (!string.IsNullOrEmpty(obj.Name))
            {
                objectName = obj.Name;
            }

            foreach (int line in obj.ReferenceLines)
            {
                usages.Add(new UsageInfo
                {
                    ObjectName = objectName,
                    ComponentName = GetComponentName(
                        obj.TypeId
                    ),
                    AssetPath = assetPath,
                    GameObjectId = gameObjectId,
                    Line = line
                });
            }
        }

        return usages;
    }

    // -------------------------------------------------
    // RESOLVE OBJECT BY FILE ID
    // -------------------------------------------------

    private static UnityEngine.Object ResolveGlobalObject(
        string assetPath,
        long fileId,
        int identifierType)
    {
        if (fileId == 0)
            return null;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        if (string.IsNullOrEmpty(guid))
            return null;

        // Unity GlobalObjectId uses unsigned IDs.
        ulong unsignedId = unchecked((ulong)fileId);

        string idString =
            $"GlobalObjectId_V1-{identifierType}-" +
            $"{guid}-{unsignedId}-0";

        if (!GlobalObjectId.TryParse(
                idString,
                out GlobalObjectId globalId))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
            globalId
        );
    }

    private static GameObject ResolveSceneObject(
        UsageInfo usage)
    {
        Scene scene = SceneManager.GetSceneByPath(
            usage.AssetPath
        );

        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        return ResolveGlobalObject(
            usage.AssetPath,
            usage.GameObjectId,
            2
        ) as GameObject;
    }

    private static GameObject ResolvePrefabAssetObject(
        UsageInfo usage)
    {
        if (usage.GameObjectId == 0)
            return null;

        // Imported Asset.
        GameObject go = ResolveGlobalObject(
            usage.AssetPath,
            usage.GameObjectId,
            1
        ) as GameObject;

        if (go != null)
            return go;

        // Source Asset.
        go = ResolveGlobalObject(
            usage.AssetPath,
            usage.GameObjectId,
            3
        ) as GameObject;

        if (go != null)
            return go;

        // Fallback: inspect the Prefab asset hierarchy.
        GameObject root =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                usage.AssetPath
            );

        if (root == null)
            return null;

        foreach (Transform t in root.GetComponentsInChildren<
                     Transform>(true))
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    t.gameObject,
                    out string guid,
                    out long localId))
            {
                continue;
            }

            if (localId == usage.GameObjectId)
                return t.gameObject;
        }

        return null;
    }

    // -------------------------------------------------
    // FIND OBJECT INSIDE PREFAB STAGE
    // -------------------------------------------------

    private static GameObject ResolvePrefabStageObject(
        UsageInfo usage,
        GameObject stageRoot)
    {
        GameObject sourceObject =
            ResolvePrefabAssetObject(usage);

        if (sourceObject == null || stageRoot == null)
            return null;

        // Build the exact hierarchy path using sibling indices.
        // This also handles GameObjects with duplicate names.
        List<int> siblingIndices = new List<int>();

        Transform current = sourceObject.transform;

        while (current.parent != null)
        {
            siblingIndices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        siblingIndices.Reverse();

        Transform target = stageRoot.transform;

        foreach (int index in siblingIndices)
        {
            if (index < 0 || index >= target.childCount)
                return null;

            target = target.GetChild(index);
        }

        return target.gameObject;
    }

    // -------------------------------------------------
    // CONSOLE LOG CONTEXT
    // -------------------------------------------------

    private static UnityEngine.Object GetLogContext(
        UsageInfo usage)
    {
        string extension = Path.GetExtension(
            usage.AssetPath
        );

        if (extension.Equals(
                ".unity",
                StringComparison.OrdinalIgnoreCase))
        {
            GameObject sceneObject =
                ResolveSceneObject(usage);

            if (sceneObject != null)
                return sceneObject;
        }

        if (extension.Equals(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null &&
                stage.assetPath == usage.AssetPath)
            {
                GameObject stageObject =
                    ResolvePrefabStageObject(
                        usage,
                        stage.prefabContentsRoot
                    );

                if (stageObject != null)
                    return stageObject;
            }

            GameObject assetObject =
                ResolvePrefabAssetObject(usage);

            if (assetObject != null)
                return assetObject;
        }

        return AssetDatabase.LoadMainAssetAtPath(
            usage.AssetPath
        );
    }

    // -------------------------------------------------
    // OPEN PREFAB / SCENE AND SELECT OBJECT
    // -------------------------------------------------

    private static void OpenAndSelect(UsageInfo usage)
    {
        string extension = Path.GetExtension(
            usage.AssetPath
        );

        if (extension.Equals(
                ".unity",
                StringComparison.OrdinalIgnoreCase))
        {
            SelectSceneObject(usage);
            return;
        }

        if (extension.Equals(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
        {
            SelectPrefabObject(usage);
            return;
        }

        // Other asset types do not necessarily
        // contain GameObjects.
        UnityEngine.Object asset =
            AssetDatabase.LoadMainAssetAtPath(
                usage.AssetPath
            );

        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        else
        {
            Debug.LogWarning(
                $"[Sprite Usage] Asset not found: " +
                usage.AssetPath
            );
        }
    }

    private static void SelectSceneObject(UsageInfo usage)
    {
        Scene scene = SceneManager.GetSceneByPath(
            usage.AssetPath
        );

        // Open the Scene if it is not currently loaded.
        // Additive mode preserves the other open Scenes.
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(
                usage.AssetPath,
                OpenSceneMode.Additive
            );
        }

        GameObject target = ResolveSceneObject(usage);

        if (target == null)
        {
            Debug.LogWarning(
                $"[Sprite Usage] Cannot resolve GameObject " +
                $"{usage.ObjectName} in {usage.AssetPath}. " +
                $"The Scene has been opened."
            );

            return;
        }

        SelectGameObject(target);
    }

    private static void SelectPrefabObject(UsageInfo usage)
    {
        if (usage.GameObjectId == 0)
        {
            SelectAssetFallback(usage);
            return;
        }

        // Resolve the source object before opening Prefab Mode.
        // This preserves the hierarchy path of the asset object.
        GameObject sourceObject =
            ResolvePrefabAssetObject(usage);

        if (sourceObject == null)
        {
            SelectAssetFallback(usage);
            return;
        }

        var stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage == null ||
            stage.assetPath != usage.AssetPath)
        {
            stage = PrefabStageUtility.OpenPrefab(
                usage.AssetPath
            );
        }

        if (stage == null)
            return;

        GameObject target = ResolvePrefabStageObject(
            usage,
            stage.prefabContentsRoot
        );

        if (target == null)
        {
            Debug.LogWarning(
                $"[Sprite Usage] Prefab opened, but could not " +
                $"resolve Object: {usage.ObjectName}"
            );

            return;
        }

        SelectGameObject(target);
    }

    private static void SelectAssetFallback(UsageInfo usage)
    {
        UnityEngine.Object asset =
            AssetDatabase.LoadMainAssetAtPath(
                usage.AssetPath
            );

        if (asset == null)
            return;

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        Debug.LogWarning(
            $"[Sprite Usage] Cannot resolve the exact GameObject. " +
            $"Selected asset instead: {usage.AssetPath}"
        );
    }

    private static void SelectGameObject(GameObject target)
    {
        if (target == null)
            return;

        Selection.activeGameObject = target;

        EditorGUIUtility.PingObject(target);

        // Reveal the object in the Hierarchy window.
        EditorApplication.ExecuteMenuItem(
            "Window/General/Hierarchy"
        );

        EditorGUIUtility.PingObject(target);

        // Focus the object in Scene View when applicable.
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    // -------------------------------------------------
    // COMPONENT NAMES
    // -------------------------------------------------

    private static string GetComponentName(int classId)
    {
        switch (classId)
        {
            case 1: return "GameObject";
            case 4: return "Transform";
            case 21: return "Material";
            case 74: return "AnimationClip";
            case 95: return "Animator";
            case 111: return "Animation";
            case 114: return "MonoBehaviour";
            case 212: return "SpriteRenderer";
            case 213: return "Sprite";
            case 224: return "RectTransform";

            default:
                return $"Serialized Object ({classId})";
        }
    }
}