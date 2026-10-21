using System;
using CoreData;

namespace Core.GamePlay
{
    /// <summary>Độ khó level. Giữ tên của AntFood/FishHunt để UI dùng chung; Normal = Regular trong GDD.</summary>
    public enum LevelDifficulty
    {
        Normal = 0,
        Hard = 1,
        SuperHard = 2
    }

    /// <summary>Event bus gameplay ↔ UI (thay GameGlobalEvent của AntFood). Không có body, chỉ static Action.</summary>
    public static class GameGlobalEvent
    {
        public static Action OnPurchaseIapSuccess;
        public static Action ForceRebuildCanvas;

        /// <summary>Booster cần chọn bể (Magnet, Swap): bắt đầu pha chọn mục tiêu.</summary>
        public static Action<ResourceType> OnPlayerSelectBoosterTarget;

        public static Action<ResourceType> OnCancelBoosterTarget;
        public static Action<ResourceType> OnPlayerUseBoosterCompleted;

        /// <summary>(số cá đã thu, tổng cá) mỗi khi tiến độ level đổi.</summary>
        public static Action<int, int> OnProgressChanged;

        /// <summary>Người chơi bấm một con cá và nước đi đã bắt đầu (cá rời bóng, đang bay). Không gồm tự thu / booster.</summary>
        public static Action<Fish> OnFishPicked;

        /// <summary>Bàn đã dừng hẳn (không cá bay, không bóng nổ, không booster) sau một nước đi.</summary>
        public static Action OnBoardSettled;

        /// <summary>(ô bể, loài) bể vừa đủ cá và chốt, trước khi bay đi. Dùng cho tutorial.</summary>
        public static Action<int, int> OnTankCompleted;

        /// <summary>(ô bể, loài) bể vừa đậu xong ở ô và nhận cá được; loài -1 = hết đơn (bể bay đi).</summary>
        public static Action<int, int> OnTankArrived;

        /// <summary>(số ô chờ trống, tổng số ô) mỗi lần ô chờ đổi, và một lần sau khi dựng bàn.</summary>
        public static Action<int, int> OnHoldingChanged;

        /// <summary>(cá, chỉ số ô) cá vừa đáp xuống ô chờ.</summary>
        public static Action<Fish, int> OnFishLandedInHolding;

        /// <summary>Người chơi nhả tay trên cá đang viền đỏ (bị từ chối: L1 chưa có bể, đàn thiếu chỗ).</summary>
        public static Action<Fish> OnFishTapDenied;

        /// <summary>L1-L2: bàn kẹt vì đầy ô chờ, ô chờ vừa được gom vào túi miễn phí thay vì thua.</summary>
        public static Action OnFreeRescue;
    }
}

namespace Core.GamePlay.GlobalEnum
{
    public enum LoseReason
    {
        None = -1,
        HoldingFull = 0
    }

    /// <summary>Loại vật cản để popup giới thiệu (NewFeatureGlobalConfig). Khớp ObstacleType của GDD.</summary>
    public enum ObstacleFeatureType
    {
        None = 0,
        HiddenFish = 1,
        Ice = 2,
        Spawner = 3,
        LockKey = 4,
        BubbleHp = 5,
        Rock = 6,
        MultiLayer = 7,
        FishSchool = 8,
        BubbleOrder = 9,
    }
}
