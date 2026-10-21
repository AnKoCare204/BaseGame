using System;
using Core.GamePlay;
using CoreData;
using Manager;
using Core.UI.Activities;
using Core.UI.Modals;
using Core.UI.Screens;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// Nạp / dọn level (khung AntFood/FishSort): sinh prefab Level từ PrefabGlobalConfig, lấy LevelConfig từ LevelGlobalConfig,
    /// theo dõi số liệu phiên (retry, revive, coin/booster đã tiêu) và tạm dừng bàn.
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        [field: SerializeField] public Level CurrentLevel { get; private set; }
        [field: SerializeField] public bool IsPause { get; private set; }
        public LevelConfig LevelConfig { get; private set; }
        [field: SerializeField] public Transform LevelContainer { get; private set; }
        [field: SerializeField] public Reactive<LevelDifficulty> currentLevelDifficulty = new(LevelDifficulty.Normal);

        private int currentReviveCount;
        private int retryCount = -1;
        private int currentLevelBoosterSpend;
        private int currentLevelCoinSpend;
        private int currentLevelAdsCount;
        private DateTime timeStartLevel;

        public int CurrentReviveCount
        {
            get => currentReviveCount;
            set => currentReviveCount = value;
        }

        public int CurrentRetryCount
        {
            get => retryCount;
            set
            {
                retryCount = value;
                if (InGameDataManager.Instance == null) return;
                // Lưu kèm level đang chơi: bộ đếm của level khác không được mang sang level này.
                int level = GameManager.Instance != null ? GameManager.Instance.Level.Value : 0;
                if (UserData.Instance.LevelRetryCount == value && UserData.Instance.LevelRetryLevel == level) return;
                UserData.Instance.SetLevelRetry(value, level);
            }
        }

        public int CurrentLevelBoosterSpend => currentLevelBoosterSpend;
        public int CurrentLevelCoinSpend => currentLevelCoinSpend;
        public int CurrentLevelAdsCount => currentLevelAdsCount;
        public bool IsTrackingLevelSession => CurrentLevel != null;
        public bool IsLevelCompleted => CurrentLevel != null && CurrentLevel.IsCompleted;

        /// <summary>Số liệu lượt chơi hiện tại cho level_background / level_complete / level_fail.</summary>
        public LevelSessionStats SessionStats { get; } = new LevelSessionStats();

        private void Start()
        {
            IsPause = true;
            InputManager.OnPointerPressed += OnStatsInput;
        }

        private void OnDestroy()
        {
            InputManager.OnPointerPressed -= OnStatsInput;
        }

        private void OnStatsInput()
        {
            SessionStats.Taps++;
            SessionStats.OnInput(Time.realtimeSinceStartup);
        }

        /// <summary>App xuống nền khi đang trong level: gửi level_background một lần mỗi lượt chơi (bắt nhóm thoát giữa level bằng cách tắt app).</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused || CurrentLevel == null || SessionStats.BackgroundSent) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsState(GameState.InGame)) return;
            SessionStats.BackgroundSent = true;
            IngameFirebaseAnalystic.Instance.TrackLevelBackground(SessionStats, Time.realtimeSinceStartup);
        }

        public void ResetLevelSessionStats()
        {
            currentLevelBoosterSpend = 0;
            currentLevelCoinSpend = 0;
            currentLevelAdsCount = 0;
        }

        public void AddLevelBoosterSpend(int value = 1) => currentLevelBoosterSpend += value;
        public void AddLevelCoinSpend(int value) => currentLevelCoinSpend += value;
        public void AddLevelAdsCount(int value = 1) => currentLevelAdsCount += value;
        public void IncreaseReviveCount() => currentReviveCount++;

        public void SetPause(bool value)
        {
            IsPause = value;
        }

        public async UniTask ClearLevel()
        {
            SetPause(true);
            if (CurrentLevel != null)
            {
                CurrentLevel.Clear();
                Destroy(CurrentLevel.gameObject);
            }

            CurrentLevel = null;
            await UniTask.WaitForEndOfFrame();
        }

        /// <summary>
        /// Nạp level hiện tại. false = không dựng được: KHÔNG giữ prefab Level dở dang, nơi gọi tự xử lý (về Home, không trừ mạng).
        /// </summary>
        public async UniTask<bool> LoadLevel()
        {
            timeStartLevel = DateTime.Now;
            ResetLevelSessionStats();
            await ClearLevel();

            Level prefab = PrefabGlobalConfig.Instance.levelPrefab;
            if (prefab == null)
            {
                Debug.LogError("[LevelManager] PrefabGlobalConfig.levelPrefab chưa gán.");
                return false;
            }

            int levelIndex = GameManager.Instance.Level.Value;
            LevelConfig = LevelGlobalConfig.Instance.GetLevelConfig(levelIndex, CurrentRetryCount);
            Debug.Log($"[LevelManager] Level {levelIndex} difficulty={LevelConfig.LevelType} retry={LevelConfig.RetryCount}");

            // Lỗi lúc dựng bàn phải thành "nạp thất bại" để nơi gọi đưa người chơi về Home, không được để màn loading treo.
            bool built = await LevelLoadGuard.RunAsync(() => BuildLevelAsync(prefab), LogBuildError);
            if (!built)
            {
                await DiscardBrokenLevelAsync();
                return false;
            }

            ShowIntroThenResume().Forget();
            return true;
        }

        private async UniTask BuildLevelAsync(Level prefab)
        {
            Transform container = LevelContainer != null ? LevelContainer : transform;
            CurrentLevel = Instantiate(prefab, container);
            CurrentLevel.transform.SetParent(container, false);
            await UniTask.WaitForEndOfFrame();

            currentLevelDifficulty.Value = LevelConfig.LevelType;
            await CurrentLevel.StartGame(LevelConfig);
            GameManager.Instance.SetState(GameState.InGame);
            CheckClaimBooster();
            CurrentReviveCount = 0;
            SessionStats.Reset(Time.realtimeSinceStartup);
            IngameFirebaseAnalystic.Instance.TrackLevelStart();
            UserPropertyManager.SetUserPropertyFirebase?.Invoke("Level", GameManager.Instance.Level.Value.ToString());
        }

        private static void LogBuildError(Exception e)
        {
            Debug.LogError($"[LevelManager] Dựng level lỗi, huỷ vào level: {e}");
        }

        /// <summary>Dọn bàn dựng dở. Clear() của một bàn hỏng cũng có thể lỗi, khi đó huỷ thẳng object.</summary>
        private async UniTask DiscardBrokenLevelAsync()
        {
            if (await LevelLoadGuard.RunAsync(ClearLevel, LogBuildError)) return;
            if (CurrentLevel != null) Destroy(CurrentLevel.gameObject);
            CurrentLevel = null;
            SetPause(true);
        }

        /// <summary>Sau khi màn loading đóng: HUD → popup mở khóa booster (kèm luồng nhận) → cảnh báo độ khó → bỏ pause.</summary>
        private async UniTask ShowIntroThenResume()
        {
            await UniTask.WaitUntil(IsDoneLoadingAndCanShowAfterLoadLevel, cancellationToken: this.GetCancellationTokenOnDestroy());
            await UniTask.WaitForSeconds(0.1f, cancellationToken: this.GetCancellationTokenOnDestroy());
            ScreenInGameContext.Events.LoadLevel?.Invoke();
            await CheckNewUnlockedItem();
            await CheckShowAlertDifficult();
            SetPause(false);
        }

        private bool IsDoneLoadingAndCanShowAfterLoadLevel()
        {
            return !UIManager.Instance.IsHaveActivity(nameof(ActivityLoading)) && LoadingManager.Instance.IsLoadingComplete;
        }

        public async UniTask CheckNewUnlockedItem()
        {
            string boosterKey = ItemGlobalConfig.Instance.GetNewBoosterUnlock(GameManager.Instance.Level.Value);
            if (string.IsNullOrEmpty(boosterKey)) return;
            await RunBoosterUnlockFlowAsync(boosterKey);
        }

        /// <summary>
        /// Popup mở khóa booster ("Booster_&lt;Type&gt;") rồi chờ trọn luồng nhận (đóng popup → booster bay về nút → ép dùng thử).
        /// Cờ nhận booster của Level đặt về false trước; ScreenInGame.ClaimBooster đặt lại true khi xong.
        /// </summary>
        public async UniTask RunBoosterUnlockFlowAsync(string boosterKey)
        {
            if (CurrentLevel != null)
            {
                CurrentLevel.SetIsClaimBooster(false);
                CurrentLevel.SetIsBoosterClaimFlowDone(false);
            }

            ViewOptions viewOptions = new ViewOptions(nameof(ModalNewObject));
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions, boosterKey);

            // PushAsync chỉ chờ popup mở xong; chờ trọn luồng nhận booster rồi mới cho intro kế tiếp hiện.
            await UniTask.WaitUntil(IsBoosterClaimFlowSettled, cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        private bool IsBoosterClaimFlowSettled()
        {
            if (CurrentLevel == null) return true;
            if (!CurrentLevel.GetIsBoosterClaimFlowDone()) return false;
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsUsingBooster) return false;

            ActivityContainer activityContainer = ActivityContainer.Find(ContainerKey.Activities);
            if (activityContainer == null) return true;
            if (activityContainer.IsInTransition) return false;
            return !activityContainer.Activities.ContainsKey(nameof(ActivityFocusButton))
                   && !activityContainer.Activities.ContainsKey(nameof(ActivityTipBooster));
        }

        private void CheckClaimBooster()
        {
            bool hasNewBooster = ItemGlobalConfig.Instance.IsNeedShowBoosterUnlock(GameManager.Instance.Level.Value);
            CurrentLevel.SetIsClaimBooster(!hasNewBooster);
            CurrentLevel.SetIsBoosterClaimFlowDone(!hasNewBooster);
        }

        private async UniTask CheckShowAlertDifficult()
        {
            await UniTask.WaitUntil(() => CurrentLevel == null || CurrentLevel.GetIsClaimBooster(), cancellationToken: this.GetCancellationTokenOnDestroy());
            ScreenInGameContext.Events.CheckShowAlertDifficult?.Invoke();
        }

        /// <summary>Giá Play On lần này theo số lần đã dùng trong level.</summary>
        public int GetReviveCoin()
        {
            return ReviveRemoteConfigData.Instance.GetCurrentReviveCoin(Mathf.Max(0, currentReviveCount - 1));
        }

        public int GetPlayTimeToNow()
        {
            return (int)DateTime.Now.Subtract(timeStartLevel).TotalSeconds;
        }

        public Vector3 GetTutPosition()
        {
            return CurrentLevel != null ? CurrentLevel.GetTutSpawnPoint() : Vector3.zero;
        }
    }
}
