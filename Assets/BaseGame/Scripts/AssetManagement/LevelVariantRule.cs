using System.Collections.Generic;

namespace CoreData
{
    /// <summary>
    /// Biến thể LevelData_{n}_{x}: từ lần chơi lại thứ x của level n, chơi biến thể x thay bản gốc. Nhiều biến thể thì bậc thang
    /// (x lớn nhất ≤ số lần chơi lại). Chỉ áp cho level đang chơi đúng nội dung của nó (không loop), không áp cho level 1-2 (FTUE,
    /// LevelSourceRule) và lượt đầu. Bộ test đang bật có level n thì bộ test thắng (LevelAddressableProvider xét). Hàm thuần, test ở
    /// LevelAddrVariantRuleTests.
    /// </summary>
    public static class LevelVariantRule
    {
        public static bool IsEligible(LevelRequest request)
        {
            return request.IsValid && request.Level == request.SourceLevel &&
                   !LevelSourceRule.IsApkOnly(request.SourceLevel) && request.RetryCount >= 1;
        }

        /// <summary>
        /// Các x ≤ retryCount, giảm dần. Phần tử đầu là bậc thang (x lớn nhất ≤ r); các phần tử sau để lùi bậc khi bậc cao chưa có
        /// trên máy hoặc không đọc được.
        /// </summary>
        public static List<int> Candidates(int retryCount, IReadOnlyList<int> variants)
        {
            List<int> result = new List<int>();
            if (variants == null) return result;
            for (int i = 0; i < variants.Count; i++)
            {
                int variant = variants[i];
                if (variant >= 1 && variant <= retryCount && !result.Contains(variant)) result.Add(variant);
            }

            result.Sort((a, b) => b.CompareTo(a));
            return result;
        }
    }
}
