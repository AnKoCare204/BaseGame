using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "ModalShopListener" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của EditModeTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [ModalShopListenerTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class ModalShopListenerTestMenu
{
    [MenuItem("Tools/Game/Run Modal Shop Listener Tests")]
    public static void Run() => EditModeTestMenu.RunTests("ModalShopListener", "[ModalShopListenerTests]");
}
