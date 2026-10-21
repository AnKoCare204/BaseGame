namespace Core.Tutorial
{
    /// <summary>
    /// Đếm thời gian đứng yên cho IdleHintController (thuần, test ở FtueIdleHintTimerTests). Đủ delay thì báo gợi ý đúng một lần rồi chờ
    /// lần chạm kế; lúc không gợi ý được thì đếm lại từ đầu.
    /// </summary>
    public sealed class IdleHintTimer
    {
        private float idle;

        /// <summary>Đã báo gợi ý, chờ lần chạm kế mới đếm lại.</summary>
        public bool WaitingInput { get; private set; }

        /// <summary>delay ≤ 0 = lúc này không gợi ý được (pause, modal, kịch bản đang khóa...). true = tới lúc gợi ý.</summary>
        public bool Tick(float deltaTime, float delay)
        {
            if (WaitingInput) return false;
            if (delay <= 0f)
            {
                idle = 0f;
                return false;
            }

            idle += deltaTime;
            if (idle < delay) return false;
            WaitingInput = true;
            return true;
        }

        public void OnInput()
        {
            idle = 0f;
            WaitingInput = false;
        }
    }
}
