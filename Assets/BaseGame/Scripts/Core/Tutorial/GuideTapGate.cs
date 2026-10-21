namespace Core.Tutorial
{
    /// <summary>
    /// Khung chặn bấm chỉ nhận "Tap to continue" sau MinShowSeconds kể từ lúc hiện: chạm đúp không đóng luôn câu kế tiếp.
    /// Test ở FtueGuideTapGateTests.
    /// </summary>
    public static class GuideTapGate
    {
        public const float MinShowSeconds = 0.4f;

        public static bool Accepts(float shownAt, float now) => now - shownAt >= MinShowSeconds;
    }
}
