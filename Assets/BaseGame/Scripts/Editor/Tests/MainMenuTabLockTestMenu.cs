using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "MainMenuTabLock" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của LevelAddrTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [MainMenuTabLockTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class MainMenuTabLockTestMenu
{
    [MenuItem("Tools/FishSort/Run Main Menu Tab Lock Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("MainMenuTabLock", "[MainMenuTabLockTests]");
}
