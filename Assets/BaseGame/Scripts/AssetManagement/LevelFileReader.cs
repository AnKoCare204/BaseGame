#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CoreData
{
    /// <summary>
    /// Chỉ trong Editor: đọc / ghi file level thẳng theo đường dẫn (tương đối với thư mục project), không qua Addressables.
    /// Dùng cho Play mode mặc định trong Editor và cho các tool level.
    /// </summary>
    public static class LevelFileReader
    {
        public const string TestLevelFolder = "Assets/BaseGame/TextAssets/TestLevel";

        /// <summary>null khi thiếu file hoặc file không giải mã được.</summary>
        public static LevelData Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelFileReader] Đọc {path} lỗi: {e.Message}");
                return null;
            }

            return LevelDataCodec.TryDecode(text, out LevelData data) ? data : null;
        }

        public static bool Write(string path, LevelData data, out string message)
        {
            if (data == null || string.IsNullOrEmpty(path))
            {
                message = "Thiếu data hoặc đường dẫn";
                return false;
            }
            string payload = DataSerializer.Serialize(data);
            if (string.IsNullOrEmpty(payload))
            {
                message = $"Mã hoá {path} thất bại, không ghi file";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, payload);
            }
            catch (Exception e)
            {
                message = $"Ghi {path} lỗi - {e.Message}";
                return false;
            }

            message = "";
            return true;
        }

        public static string GetTestLevelPath(string version, int level)
        {
            return $"{TestLevelFolder}/{version}/{LevelAddress.LevelFilePrefix}{level}.json";
        }

        /// <summary>File biến thể x của level n trong thư mục của tool vẽ level (LevelDesignData.GetVariantFileName).</summary>
        public static string GetVariantLevelPath(int level, int variant)
        {
            return $"{LevelAddress.VariantFolder}/{LevelAddress.LevelFilePrefix}{level}_{variant}.json";
        }

        /// <summary>Bảng biến thể theo file đang có trong thư mục biến thể. Đọc lại mỗi lần gọi: tool có thể vừa lưu biến thể mới.</summary>
        public static LevelVariantIndex GetVariantIndex()
        {
            if (!Directory.Exists(LevelAddress.VariantFolder)) return LevelVariantIndex.Empty;
            List<string> names = new List<string>();
            foreach (string file in Directory.GetFiles(LevelAddress.VariantFolder, LevelAddress.LevelFilePrefix + "*.json"))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }

            return LevelVariantIndex.FromFileNames(names);
        }

        /// <summary>Tên các thư mục con của TestLevel, mỗi thư mục là một bộ test.</summary>
        public static List<string> GetTestVersions()
        {
            List<string> versions = new List<string>();
            if (!Directory.Exists(TestLevelFolder)) return versions;
            foreach (string directory in Directory.GetDirectories(TestLevelFolder))
            {
                string name = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(name) && !name.StartsWith(".")) versions.Add(name);
            }

            versions.Sort(StringComparer.Ordinal);
            return versions;
        }
    }
}
#endif
