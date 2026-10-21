using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>
    /// Làm tối HUD khi tutorial làm tối thế giới (spec FTUE bản sửa mục 2.1). HUD là ScreenSpaceOverlay vẽ sau mọi camera nên lớp tối của
    /// thế giới không phủ được, mà lớp tối Overlay lại che luôn vật đang làm sáng: hạ alpha CanvasGroup gốc của HUD (HUD mờ trên nền đã
    /// tối), phần HUD đang làm sáng có CanvasGroup riêng bỏ qua nhóm cha. HUD tối cũng không nhận bấm (nút Settings, Speed...): chạm rơi
    /// xuống lớp tối của tutorial. Dùng blocksRaycasts chứ không dùng interactable, vì interactable là khoá HUD lúc thắng của ScreenInGame.
    /// Test ở FtueHudDimTests.
    /// </summary>
    public static class HudDim
    {
        public static void SetDim(CanvasGroup main, bool on, float dimAlpha)
        {
            if (main == null) return;
            main.alpha = on ? dimAlpha : 1f;
            main.blocksRaycasts = !on;
        }

        public static void SetLit(GameObject part, bool lit)
        {
            if (part == null) return;
            var group = part.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (!lit) return;
                group = part.AddComponent<CanvasGroup>();
            }

            group.ignoreParentGroups = lit;
        }
    }
}
