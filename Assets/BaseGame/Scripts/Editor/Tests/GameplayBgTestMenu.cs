using UnityEditor;

/// Chạy các test EditMode có tên class bắt đầu bằng "GameplayBg" (bộ chạy reflection của LevelAddrTestMenu, không mở hộp thoại lưu scene).
/// Kết quả in ra Console với tiền tố [GameplayBgTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class GameplayBgTestMenu
{
    [MenuItem("Tools/FishSort/Run Gameplay Bg Tests")]
    public static void Run() => LevelAddrTestMenu.RunTests("GameplayBg", "[GameplayBgTests]");
}
