using System;
using System.Collections.Generic;
using Core.GamePlay;
using CoreData;
using Core.GamePlay.GlobalEnum;
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
    /// Nạp / dọn level (khung AntFood): sinh prefab Level từ PrefabGlobalConfig, lấy LevelData từ LevelGlobalConfig,
    /// theo dõi số liệu phiên (retry, revive, coin/booster đã tiêu) và tạm dừng bàn.
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        [field: SerializeField] public Level CurrentLevel { get; private set; }
        [field: SerializeField] public bool IsPause { get; private set; }
        [field: SerializeField] public LevelConfig LevelConfig { get; private set; }
        [field: SerializeField] public Transform LevelContainer { get; private set; }
        [field: SerializeField] public Reactive<LevelDifficulty> currentLevelDifficulty = new(LevelDifficulty.Normal);
        [SerializeField] private bool simulationFrozen;

        /// <summary>Idle board motion is stopped (rise, school hover, soft-body, tail). Input still uses <see cref="IsPause"/> only.</summary>
        public bool IsSimulationFrozen => simulationFrozen;

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
                // Lưu kèm level đang chơi: bộ đếm của level khác không được mang sang level này (LevelRetryRule).
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
        public LevelData CurrentLevelData => LevelConfig?.LevelData;

        /// <summary>Số liệu lượt chơi hiện tại cho level_background / level_complete / level_fail.</summary>
        public LevelSessionStats SessionStats { get; } = new LevelSessionStats();

        private void Start()
        {
            IsPause = true;
            GameGlobalEvent.OnFishPicked += OnStatsFishPicked;
            GameGlobalEvent.OnFishTapDenied += OnStatsFishDenied;
            GameGlobalEvent.OnHoldingChanged += SessionStats.OnHolding;
            InputManager.OnPointerPressed += OnStatsInput;
        }

        private void OnDestroy()
        {
            GameGlobalEvent.OnFishPicked -= OnStatsFishPicked;
            GameGlobalEvent.OnFishTapDenied -= OnStatsFishDenied;
            GameGlobalEvent.OnHoldingChanged -= SessionStats.OnHolding;
            InputManager.OnPointerPressed -= OnStatsInput;
        }

        private void OnStatsFishPicked(Fish _) => SessionStats.Taps++;
        private void OnStatsFishDenied(Fish _) => SessionStats.DeniedTaps++;
        private void OnStatsInput() => SessionStats.OnInput(Time.realtimeSinceStartup);

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

        public void SetSimulationFrozen(bool value)
        {
            simulationFrozen = value;
        }

        public async UniTask ClearLevel()
        {
            if (TutorialManager.Instance) TutorialManager.Instance.EndLevel();
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
        /// Nạp level hiện tại. false = không nạp được data (file hỏng, không có bản nào trên máy): KHÔNG sinh prefab Level,
        /// LevelConfig cũ giữ nguyên, nơi gọi tự xử lý (về Home, không trừ mạng).
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
            LevelConfig config = LevelGlobalConfig.Instance.GetLevelConfig(levelIndex, CurrentRetryCount);
            LevelData data = await config.LoadLevelDataAsync();
            if (data == null)
            {
                Debug.LogError($"[LevelManager] Level {levelIndex} ({config.LevelAddress}) không nạp được data, huỷ vào level.");
                return false;
            }

            LevelConfig = config;
            Debug.Log($"[LevelManager] Level {levelIndex} source={config.Source} difficulty={config.LevelType} retry={config.RetryCount} variant={config.Variant}");

            // Dữ liệu level tới từ CDN có thể dùng nội dung mà bản app này không có. Lỗi lúc dựng bàn phải thành
            // "nạp thất bại" để nơi gọi đưa người chơi về Home, không được để màn loading treo.
            bool built = await LevelLoadGuard.RunAsync(() => BuildLoadedLevelAsync(prefab, levelIndex), LogBuildError);
            if (!built)
            {
                await DiscardBrokenLevelAsync();
                return false;
            }

            ShowIntroThenResume().Forget();
            return true;
        }

        private async UniTask BuildLoadedLevelAsync(Level prefab, int levelIndex)
        {
            Transform container = LevelContainer != null ? LevelContainer : transform;
            CurrentLevel = Instantiate(prefab, container);
            CurrentLevel.transform.SetParent(container, false);
            CurrentLevel.transform.position = container.position;
            await UniTask.WaitForEndOfFrame();

            currentLevelDifficulty.Value = LevelConfig.LevelType;
            await CurrentLevel.StartGame(LevelConfig.LevelData);
            GameManager.Instance.SetState(GameState.InGame);
            CheckClaimBooster();
            CurrentReviveCount = 0;
            SessionStats.Reset(Time.realtimeSinceStartup);
            IngameFirebaseAnalystic.Instance.TrackLevelStart();
            UserPropertyManager.SetUserPropertyFirebase?.Invoke("Level", GameManager.Instance.Level.Value.ToString());
            LevelAddressableProvider.PrefetchUpcoming(levelIndex);
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

        /// <summary>
        /// Sau khi màn loading đóng: HUD + luật HUD theo level (TutorialManager.PrepareLevel) → popup mở khóa booster (kèm trọn luồng nhận + ép
        /// dùng thử) → popup obstacle mới → cảnh báo độ khó → kịch bản tutorial (khóa bấm trước khi bỏ pause) → bỏ pause.
        /// </summary>
        private async UniTask ShowIntroThenResume()
        {
            await UniTask.WaitUntil(IsDoneLoadingAndCanShowAfterLoadLevel, cancellationToken: this.GetCancellationTokenOnDestroy());
            await UniTask.WaitForSeconds(0.1f, cancellationToken: this.GetCancellationTokenOnDestroy());
            int level = GameManager.Instance.Level.Value;
            ScreenInGameContext.Events.LoadLevel?.Invoke();
            if (TutorialManager.Instance) TutorialManager.Instance.PrepareLevel(level);
            await CheckNewUnlockedItem();
            await CheckNewUnlockedObstacle();
            await CheckShowAlertDifficult();
            if (TutorialManager.Instance) await TutorialManager.Instance.BeginLevelAsync(level);
            SetPause(false);
        }
        
        private bool IsDoneLoadingAndCanShowAfterLoadLevel()
        {
            // Debug.Log("[LevelManager] IsDoneLoadingAndCanShowAfterLoadLevel1: " + !UIManager.Instance.IsHaveActivity<ActivityLoading>());
            // Debug.Log("[LevelManager] IsDoneLoadingAndCanShowAfterLoadLevel2: " +
            //           LoadingManager.Instance.IsLoadingComplete);
            return !UIManager.Instance.IsHaveActivity(nameof(ActivityLoading)) && LoadingManager.Instance.IsLoadingComplete;
        }

        public async UniTask CheckNewUnlockedItem()
        {
            string boosterKey = ItemGlobalConfig.Instance.GetNewBoosterUnlock(GameManager.Instance.Level.Value);
            if (string.IsNullOrEmpty(boosterKey)) return;
            await RunBoosterUnlockFlowAsync(boosterKey);
        }

        /// <summary>
        /// Popup mở khóa booster ("Booster_&lt;Type&gt;") rồi chờ trọn luồng nhận (đóng popup → booster bay về nút → ép dùng thử → booster chạy
        /// xong). Dùng ở đầu level (CheckNewUnlockedItem); gọi giữa level được (booster UnlockMidLevel). Giữa level cờ nhận booster của Level đang là
        /// true nên đặt về false trước; ScreenInGame.ClaimBooster đặt lại true khi xong. Không đặt thì IsBoosterClaimFlowSettled trả true ngay.
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

            // PushAsync chỉ chờ popup mở xong. Phải chờ trọn luồng nhận booster rồi mới cho intro kế tiếp hiện, nếu không popup obstacle mới
            // sẽ đè lên hiệu ứng bay / tutorial ép dùng booster.
            await UniTask.WaitUntil(IsBoosterClaimFlowSettled, cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        /// <summary>
        /// Luồng nhận booster mở khóa đã xong hẳn: ScreenInGame.ClaimBooster chạy hết, booster ép dùng thử không còn chờ chọn bể (Swap),
        /// overlay của nó (focus nút, tip, Spine của booster vừa dùng) đã đóng và cá trên bàn đã bay xong.
        /// </summary>
        private bool IsBoosterClaimFlowSettled()
        {
            if (CurrentLevel == null) return true;
            if (!CurrentLevel.GetIsBoosterClaimFlowDone()) return false;
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsUsingBooster) return false;

            ActivityContainer activityContainer = ActivityContainer.Find(ContainerKey.Activities);
            if (activityContainer != null)
            {
                if (activityContainer.IsInTransition) return false;
                for (int i = 0; i < BoosterTutorialActivities.Length; i++)
                {
                    if (activityContainer.Activities.ContainsKey(BoosterTutorialActivities[i])) return false;
                }
            }

            return CurrentLevel.Board == null || !CurrentLevel.Board.IsBusy;
        }

        private static readonly string[] BoosterTutorialActivities =
        {
            nameof(ActivityFocusButton), nameof(ActivityTipBooster),
            nameof(ActivityBoosterShuffle), nameof(ActivityBoosterFishingRod), nameof(ActivityBoosterPackup),
        };

        public async UniTask CheckNewUnlockedObstacle()
        {
            LevelData levelModel = LevelConfig?.LevelData;
            if (levelModel == null) return;

            // Which obstacles are actually present in this specific level layout.
            HashSet<ObstacleFeatureType> presentFeatures = levelModel.GetPresentObstacleFeatures();
            if (presentFeatures.Count == 0) return;

            NewFeatureData[] configs = NewFeatureGlobalConfig.Instance.NewFeatureDatas;
            if (configs == null) return;

            ModalContainer modalContainer = ModalContainer.Find(ContainerKey.Modals);
            
            // Iterate in config order so multiple new obstacles are introduced sequentially.
            for (int i = 0; i < configs.Length; i++)
            {
                NewFeatureData feature = configs[i];
                if (feature == null || feature.FeatureType == ObstacleFeatureType.None) continue;
                if (!presentFeatures.Contains(feature.FeatureType)) continue;          // not in this level
                if (ObstacleTutorialData.Instance.IsSeen(feature.FeatureId)) continue; // already introduced

                // Never overlap another modal: wait until the container is idle and empty.
                await UniTask.WaitUntil(() => !modalContainer.IsInTransition && modalContainer.Modals.Count == 0, cancellationToken: this.GetCancellationTokenOnDestroy());
                await UniTask.WaitForSeconds(0.3f, cancellationToken: this.GetCancellationTokenOnDestroy());

                // Luồng nhận booster (nếu có) tự mở pause khi xong: khóa bàn lại trong lúc popup obstacle hiện.
                SetPause(true);
                ViewOptions viewOptions = new ViewOptions(nameof(ModalNewObstacle));
                await modalContainer.PushAsync(viewOptions, feature.FeatureId);

                // Block until the player closes this popup before showing the next obstacle.
                await UniTask.WaitUntil(() => !modalContainer.IsInTransition && modalContainer.Modals.Count == 0, cancellationToken: this.GetCancellationTokenOnDestroy());

                // Persist immediately: this obstacle is never introduced again, in any session.
                ObstacleTutorialData.Instance.MarkSeen(feature.FeatureId);
            }
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

        /// <summary>Giá Play On lần này (900 / 1900 / 2300) theo số lần đã dùng trong level.</summary>
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
