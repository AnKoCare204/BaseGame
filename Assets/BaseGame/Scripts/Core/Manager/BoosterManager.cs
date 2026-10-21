using Core.GamePlay;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Booster trong bàn (mô hình AntFood/FishSort): nút booster ở ScreenInGame gọi UseBoosterOrNotify; hiệu ứng thật nằm trong
    /// Level.UseBooster (lớp gameplay của game override). Ở đây chỉ điều phối, khoá bấm trong lúc chạy và trừ tài nguyên khi đã dùng.
    /// </summary>
    public class BoosterManager : Singleton<BoosterManager>
    {
        [field: SerializeField] public bool IsUsingBooster { get; private set; }
        [field: SerializeField] public ResourceType CurrentBoosterType { get; private set; } = ResourceType.None;

        private static Level CurrentLevel => LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;

        [Button]
        public void UseBooster(ResourceType boosterType)
        {
            if (IsUsingBooster || !boosterType.IsBooster()) return;
            Level level = CurrentLevel;
            if (level == null) return;

            IsUsingBooster = true;
            CurrentBoosterType = boosterType;
            UseBoosterAsync(level, boosterType).Forget();
        }

        private async UniTaskVoid UseBoosterAsync(Level level, ResourceType boosterType)
        {
            bool used = false;
            try
            {
                used = await level.UseBooster(boosterType);
            }
            finally
            {
                // Level đổi / bị huỷ trong lúc booster chạy thì không trừ.
                Finish(used && level == CurrentLevel);
            }

            if (used) GameGlobalEvent.OnPlayerUseBoosterCompleted?.Invoke(boosterType);
        }

        /// <summary>Dùng booster nếu bàn cho phép, không thì báo lý do (NotifyBoosterBlocked). Trả về true nếu booster đã được kích hoạt.</summary>
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

        /// <summary>Báo booster không dùng được: bàn đang bận chỉ rung + tiếng từ chối, còn lại hiện MessageNoUse của booster.</summary>
        public void NotifyBoosterBlocked(ResourceType type)
        {
            BoosterBlockReason reason = GetBoosterBlockReason(type);
            if (reason != BoosterBlockReason.Busy)
            {
                BoosterInfoData data = ItemGlobalConfig.Instance.GetBoosterData(type);
                string message = data?.MessageNoUse;
                if (!string.IsNullOrEmpty(message)) ScreenInGameContext.Events.ShowNotification?.Invoke(message);
            }

            if (VibrationManager.Instance != null) VibrationManager.Instance.CallHaptic(HapticType.MediumImpact);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxTapDenied);
        }

        public bool CanUseBooster(ResourceType boosterType)
        {
            Level level = CurrentLevel;
            return level != null && level.CanUseBooster(boosterType);
        }

        public BoosterBlockReason GetBoosterBlockReason(ResourceType boosterType)
        {
            Level level = CurrentLevel;
            return level != null ? level.GetBoosterBlockReason(boosterType) : BoosterBlockReason.NotAvailable;
        }

        /// <summary>Kết thúc lượt dùng booster: dùng được thì trừ kho và phát tiếng; không thì chỉ bỏ cờ.</summary>
        private void Finish(bool consumed)
        {
            ResourceType type = CurrentBoosterType;
            if (consumed)
            {
                PlayerResourceManager.Instance.SubResourceValue(type, 1, ResourcePos.ingame.ToString(), type.ToSpendPos().ToString());
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterUse);
            }

            IsUsingBooster = false;
            CurrentBoosterType = ResourceType.None;
        }
    }
}
