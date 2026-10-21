using System;
using CoreData;

namespace Core.GamePlay
{
    /// <summary>Độ khó level. Giữ tên của AntFood/FishSort để UI cảnh báo độ khó dùng chung.</summary>
    public enum LevelDifficulty
    {
        Normal = 0,
        Hard = 1,
        SuperHard = 2
    }

    /// <summary>Event bus gameplay ↔ UI. Không có body, chỉ static Action; phát bằng ?.Invoke.</summary>
    public static class GameGlobalEvent
    {
        public static Action OnPurchaseIapSuccess;
        public static Action ForceRebuildCanvas;

        /// <summary>Booster cần chọn mục tiêu trên bàn: bắt đầu pha chọn.</summary>
        public static Action<ResourceType> OnPlayerSelectBoosterTarget;

        public static Action<ResourceType> OnCancelBoosterTarget;
        public static Action<ResourceType> OnPlayerUseBoosterCompleted;

        /// <summary>(tiến độ hiện tại, tổng) mỗi khi tiến độ level đổi; ScreenInGame vẽ thanh tiến độ.</summary>
        public static Action<int, int> OnProgressChanged;
    }
}

namespace Core.GamePlay.GlobalEnum
{
    /// <summary>Lý do kẹt, gửi kèm analytics và chọn câu trong ModalLose. Game mới thêm giá trị ở cuối.</summary>
    public enum LoseReason
    {
        None = -1,
        Stuck = 0,
        OutOfMoves = 1,
        TimeUp = 2,
    }
}

/// <summary>Lý do một booster không dùng được lúc này (Level.GetBoosterBlockReason), UI chọn thông báo theo đó.</summary>
public enum BoosterBlockReason
{
    None = 0,
    NotAvailable = 1,
    /// <summary>Bàn ở trạng thái không cho dùng (vd đầy chỗ chứa).</summary>
    Blocked = 2,
    /// <summary>Bàn đang bận (animation): chờ xong rồi bấm lại, UI không hiện thông báo.</summary>
    Busy = 3
}
