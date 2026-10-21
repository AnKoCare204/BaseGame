using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Tutorial
{
    /// <summary>Trạng thái người chơi lúc vào level, đủ để kịch bản quyết định có chạy không (thuần, test được).</summary>
    public readonly struct TutorialRunContext
    {
        public readonly int Level;
        public readonly IReadOnlyCollection<string> DoneSteps;

        public TutorialRunContext(int level, IReadOnlyCollection<string> doneSteps)
        {
            Level = level;
            DoneSteps = doneSteps;
        }

        public bool IsDone(string id)
        {
            if (DoneSteps == null) return false;
            foreach (string step in DoneSteps)
            {
                if (step == id) return true;
            }

            return false;
        }
    }

    /// <summary>Một kịch bản tutorial gắn với một level (spec FTUE đợt 1 mục 5.2). Chỉ đụng UI / bàn chơi qua TutorialStage.</summary>
    public interface ITutorialScript
    {
        string Id { get; }
        int Level { get; }

        /// <summary>true = khóa bấm ngay lúc vào level (trước khi bỏ pause), kịch bản tự mở dần; false = chạy nền chờ điều kiện.</summary>
        bool BlocksAtStart { get; }

        bool ShouldRun(TutorialRunContext ctx);
        UniTask RunAsync(TutorialStage stage, CancellationToken token);
    }
}
