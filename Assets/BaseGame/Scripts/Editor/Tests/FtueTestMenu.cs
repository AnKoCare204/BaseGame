using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "Ftue" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của LevelAddrTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [FtueTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class FtueTestMenu
{
    [MenuItem("Tools/FishSort/Run Ftue Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("Ftue", "[FtueTests]");
}
