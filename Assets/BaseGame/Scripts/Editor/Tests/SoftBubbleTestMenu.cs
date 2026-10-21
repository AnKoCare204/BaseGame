using UnityEditor;

/// Chạy các class test có tên bắt đầu bằng "SoftBubble" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy reflection của LevelAddrTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [SoftBubbleTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class SoftBubbleTestMenu
{
    [MenuItem("Tools/FishSort/Run Soft Bubble Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("SoftBubble", "[SoftBubbleTests]");
}
