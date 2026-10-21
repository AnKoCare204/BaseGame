using System;
using Cysharp.Threading.Tasks;

namespace CoreData
{
    /// <summary>
    /// Chạy một bước của việc vào level và biến mọi exception thành kết quả false. Dữ liệu level tới từ CDN, không qua
    /// vòng kiểm thử của bản app: một lỗi lúc dựng bàn phải thành "nạp thất bại" để nơi gọi đưa người chơi về Home,
    /// chứ không được để màn loading treo mãi.
    /// </summary>
    public static class LevelLoadGuard
    {
        public static async UniTask<bool> RunAsync(Func<UniTask> body, Action<Exception> onError)
        {
            if (body == null) return false;
            try
            {
                await body();
                return true;
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
                return false;
            }
        }
    }
}
