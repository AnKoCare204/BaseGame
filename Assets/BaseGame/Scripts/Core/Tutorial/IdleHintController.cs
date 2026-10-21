using Core.GamePlay;
using Core.Manager;
using Core.UI.Activities;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>
    /// Gợi ý khi đứng yên (spec FTUE đợt 1 mục 13): đếm thời gian không chạm màn hình trong lúc bàn đang chơi; đủ
    /// IdleHintRemoteConfigData.GetDelay(level) giây thì bàn tay chỉ một con cá khớp bể đã đậu (kèm phóng nhịp). Chạm bất kỳ đâu thì tắt.
    /// Gợi ý xong chỉ đếm lại sau lần chạm kế tiếp. Không chen khi kịch bản tutorial đang làm tối / khóa bấm.
    /// </summary>
    public class IdleHintController : MonoBehaviour
    {
        private readonly IdleHintTimer timer = new IdleHintTimer();
        private bool shown;

        private void OnEnable()
        {
            InputManager.OnPointerPressed += OnInput;
            GameGlobalEvent.OnFishPicked += OnFishPicked;
        }

        private void OnDisable()
        {
            InputManager.OnPointerPressed -= OnInput;
            GameGlobalEvent.OnFishPicked -= OnFishPicked;
            Hide();
        }

        private void OnFishPicked(Fish _) => OnInput();

        private void OnInput()
        {
            Hide();
            timer.OnInput();
        }

        private void Update()
        {
            if (shown)
            {
                if (!CanHint(out _)) Hide();
                return;
            }

            if (timer.WaitingInput) return;
            CanHint(out float delay);
            if (timer.Tick(Time.deltaTime, delay)) TryShow();
        }

        private static bool CanHint(out float delay)
        {
            delay = -1f;
            GameManager game = GameManager.Instance;
            LevelManager levels = LevelManager.Instance;
            if (game == null || levels == null || levels.CurrentLevel == null || levels.IsPause || !game.IsState(GameState.InGame)) return false;
            TutorialManager tutorial = TutorialManager.Instance;
            if (tutorial != null && tutorial.IsStageBusy) return false;
            if (IsUiBlocking()) return false;
            BoardManager board = levels.CurrentLevel.Board;
            if (board == null || board.Model == null || board.IsBusy || board.IsCompleted || board.IsStuck || !board.Model.CanTap) return false;
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsUsingBooster) return false;
            delay = IdleHintRemoteConfigData.Instance.GetDelay(game.Level.Value);
            return delay > 0f;
        }

        /// <summary>Có modal hay activity nào đang mở (trừ khung chữ tutorial không chặn bấm).</summary>
        private static bool IsUiBlocking()
        {
            ModalContainer modals = ModalContainer.Find(ContainerKey.Modals);
            if (modals != null && (modals.IsInTransition || modals.Modals.Count > 0)) return true;
            ActivityContainer activities = ActivityContainer.Find(ContainerKey.Activities);
            if (activities == null) return false;
            if (activities.IsInTransition) return true;
            int allowed = activities.Activities.ContainsKey(nameof(ActivityFirstTut)) ? 1 : 0;
            return activities.Activities.Count > allowed;
        }

        /// <summary>Không có cá khớp bể nào thì thôi, chờ lần chạm kế (timer đã chuyển sang chờ chạm).</summary>
        private void TryShow()
        {
            BoardManager board = LevelManager.Instance.CurrentLevel.Board;
            if (!board.TryShowMatchingHint(out Fish fish)) return;
            WorldPointerHand hand = TutorialManager.Instance != null ? TutorialManager.Instance.Hand : null;
            if (hand != null) hand.PointAt(fish.transform, this);
            shown = true;
            if (LevelManager.Instance != null) LevelManager.Instance.SessionStats.IdleHints++;
        }

        private void Hide()
        {
            if (!shown) return;
            shown = false;
            WorldPointerHand hand = TutorialManager.Instance != null ? TutorialManager.Instance.Hand : null;
            if (hand != null) hand.Hide(this);
            Level level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
            if (level != null && level.Board != null) level.Board.ClearHintPulse();
        }
    }
}
