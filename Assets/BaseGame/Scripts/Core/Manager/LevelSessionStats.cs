using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Số liệu của một lượt chơi level (spec FTUE đợt 1 mục 14): gửi kèm level_complete / level_fail và trong level_background (app xuống
    /// nền giữa level). Thời gian theo Time.realtimeSinceStartup do bên gọi truyền vào (test được).
    /// </summary>
    public class LevelSessionStats
    {
        public int Taps;
        public int DeniedTaps;
        public int HoldingMax;
        public int HoldingUsed;
        public int IdleHints;
        public bool BackgroundSent;
        private float startTime;
        private float lastInputTime;

        public void Reset(float now)
        {
            Taps = 0;
            DeniedTaps = 0;
            HoldingMax = 0;
            HoldingUsed = 0;
            IdleHints = 0;
            BackgroundSent = false;
            startTime = now;
            lastInputTime = now;
        }

        public void OnInput(float now) => lastInputTime = now;

        public void OnHolding(int free, int slots)
        {
            HoldingUsed = Mathf.Max(0, slots - free);
            HoldingMax = Mathf.Max(HoldingMax, HoldingUsed);
        }

        public int DurationSec(float now) => Mathf.Max(0, Mathf.FloorToInt(now - startTime));
        public int IdleSec(float now) => Mathf.Max(0, Mathf.FloorToInt(now - lastInputTime));
    }
}
