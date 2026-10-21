using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "Iap" (IapConfirmRouting, IapRewardDelivery) từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của EditModeTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [IapTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class IapConfirmRoutingTestMenu
{
    [MenuItem("Tools/Game/Run IAP Tests")]
    public static void Run() => EditModeTestMenu.RunTests("Iap", "[IapTests]");
}
