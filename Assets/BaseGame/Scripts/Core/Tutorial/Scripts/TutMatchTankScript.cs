using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Tutorial
{
    /// <summary>
    /// L1 (spec mục 7, bản sửa mục 2.2): làm tối, làm sáng order đầu + 3 cá cùng loài, bàn tay chỉ từng con, chỉ 3 con này bấm được.
    /// Order đủ thì bộ đếm hiện, làm sáng bộ đếm + hàng order với câu mục tiêu (chạm để tiếp tục, tối đa 3 s), ghi xong rồi thả cho tự chơi.
    /// </summary>
    public sealed class TutMatchTankScript : ITutorialScript
    {
        public string Id => TutorialIds.L1Match;
        public int Level => 1;
        public bool BlocksAtStart => true;

        public bool ShouldRun(TutorialRunContext ctx) => ctx.Level == Level && !ctx.IsDone(Id);

        public async UniTask RunAsync(TutorialStage stage, CancellationToken token)
        {
            TutorialTexts texts = stage.Texts;
            BoardManager board = stage.Board;
            if (board == null) return;
            await UniTask.WaitUntil(() => board == null || board.IsFirstOpenTankReady(), cancellationToken: token);

            var fishes = new List<Fish>();
            int goal = GamePlayGlobalConfig.TankGoal;
            if (board != null && board.TryGetFirstOpenTank(out int tankIndex, out int fishId) &&
                board.TryCollectTappableFish(fishId, goal, fishes) && fishes.Count == goal)
            {
                // Bước 1: ép bấm 3 con của order đầu. Đăng ký chờ trước khi mở bấm (xem TutorialStage, mục chờ sự kiện).
                var remaining = new List<Fish>(fishes);
                UniTask allPicked = stage.WaitPickedAll(remaining, (picked, left) =>
                {
                    stage.PlayStepSound(goal - left - 1);
                    if (left > 0) stage.ShowHand(remaining[0].transform);
                    else stage.HideHand();
                }, token);
                UniTask<int> tankDone = stage.WaitTankCompleted(token);

                stage.DimWorld(true);
                stage.Highlight(stage.Level.TankAt(tankIndex));
                foreach (Fish fish in fishes) stage.Highlight(fish);
                stage.AllowOnly(fishes);
                stage.ShowHand(fishes[0].transform);
                await stage.ShowGuide(texts.L1Match, false);
                await allPicked;

                stage.HideGuide();
                stage.BlockAll();
                await tankDone;
                stage.Step("match_forced");
            }

            // Bước 2: bộ đếm hiện; làm sáng bộ đếm + hàng order, câu mục tiêu (chạm để tiếp tục).
            stage.ClearHighlights();
            stage.SetHudPart(HudPart.Progress, true, true);
            await UniTask.Delay(400, cancellationToken: token);   // chờ bộ đếm bung xong mới đặt khung
            // Order mới thế chỗ order vừa xong còn đang rơi xuống (bàn bận tới khi đậu): làm sáng trước lúc đó thì order mới bị tối.
            await UniTask.WaitUntil(() => board == null || !board.IsBusy, cancellationToken: token);
            await stage.ExplainAsync(texts.L1Goal, () =>
            {
                stage.HighlightHud(HudPart.Progress);
                stage.HighlightOrders();
            }, token);
            stage.Step("goal");
            stage.Complete();

            // Bước 3: tự chơi; idle hint và gợi ý khi bấm sai (TutorialManager) lo phần còn lại.
            stage.AllowAll();
        }
    }
}
