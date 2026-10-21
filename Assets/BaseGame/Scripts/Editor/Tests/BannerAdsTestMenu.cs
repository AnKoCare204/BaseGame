using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "BannerAds" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Dùng bộ chạy của EditModeTestMenu (không qua TestRunnerApi nên không bị hộp thoại "Scene(s) Have Been Modified" chặn).
/// Kết quả in ra Console với tiền tố [BannerAdsTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class BannerAdsTestMenu
{
    [MenuItem("Tools/Game/Run Banner Ads Tests")]
    public static void Run() => EditModeTestMenu.RunTests("BannerAds", "[BannerAdsTests]");
}
