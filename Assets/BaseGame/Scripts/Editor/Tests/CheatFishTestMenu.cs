using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "CheatFish" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của LevelAddrTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [CheatFishTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class CheatFishTestMenu
{
    [MenuItem("Tools/FishSort/Run Cheat Fish Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("CheatFish", "[CheatFishTests]");
}
