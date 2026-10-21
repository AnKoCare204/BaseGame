using System;

namespace CoreData
{
    /// <summary>
    /// Quy ước address và label của level trong Addressables. Toàn hàm thuần, không gọi Unity API.
    /// Level production: address "Levels/Level_{n}", label "lvl_{(n-1)/ChunkSize}" (mỗi label một bundle).
    /// Level test: address "Levels/TEST/{Version}/Level_{n}", label "lvl_test_{Version}" (mỗi bộ test một bundle).
    /// </summary>
    public static class LevelAddress
    {
        public const int ChunkSize = 100;
        public const string LabelPrefix = "lvl_";
        public const string TestLabelPrefix = "lvl_test_";
        public const string AddressPrefix = "Levels/Level_";
        public const string TestAddressPrefix = "Levels/TEST/";
        public const string LevelFilePrefix = "LevelData_";
        public const string VariantAddressPrefix = "Levels/Variant/";

        /// <summary>
        /// Thư mục biến thể LevelData_{n}_{x}.json: tool vẽ level ghi vào, Setup đóng gói, Editor (chế độ đọc file) đọc thẳng.
        /// Hằng thuần nên dùng được cả ở bản build.
        /// </summary>
        public const string VariantFolder = "Assets/BaseGame/TextAssets/LevelDataVariant";

        public static string GetLevelAddress(int level) => $"{AddressPrefix}{level}";

        public static string GetTestAddress(int level, string version) => $"{TestAddressPrefix}{version}/Level_{level}";

        /// <summary>Biến thể x của level n: "Levels/Variant/Level_{n}_{x}". Label là chunk của n (xem GetChunkLabelForAddress).</summary>
        public static string GetVariantAddress(int level, int variant) => $"{VariantAddressPrefix}Level_{level}_{variant}";

        public static bool IsVariantAddress(string address)
        {
            return address != null && address.StartsWith(VariantAddressPrefix, StringComparison.Ordinal);
        }

        /// <summary>"Levels/Variant/Level_12_3" -> (12, 3). Sai dạng, hoặc n / x nhỏ hơn 1, là không hợp lệ.</summary>
        public static bool TryParseVariantAddress(string address, out int level, out int variant)
        {
            level = 0;
            variant = 0;
            if (!IsVariantAddress(address)) return false;
            return TryParseLevelVariantPair(address.Substring(VariantAddressPrefix.Length), "Level_", out level, out variant);
        }

        /// <summary>
        /// "LevelData_12_3" -> (12, 3): n = level gốc, x = số lần chơi lại để bật biến thể. Như TryParseLevelFileName, tên có ký tự
        /// thừa ("LevelData_12_3 (1)" do Windows đặt khi copy đè) là không hợp lệ.
        /// </summary>
        public static bool TryParseVariantFileName(string nameWithoutExtension, out int level, out int variant)
        {
            return TryParseLevelVariantPair(nameWithoutExtension, LevelFilePrefix, out level, out variant);
        }

        // "{prefix}{n}_{x}", n và x toàn chữ số, đều >= 1.
        private static bool TryParseLevelVariantPair(string text, string prefix, out int level, out int variant)
        {
            level = 0;
            variant = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string rest = text.Substring(prefix.Length);
            int split = rest.IndexOf('_');
            if (split <= 0 || split == rest.Length - 1) return false;
            if (!IsDigits(rest, 0, split) || !IsDigits(rest, split + 1, rest.Length)) return false;
            if (!int.TryParse(rest.Substring(0, split), out int parsedLevel) ||
                !int.TryParse(rest.Substring(split + 1), out int parsedVariant)) return false;
            if (parsedLevel < 1 || parsedVariant < 1) return false;
            level = parsedLevel;
            variant = parsedVariant;
            return true;
        }

        private static bool IsDigits(string text, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (text[i] < '0' || text[i] > '9') return false;
            }

            return true;
        }

        public static string GetChunkLabel(int level)
        {
            int chunk = Math.Max(0, (level - 1) / ChunkSize);
            return $"{LabelPrefix}{chunk}";
        }

        public static string GetTestLabel(string version) => $"{TestLabelPrefix}{version}";

        public static bool IsTestLabel(string label)
        {
            return label != null && label.StartsWith(TestLabelPrefix, StringComparison.Ordinal);
        }

        public static bool IsTestAddress(string address)
        {
            return address != null && address.StartsWith(TestAddressPrefix, StringComparison.Ordinal);
        }

        public static string GetVersionFromTestLabel(string label)
        {
            return IsTestLabel(label) ? label.Substring(TestLabelPrefix.Length) : "";
        }

        /// <summary>"Levels/Level_123" -> "lvl_1"; "Levels/Variant/Level_123_2" -> "lvl_1"; "Levels/TEST/VersionA/Level_5" -> "lvl_test_VersionA". null khi address không theo quy ước.</summary>
        public static string GetChunkLabelForAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;

            // Phải xét trước nhánh cắt số sau dấu '_' cuối: "Levels/Variant/Level_12_3" là chunk của level 12, không phải level 3.
            if (IsVariantAddress(address))
            {
                return TryParseVariantAddress(address, out int variantLevel, out _) ? GetChunkLabel(variantLevel) : null;
            }
            if (IsTestAddress(address))
            {
                string relative = address.Substring(TestAddressPrefix.Length);
                int slash = relative.LastIndexOf('/');
                return slash > 0 ? GetTestLabel(relative.Substring(0, slash)) : null;
            }

            int underscore = address.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(address.Substring(underscore + 1), out int level)
                ? GetChunkLabel(level)
                : null;
        }

        /// <summary>Thứ tự tải nền: chunk hiện tại, các chunk phía trước (gần trước), các chunk phía sau, label lạ, bộ test cuối cùng.</summary>
        public static int SyncOrder(string label, int currentChunk)
        {
            if (IsTestLabel(label)) return int.MaxValue;
            if (label == null || !label.StartsWith(LabelPrefix, StringComparison.Ordinal) ||
                !int.TryParse(label.Substring(LabelPrefix.Length), out int chunk))
            {
                return int.MaxValue - 1;
            }

            if (chunk == currentChunk) return 0;
            if (chunk > currentChunk) return chunk - currentChunk;
            return 10000 + (currentChunk - chunk);
        }

        /// <summary>
        /// Version là tên MỘT thư mục con trong TestLevel. Giá trị có dấu phân cách hay ".." (gõ nhầm trên Firebase) coi như
        /// không hợp lệ và trả "", tức là không bật bộ test nào.
        /// </summary>
        public static string SanitizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "";
            string trimmed = version.Trim().Trim('/', '\\').Trim();
            if (trimmed.Length == 0) return "";
            if (trimmed.IndexOf('/') >= 0 || trimmed.IndexOf('\\') >= 0 || trimmed.Contains("..")) return "";
            return trimmed;
        }

        /// <summary>"LevelData_12" -> 12. Tên có ký tự thừa ("LevelData_12 1", do Windows đặt khi copy đè) hoặc số nhỏ hơn 1 là không hợp lệ.</summary>
        public static bool TryParseLevelFileName(string nameWithoutExtension, out int level)
        {
            level = 0;
            if (string.IsNullOrEmpty(nameWithoutExtension)) return false;
            if (!nameWithoutExtension.StartsWith(LevelFilePrefix, StringComparison.Ordinal)) return false;
            string number = nameWithoutExtension.Substring(LevelFilePrefix.Length);
            if (number.Length == 0) return false;
            for (int i = 0; i < number.Length; i++)
            {
                if (number[i] < '0' || number[i] > '9') return false;
            }

            if (!int.TryParse(number, out int parsed) || parsed < 1) return false;
            level = parsed;
            return true;
        }
    }
}
