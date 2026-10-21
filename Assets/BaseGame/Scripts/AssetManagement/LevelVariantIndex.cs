using System.Collections.Generic;

namespace CoreData
{
    /// <summary>
    /// Bảng level gốc -> các số biến thể (tăng dần). Dựng từ key của một catalog Addressables (address biến thể
    /// "Levels/Variant/Level_{n}_{x}") hoặc từ tên file trong thư mục biến thể (Editor đọc file). Hàm thuần, test ở
    /// LevelAddrVariantIndexTests.
    /// </summary>
    public sealed class LevelVariantIndex
    {
        private static readonly List<int> None = new List<int>();
        public static readonly LevelVariantIndex Empty = new LevelVariantIndex(new Dictionary<int, List<int>>());

        private readonly Dictionary<int, List<int>> variantsByLevel;

        private LevelVariantIndex(Dictionary<int, List<int>> variantsByLevel)
        {
            this.variantsByLevel = variantsByLevel;
        }

        public int LevelCount => variantsByLevel.Count;

        /// <summary>Nhận key là address biến thể (string); bỏ qua mọi key khác (level thường, bộ test, label, guid...).</summary>
        public static LevelVariantIndex Build(IEnumerable<object> keys)
        {
            Dictionary<int, List<int>> map = new Dictionary<int, List<int>>();
            if (keys != null)
            {
                foreach (object key in keys)
                {
                    if (key is string address && LevelAddress.TryParseVariantAddress(address, out int level, out int variant))
                    {
                        Add(map, level, variant);
                    }
                }
            }

            return Finish(map);
        }

        /// <summary>Nhận tên file không đuôi dạng LevelData_{n}_{x}; tên khác bỏ qua.</summary>
        public static LevelVariantIndex FromFileNames(IEnumerable<string> namesWithoutExtension)
        {
            Dictionary<int, List<int>> map = new Dictionary<int, List<int>>();
            if (namesWithoutExtension != null)
            {
                foreach (string name in namesWithoutExtension)
                {
                    if (LevelAddress.TryParseVariantFileName(name, out int level, out int variant)) Add(map, level, variant);
                }
            }

            return Finish(map);
        }

        /// <summary>Các x của level, tăng dần; rỗng nếu level không có biến thể. Không sửa list trả về.</summary>
        public IReadOnlyList<int> GetVariants(int level)
        {
            return variantsByLevel.TryGetValue(level, out List<int> variants) ? variants : None;
        }

        private static void Add(Dictionary<int, List<int>> map, int level, int variant)
        {
            if (!map.TryGetValue(level, out List<int> variants))
            {
                variants = new List<int>();
                map[level] = variants;
            }

            if (!variants.Contains(variant)) variants.Add(variant);
        }

        private static LevelVariantIndex Finish(Dictionary<int, List<int>> map)
        {
            foreach (List<int> variants in map.Values) variants.Sort();
            return new LevelVariantIndex(map);
        }
    }
}
