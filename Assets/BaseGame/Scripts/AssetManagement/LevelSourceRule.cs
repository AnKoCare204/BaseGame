namespace CoreData
{
    /// <summary>
    /// Level nào chỉ chơi bản bake trong app (Android lẫn iOS: nhóm Levels_Main build theo [BuildTarget] cùng bản build), không lấy bản
    /// CDN hay bộ test. Level 1-2 có tutorial FTUE: data hướng dẫn phải khớp code của chính bản app đang chạy. Level 3 trở đi không
    /// còn tutorial nên theo luật thường. Hàm thuần, test ở LevelAddrApkOnlyTests.
    /// </summary>
    public static class LevelSourceRule
    {
        public const int ApkOnlyMaxLevel = 2;

        public static bool IsApkOnly(int sourceLevel) => sourceLevel >= 1 && sourceLevel <= ApkOnlyMaxLevel;
    }
}
