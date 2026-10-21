using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

/// <summary>
/// Bảng ảnh 2D của từng loài cá, dùng cho tool vẽ level (và bất cứ chỗ nào cần icon cá mà không muốn dựng model).
///
/// ID ở đây là ID trong FullFishModelConfigList của FishModelGlobalConfig, không phải số trên tên file: ID đánh theo
/// thứ tự trong list (ID 0 là prefab Fish_1, ID 24 là Fish_41) nên hai số này lệch nhau. Việc ghép theo tên prefab chỉ
/// làm lúc bấm quét, kết quả nằm thẳng trong asset: mở Inspector ra là thấy ID nào đi với ảnh nào và sửa tay được.
/// Đổi thứ tự hay thêm bớt model trong FishModelGlobalConfig thì phải quét lại.
/// </summary>
[CreateAssetMenu(fileName = "FishIconGlobalConfig", menuName = "GlobalConfigs/FishIconGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class FishIconGlobalConfig : GlobalConfig<FishIconGlobalConfig>
{
    [field: SerializeField, FolderPath, Tooltip("Thư mục chứa ảnh cá; nút quét bên dưới đọc mọi file .png trong đây")]
    public string IconFolder { get; private set; } = "Assets/BaseGame/Graphics/Sprites/FishInBubble";

    [field: SerializeField, Tooltip("ID là ID trong FullFishModelConfigList; ảnh là file trùng tên prefab của model: ID 0 (Fish_1) -> Fish_01.png")]
    public FishIconConfig[] FishIconConfigs { get; private set; } = Array.Empty<FishIconConfig>();

    public int Count => FishIconConfigs?.Length ?? 0;

    /// <summary>Ảnh của loài id, null nếu bảng chưa có entry cho id đó. Không fallback sang entry khác.</summary>
    public Sprite GetIcon(int id)
    {
        if (FishIconConfigs == null) return null;
        foreach (FishIconConfig entry in FishIconConfigs)
        {
            if (entry != null && entry.ID == id) return entry.Icon;
        }

        return null;
    }

    public bool HasIcon(int id) => GetIcon(id) != null;

    /// <summary>
    /// Khoá để ghép tên prefab của loài với tên file ảnh: viết thường, số đuôi bỏ số 0 đệm (prefab Fish_1 và file
    /// Fish_01 đều ra "fish_1"). Giữ cả phần chữ nên Prop_7 không ghép nhầm sang Fish_07.
    /// </summary>
    public static string GetNameKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        int at = name.Length;
        while (at > 0 && char.IsDigit(name[at - 1])) at--;
        string head = name.Substring(0, at).ToLowerInvariant();
        return at < name.Length && int.TryParse(name.Substring(at), out int number) ? head + number : head;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Dựng lại bảng từ FullFishModelConfigList của FishModelGlobalConfig: mỗi element cho một entry, ID là ID của
    /// element, ảnh là file .png trong IconFolder trùng tên với prefab FishModel của element đó.
    ///
    /// Phải đi qua LoadAllAssetsAtPath rồi lọc Sprite: LoadAssetAtPath&lt;Sprite&gt; trả null với file import ở
    /// Sprite Mode = Multiple vì asset chính lúc đó là Texture2D.
    /// </summary>
    [Button("Quét thư mục ảnh cá")]
    public void ScanIconFolder()
    {
        if (!System.IO.Directory.Exists(IconFolder))
        {
            Debug.LogError($"FishIconGlobalConfig: không thấy thư mục {IconFolder}", this);
            return;
        }

        FishModelGlobalConfig models = FishModelGlobalConfig.Instance;
        if (models == null)
        {
            Debug.LogError("FishIconGlobalConfig: không thấy FishModelGlobalConfig", this);
            return;
        }

        var skipped = new List<string>();
        var duplicated = new List<string>();
        var files = new Dictionary<string, string>();
        var sprites = new Dictionary<string, Sprite>();

        foreach (string path in System.IO.Directory.GetFiles(IconFolder, "*.png"))
        {
            string assetPath = path.Replace('\\', '/');
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string key = GetNameKey(fileName);

            if (files.TryGetValue(key, out string owner))
            {
                duplicated.Add($"{owner} và {fileName}");
                continue;
            }

            Sprite sprite = null;
            foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite s)
                {
                    sprite = s;
                    break;
                }
            }

            if (sprite == null)
            {
                skipped.Add($"{fileName} (không có sprite con - Texture Type phải là Sprite)");
                continue;
            }

            files[key] = fileName;
            sprites[key] = sprite;
        }

        var found = new List<FishIconConfig>();
        var noIcon = new List<string>();
        var seenIds = new HashSet<int>();
        var used = new HashSet<string>();

        foreach (FishModelConfig entry in models.GetFullFishModelConfigList())
        {
            // ID âm là model vật cản dùng chung (cá ẩn, spawner, key), không phải loài cá nên không cần icon.
            if (entry == null || entry.ID < 0 || !seenIds.Add(entry.ID)) continue;
            if (entry.FishModel == null)
            {
                noIcon.Add($"ID {entry.ID} (chưa gán model)");
                continue;
            }

            string prefabName = System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GetAssetPath(entry.FishModel));
            string key = GetNameKey(prefabName);
            if (!sprites.TryGetValue(key, out Sprite sprite))
            {
                noIcon.Add($"ID {entry.ID} ({prefabName})");
                continue;
            }

            used.Add(key);
            found.Add(new FishIconConfig { ID = entry.ID, Icon = sprite });
        }

        foreach (KeyValuePair<string, string> file in files)
        {
            if (!used.Contains(file.Key)) skipped.Add($"{file.Value} (không model nào trùng tên)");
        }

        found.Sort((a, b) => a.ID.CompareTo(b.ID));
        FishIconConfigs = found.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();

        string message = $"FishIconGlobalConfig: ghép {found.Count} ảnh từ {IconFolder}";
        if (noIcon.Count > 0) message += $"\nModel chưa có ảnh: {string.Join("; ", noIcon)}";
        if (duplicated.Count > 0) message += $"\nTrùng tên (chỉ giữ file đầu): {string.Join("; ", duplicated)}";
        if (skipped.Count > 0) message += $"\nBỏ qua: {string.Join("; ", skipped)}";
        if (noIcon.Count > 0 || duplicated.Count > 0 || skipped.Count > 0) Debug.LogWarning(message, this);
        else Debug.Log(message, this);
    }
#endif
}

[Serializable]
public class FishIconConfig
{
    public int ID;
    [PreviewField] public Sprite Icon;
}
