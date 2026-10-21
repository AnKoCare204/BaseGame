using UnityEditor;

/// Chạy các class test có tên bắt đầu bằng "FishRig" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy reflection của LevelAddrTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [FishRigTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class FishRigTestMenu
{
    [MenuItem("Tools/FishSort/Run Fish Rig Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("FishRig", "[FishRigTests]");
}
