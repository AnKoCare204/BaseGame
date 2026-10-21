namespace Core.Tutorial
{
    /// <summary>Luật gameplay riêng của level đầu (spec FTUE đợt 1 mục 11). Hàm thuần, test ở FtueLevelRulesTests.</summary>
    public static class FtueLevelRules
    {
        /// <summary>L1 tắt ô chờ: cá chưa có bể đậu bị từ chối thay vì bay xuống ô chờ.</summary>
        public static bool HoldingEnabled(int level) => level != 1;

        /// <summary>L1-L2: kẹt vì đầy ô chờ thì được gom vào túi miễn phí thay vì thua.</summary>
        public static bool FreeRescue(int level) => level >= 1 && level <= 2;

        /// <summary>Ô bể khóa "Locked +" chỉ hiện từ L4.</summary>
        public static bool ShowLockedTanks(int level) => level >= 4;

        /// <summary>Số order mở sẵn: L1-L3 mở 3 (spec bản sửa mục 5), từ L4 theo config chung (GamePlayGlobalConfig.DefaultTankSlots).</summary>
        public static int OpenOrderSlots(int level, int defaultSlots) => level >= 1 && level <= 3 ? 3 : defaultSlots;
    }
}
