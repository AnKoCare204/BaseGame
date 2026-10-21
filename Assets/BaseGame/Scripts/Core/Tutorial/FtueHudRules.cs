using System.Collections.Generic;

namespace Core.Tutorial
{
    /// <summary>Phần HUD bật / tắt theo level trong FTUE.</summary>
    public enum HudPart
    {
        Progress,
        Holding,
        BoosterBar,
        LockedTanks
    }

    /// <summary>Phần HUD nào hiện ở level nào (spec FTUE đợt 1 mục 10). Hàm thuần, test ở FtueHudRulesTests.</summary>
    public static class FtueHudRules
    {
        public static bool IsVisible(HudPart part, int level, IReadOnlyCollection<string> doneSteps)
        {
            switch (part)
            {
                case HudPart.Progress:
                    return level != 1 || Contains(doneSteps, TutorialIds.L1Match);
                case HudPart.Holding:
                    if (level == 1) return false;
                    return level != 2 || Contains(doneSteps, TutorialIds.L2Holding);
                case HudPart.BoosterBar:
                    return level >= 3;   // Add Slot mở lúc vào L3 như mọi booster khác
                case HudPart.LockedTanks:
                    return FtueLevelRules.ShowLockedTanks(level);
                default:
                    return true;
            }
        }

        private static bool Contains(IReadOnlyCollection<string> steps, string id)
        {
            if (steps == null) return false;
            foreach (string step in steps)
            {
                if (step == id) return true;
            }

            return false;
        }
    }
}
