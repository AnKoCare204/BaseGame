using System;
using Cysharp.Threading.Tasks;

namespace Core.Tutorial
{
    /// <summary>Lưới an toàn cho bước tutorial chạy ngoài kịch bản (như RunScriptAsync cho kịch bản). Test ở FtueTutorialGuardTests.</summary>
    public static class TutorialGuard
    {
        /// <summary>Bị huỷ (level kết thúc, EndLevel đã dọn): im lặng. Lỗi khác: log rồi onFail để người chơi không kẹt trong lớp tối / khóa bấm.</summary>
        public static async UniTask RunAsync(Func<UniTask> body, Action onFail, Action<Exception> log)
        {
            try
            {
                await body();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                log?.Invoke(e);
                onFail?.Invoke();
            }
        }
    }
}
