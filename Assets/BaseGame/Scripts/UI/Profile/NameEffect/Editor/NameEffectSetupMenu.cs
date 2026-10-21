using Profile.NameEffects;
using Profile.ProfileItem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Profile.NameEffects.EditorTools
{
    /// <summary>
    /// Dựng sẵn effect mẫu để không phải kéo thả tay: tạo prefab Shine Sweep,
    /// tạo hai definition (None + Shine Sweep) và đăng ký vào database.
    /// Chạy lại nhiều lần không nhân bản — có sẵn thì dùng lại.
    /// </summary>
    public static class NameEffectSetupMenu
    {
        private const int NoneEffectId = 0;
        private const int ShineSweepEffectId = 1;

        private const string PrefabFolder = "Assets/BaseGame/Prefabs/UI/Profile/NameEffects";
        private const string DefinitionFolder = "Assets/BaseGame/ScriptableObjects/NameEffects";
        private const string ShineSweepPrefabPath = PrefabFolder + "/NameEffect_ShineSweep.prefab";
        private const string UserNameEffectPrefabPath = "Assets/BaseGame/Prefabs/UI/Profile/UserNameEffect.prefab";

        [MenuItem("Tools/Name Effects/1. Tạo effect mẫu Shine Sweep")]
        public static void CreateShineSweep()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(DefinitionFolder);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShineSweepPrefabPath);
            if (prefab == null)
                prefab = BuildShineSweepPrefab();

            NameEffectView view = prefab != null ? prefab.GetComponent<NameEffectView>() : null;

            NameEffectDefinition none = EnsureDefinition(NoneEffectId, "None", NameEffectRarity.Common, null);
            NameEffectDefinition shine = EnsureDefinition(ShineSweepEffectId, "Shine Sweep", NameEffectRarity.Rare, view);

            NameEffectDatabase database = NameEffectDatabase.Instance;
            database.EditorRegister(none);
            database.EditorRegister(shine);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = database;
            Debug.Log($"[NameEffect] Xong. Prefab: {ShineSweepPrefabPath}. Database: {AssetDatabase.GetAssetPath(database)}");
        }

        [MenuItem("Tools/Name Effects/2. Gắn NameEffectHost vào UserNameEffect.prefab")]
        public static void AttachHostToUserNameEffect()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UserNameEffectPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[NameEffect] Không mở được {UserNameEffectPrefabPath}");
                return;
            }

            try
            {
                var userNameEffect = root.GetComponent<UserNameEffect>();
                if (userNameEffect == null)
                {
                    Debug.LogError("[NameEffect] Prefab không có UserNameEffect.");
                    return;
                }

                var host = root.GetComponent<NameEffectHost>();
                if (host == null)
                    host = root.AddComponent<NameEffectHost>();

                var hostObject = new SerializedObject(host);
                hostObject.FindProperty("text").objectReferenceValue = userNameEffect.txtName;
                hostObject.FindProperty("container").objectReferenceValue = root.transform as RectTransform;
                hostObject.ApplyModifiedPropertiesWithoutUndo();

                var effectObject = new SerializedObject(userNameEffect);
                SerializedProperty hostProperty = effectObject.FindProperty("nameEffectHost");
                if (hostProperty != null)
                {
                    hostProperty.objectReferenceValue = host;
                    effectObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, UserNameEffectPrefabPath);
                Debug.Log("[NameEffect] Đã gắn NameEffectHost vào UserNameEffect.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildShineSweepPrefab()
        {
            var root = new GameObject("NameEffect_ShineSweep", typeof(RectTransform));
            StretchFull((RectTransform)root.transform);
            var view = root.AddComponent<NameEffectView>();

            // Không thêm RectMask2D: layer tự dựng mask theo hình chữ lúc chạy.
            var layerObject = new GameObject("ShineSweep", typeof(RectTransform));
            layerObject.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)layerObject.transform);
            layerObject.AddComponent<ShineSweepLayer>();

            view.EditorSetup(NameEffectPlacement.InFront, null);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShineSweepPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static NameEffectDefinition EnsureDefinition(int id, string displayName,
            NameEffectRarity rarity, NameEffectView viewPrefab)
        {
            string path = $"{DefinitionFolder}/NameEffect_{id:00}_{displayName.Replace(" ", string.Empty)}.asset";

            var definition = AssetDatabase.LoadAssetAtPath<NameEffectDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<NameEffectDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.EditorSetup(id, displayName, rarity, viewPrefab);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
