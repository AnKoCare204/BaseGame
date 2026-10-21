using Core.GamePlay;
using Core.UI.Activities;
using Core.UI.Modals;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Booster trong bàn (Docs/GDD/08_Boosters.tsv), mô hình AntFood: tức thời (AddSlot = thêm ô chờ, Shuffle, AutoFill, PackedUp = túi
    /// gom ô chờ, UnlockTank) trừ ngay; hai pha chọn bể (Swap) giữ IsUsingBooster tới khi InputManager báo bể được chọn hoặc hủy.
    /// Hint không còn là booster.
    /// Logic thật nằm trong Level (BoardManager); ở đây chỉ điều phối và trừ resource.
    /// </summary>
    public class BoosterManager : Singleton<BoosterManager>
    {
        [field: SerializeField] public bool IsUsingBooster { get; private set; }
        [field: SerializeField] public ResourceType CurrentBoosterType { get; private set; } = ResourceType.None;

        public bool IsSelectingTarget => IsUsingBooster && NeedsTarget(CurrentBoosterType);

        private static Level CurrentLevel => LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;

        public static bool NeedsTarget(ResourceType type) => type == ResourceType.Swap;

        [Button]
        public void UseBooster(ResourceType boosterType)
        {
            if (IsUsingBooster || !boosterType.IsBooster()) return;
            Level level = CurrentLevel;
            if (level == null) return;

            IsUsingBooster = true;
            CurrentBoosterType = boosterType;
            switch (boosterType)
            {
                case ResourceType.AddSlot:
                    Finish(level.TryAddHoldingSlot());
                    break;
                case ResourceType.Shuffle:
                    Finish(level.TryUseShuffle(), nameof(ActivityBoosterShuffle));
                    break;
                case ResourceType.AutoFill:
                    UseAutoFillAsync(level).Forget();
                    break;
                case ResourceType.PackedUp:
                    Finish(level.TryUsePackedUp());   // túi tự là hiệu ứng, không overlay
                    break;
                case ResourceType.UnlockTank:
                    Finish(level.TryUnlockTank());
                    break;
                case ResourceType.Swap:
                    ScreenInGameContext.Events.HideBotPanel?.Invoke();
                    level.BeginSelectTank(boosterType);
                    GameGlobalEvent.OnPlayerSelectBoosterTarget?.Invoke(boosterType);
                    break;
                default:
                    Finish(false);
                    break;
            }
        }

        /// <summary>
        /// Người chơi bấm ô bể đang khóa (dấu + trên TankSlot, InputManager gọi). Mở bể không dùng kho booster: mở ModalUnlockTank
        /// để trả bằng ads hoặc gold rồi mở ngay một ô. Bàn chưa cho mở (đang bận, hết đơn) thì báo như nút booster HUD.
        /// </summary>
        public void OnLockedTankTapped(TankSlotView tank)
        {
            if (tank == null || !tank.IsLocked || IsUsingBooster || CurrentLevel == null) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsState(GameState.InGame)) return;
            if (!CanUseBooster(ResourceType.UnlockTank))
            {
                NotifyBoosterBlocked(ResourceType.UnlockTank);
                return;
            }

            ModalContainer modals = ModalContainer.Find(ContainerKey.Modals);
            if (modals == null || modals.IsInTransition || modals.Modals.Count > 0) return;   // tap kép lúc modal đang mở: bỏ qua
            ViewOptions options = new ViewOptions(nameof(ModalUnlockTank));
            modals.PushAsync(options).Forget();
        }

        /// <summary>
        /// Mở ô bể khóa kế tiếp sau khi người chơi trả bằng gold / ads (ModalUnlockTank), không trừ kho booster. Chờ bàn hết bận
        /// (cá đang bay, tối đa UnlockTankWaitSeconds) rồi mở; trả về false nếu vẫn không mở được (hết đơn) để modal không trừ gold.
        /// </summary>
        public async UniTask<bool> UnlockTankPaidAsync()
        {
            Level level = CurrentLevel;
            if (level == null) return false;
            float deadline = Time.time + UnlockTankWaitSeconds;
            while (level != null && level.Board != null && level.Board.IsBusy && Time.time < deadline) await UniTask.Yield();
            if (level == null || IsUsingBooster || !level.TryUnlockTank()) return false;
            PlayBoosterSfx(ResourceType.UnlockTank);
            return true;
        }

        private const float UnlockTankWaitSeconds = 5f;

        /// <summary>
        /// Dùng booster nếu bàn cho phép, không thì báo lý do (NotifyBoosterBlocked). Trả về true nếu booster đã được kích hoạt.
        /// </summary>
        public bool UseBoosterOrNotify(ResourceType type)
        {
            if (!CanUseBooster(type))
            {
                NotifyBoosterBlocked(type);
                return false;
            }

            UseBooster(type);
            ScreenInGameContext.Events.HideBoosterHint?.Invoke();
            return true;
        }

        /// <summary>
        /// Báo booster không dùng được (nút HUD và ô bể khóa dùng chung): bàn đang bận chỉ rung + tiếng từ chối, còn lại hiện
        /// MessageHoldingFull / MessageNoUse.
        /// </summary>
        public void NotifyBoosterBlocked(ResourceType type)
        {
            BoosterBlockReason reason = GetBoosterBlockReason(type);
            if (reason != BoosterBlockReason.Busy)
            {
                BoosterInfoData data = ItemGlobalConfig.Instance.GetBoosterData(type);
                string message = reason == BoosterBlockReason.HoldingFull && data != null && !string.IsNullOrEmpty(data.MessageHoldingFull)
                    ? data.MessageHoldingFull
                    : data?.MessageNoUse;
                if (!string.IsNullOrEmpty(message)) ScreenInGameContext.Events.ShowNotification?.Invoke(message);
            }

            if (VibrationManager.Instance != null) VibrationManager.Instance.CallHaptic(HapticType.MediumImpact);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxTapDenied);   // booster bị chặn dùng chung tiếng "fail nhẹ" với bấm cá bị từ chối
        }

        /// <summary>InputManager gọi khi người chơi bấm một bể trong lúc chọn mục tiêu.</summary>
        public void OnTankPicked(TankSlotView tank)
        {
            if (!IsSelectingTarget || tank == null) return;
            Level level = CurrentLevel;
            if (level == null) return;
            ResourceType type = CurrentBoosterType;
            if (!level.TryUseTankBooster(type, tank)) return;
            ScreenInGameContext.Events.ShowBotPanel?.Invoke();
            Finish(true);
            GameGlobalEvent.OnPlayerUseBoosterCompleted?.Invoke(type);
        }

        [Button]
        public void CancelBooster(ResourceType boosterType)
        {
            if (!IsUsingBooster || CurrentBoosterType != boosterType) return;
            CurrentLevel?.CancelSelectTank();
            IsUsingBooster = false;
            CurrentBoosterType = ResourceType.None;
            ScreenInGameContext.Events.ShowBotPanel?.Invoke();
            GameGlobalEvent.OnCancelBoosterTarget?.Invoke(boosterType);
        }

        public bool CanUseBooster(ResourceType boosterType)
        {
            Level level = CurrentLevel;
            return level != null && level.CanUseBooster(boosterType);
        }

        /// <summary>Lý do không dùng được để UI chọn thông báo (Shuffle bị chặn khi ô chờ đầy).</summary>
        public BoosterBlockReason GetBoosterBlockReason(ResourceType boosterType)
        {
            Level level = CurrentLevel;
            return level != null ? level.GetBoosterBlockReason(boosterType) : BoosterBlockReason.NotAvailable;
        }

        /// <summary>
        /// AutoFill: overlay cần câu chạy trước (GamePlayGlobalConfig.AutoFillOverlayLeadTime giây) rồi cá mới bay vào bể, khác Shuffle
        /// (overlay chạy song song). Giữ IsUsingBooster suốt lúc chờ nên InputManager khoá bấm cá và không dùng được booster khác; hết
        /// chờ mới lấy cá và trừ kho. Level đổi / kết thúc trong lúc chờ thì bỏ, không trừ.
        /// </summary>
        private async UniTaskVoid UseAutoFillAsync(Level level)
        {
            ShowBoosterActivity(nameof(ActivityBoosterFishingRod));
            float lead = GamePlayGlobalConfig.Instance != null ? GamePlayGlobalConfig.Instance.AutoFillOverlayLeadTime : 0f;
            if (lead > 0f)
            {
                try
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(lead), cancellationToken: this.GetCancellationTokenOnDestroy());
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
            }

            bool used = level != null && level == CurrentLevel && level.TryUseAutoFill();
            Finish(used);
        }

        /// <summary>
        /// Kết thúc lượt dùng booster tức thời: dùng được thì trừ kho, phát tiếng và bật overlay Spine (activityName, null = không có);
        /// không dùng được thì chỉ bỏ cờ.
        /// </summary>
        private void Finish(bool consumed, string activityName = null)
        {
            ResourceType type = CurrentBoosterType;
            if (consumed)
            {
                PlayerResourceManager.Instance.SubResourceValue(type, 1, ResourcePos.ingame.ToString(), type.ToSpendPos().ToString());
                PlayBoosterSfx(type);
                ShowBoosterActivity(activityName);
            }

            IsUsingBooster = false;
            CurrentBoosterType = ResourceType.None;
        }

        /// <summary>Bật overlay Spine của booster (ActivityBoosterShuffle / FishingRod / Packup, tự ẩn sau vài giây). Đang hiện rồi thì bỏ qua.</summary>
        private static void ShowBoosterActivity(string activityName)
        {
            if (string.IsNullOrEmpty(activityName)) return;
            ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
            if (container == null || container.Activities.ContainsKey(activityName)) return;
            container.Show(new ViewOptions(activityName));
        }

        /// <summary>
        /// Tiếng kích hoạt theo loại booster, phát khi booster thật sự được dùng (Swap: lúc đã chọn bể). PackedUp không nằm ở đây:
        /// BoardManager.PackHoldingIntoBag phát để Play On (hồi sinh bằng túi) dùng chung.
        /// </summary>
        private static void PlayBoosterSfx(ResourceType type)
        {
            if (AudioManager.Instance == null) return;
            switch (type)
            {
                case ResourceType.AddSlot: AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterAddSlot); break;
                case ResourceType.Shuffle: AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterShuffle); break;
                case ResourceType.AutoFill: AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterAutoFill); break;
                case ResourceType.UnlockTank: AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterUnlockTank); break;
                case ResourceType.Swap: AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterSwap); break;
            }
        }
    }
}
