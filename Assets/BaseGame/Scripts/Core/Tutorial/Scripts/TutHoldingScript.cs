using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Tutorial
{
    /// <summary>
    /// L2 (spec mục 8, bản sửa mục 2.2): hàng ô chờ hiện ra; ép bấm một cá chưa có order (X) → cá đáp ô chờ ("It waits here") →
    /// "Complete an order to get a new one" rồi tự chơi tới khi order X tới → khóa bấm, X tự bay vào ("Its order is here!") → luật thua.
    /// Mọi câu đều làm tối và làm sáng phần cần nhìn. Không có X (data cũ): chờ lần đầu cá đáp ô chờ rồi chỉ giải thích ô chờ và luật thua.
    /// </summary>
    public sealed class TutHoldingScript : ITutorialScript
    {
        /// <summary>Data cũ có thể không đưa order X tới sớm: sau bấy nhiêu order loài khác đậu thì bỏ bước "cá tự về".</summary>
        private const int MaxOtherArrivals = 3;

        public string Id => TutorialIds.L2Holding;
        public int Level => 2;
        public bool BlocksAtStart => true;

        public bool ShouldRun(TutorialRunContext ctx) => ctx.Level == Level && !ctx.IsDone(Id);

        public async UniTask RunAsync(TutorialStage stage, CancellationToken token)
        {
            TutorialTexts texts = stage.Texts;
            BoardManager board = stage.Board;
            if (board == null) return;
            await UniTask.WaitUntil(() => board == null || board.IsFirstOpenTankReady(), cancellationToken: token);

            Fish x = stage.FindHoldingDemoFish();
            if (x == null)
            {
                UniTask<Fish> firstLanding = stage.WaitFishLandedInHolding(token);   // đăng ký trước khi mở bấm
                stage.SetHudPart(HudPart.Holding, true, true);
                stage.AllowAll();
                await firstLanding;
                await ExplainWaitAsync(stage, texts, token);
                await ExplainFullRuleAsync(stage, texts, token);
                stage.AllowAll();
                stage.Complete();
                return;
            }

            int xSpecies = x.FishId;

            // Bước 1: ô chờ hiện, ép bấm X. Đăng ký chờ trước khi mở bấm (xem TutorialStage, mục chờ sự kiện).
            UniTask<Fish> pickedX = stage.WaitFishPicked(f => f == x, token);
            UniTask<Fish> landed = stage.WaitFishLandedInHolding(token);
            stage.SetHudPart(HudPart.Holding, true, true);
            stage.DimWorld(true);
            stage.Highlight(x);
            stage.HighlightHolding();
            stage.AllowOnly(new[] { x });
            stage.ShowHand(x.transform);
            await stage.ShowGuide(texts.L2NoOrder, false);
            await pickedX;
            stage.HideHand();
            stage.HideGuide();
            stage.BlockAll();
            stage.Step("holding_reveal");

            // Bước 2: cá chờ ở đây.
            await landed;
            stage.ClearHighlights();
            await ExplainWaitAsync(stage, texts, token);

            // Bước 3: lấp order để có order mới (chạm để tiếp tục), rồi thả tự chơi tới khi order X tới. Đăng ký chờ trước khi mở bấm.
            UniTask<bool> xOrderArrival = stage.WaitTankArrived(xSpecies, MaxOtherArrivals, token);
            await stage.ExplainAsync(texts.L2FillOrder, stage.HighlightOrders, token);
            stage.AllowAll();
            stage.Step("fill_tank");
            bool arrived = await xOrderArrival;

            // Bước 4: order của X tới, X tự bay từ ô chờ vào: khóa bấm, làm sáng order X + X, chữ khi X đã vào.
            if (arrived)
            {
                await UniTask.WaitUntil(() => !stage.IsBusy, cancellationToken: token);   // câu cứu miễn phí đang hiện thì chờ xong
                int freeBefore = stage.Model != null ? stage.Model.HoldingFreeCount : 0;
                UniTask<int> flewIn = UniTask.WhenAny(
                    stage.WaitHolding((free, slots) => free > freeBefore, token),
                    UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: token));
                stage.BlockAll();
                stage.DimWorld(true);
                TankSlotView xOrder = stage.FindTankSlot(xSpecies);
                if (xOrder != null) stage.Highlight(xOrder);
                stage.Highlight(x);
                await flewIn;
                await stage.ExplainAsync(texts.L2FlewIn, null, token);
                stage.AllowAll();
                stage.Step("auto_collect");
            }

            // Bước 5: luật thua.
            await ExplainFullRuleAsync(stage, texts, token);
            stage.AllowAll();
            stage.Complete();
        }

        private static async UniTask ExplainWaitAsync(TutorialStage stage, TutorialTexts texts, CancellationToken token)
        {
            await stage.ExplainAsync(texts.L2Waits, stage.HighlightHolding, token);
            stage.Step("holding_wait");
        }

        private static async UniTask ExplainFullRuleAsync(TutorialStage stage, TutorialTexts texts, CancellationToken token)
        {
            int slots = stage.Model != null ? stage.Model.HoldingSlots : 5;
            await stage.ExplainAsync(string.Format(texts.L2FullRule, slots), stage.HighlightHolding, token);
            stage.Step("full_rule");
        }
    }
}
