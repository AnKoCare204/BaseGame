using Core.Manager;
using Core.UI.Activities;
using Core.UI.Modals;
using Core.UI.Screens;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using DataModule;
using Manager;
using R3;
using Sirenix.OdinInspector;
using System;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

namespace Core
{
    public enum GameState
    {
        None,
        MainMenu,
        InGame,
        Win,
        Lose,
    }

    /// <summary>
    /// Vòng đời game (khung AntFood/FishSort): vào level, thắng, kẹt (Play On), thua, về Home.
    /// Mọi điều hướng UI đi qua UIManager; gameplay gọi WinLevel / LoseLevel.
    /// </summary>
    public class GameManager : DDOLSingleton<GameManager>
    {
#if CHEAT_ONLY
        [ShowInInspector] public BuildType BuildType => BuildType.Cheat;
#elif RELEASE_ONLY
        [ShowInInspector] public BuildType BuildType => BuildType.Release;
#else
        /// <summary>Editor mặc định Cheat: rewarded / IAP tự thành công (SDK chưa gắn).</summary>
        [ShowInInspector] public BuildType BuildType => Application.isEditor ? BuildType.Cheat : BuildType.Release;
#endif

        [field: SerializeField] public Reactive<int> Level { get; private set; } = new(0);
        public int LastLevelPlayed => PlayerData.Instance.GetLastLevelPlayed();

        /// <summary>Kết quả màn gần nhất, dùng cho param "status" của event IAP: "only start" | "win" | "lose".</summary>
        public string LastLevelResult { get; private set; } = "only start";

        [field: SerializeField] public SerializableReactiveProperty<bool> IsPauseGame { get; private set; } = new(false);

        [field: SerializeField] public GameState GameState { get; set; } = GameState.None;
        [ShowInInspector] public bool InGame => IsState(GameState.InGame);
        public int MaxLife => DefaultGlobalConfig.Instance.DefaultLife;

        public const string UnstableNetworkMessage = "Unstable network connection. \nPlease try again.";
        private const string LevelLoadFailedMessage = "Could not load the level. \nPlease try again.";

        /// <summary>Kết quả của lần LoadCurrentLevel gần nhất chạy trong màn loading, để bước sau loading biết mà xử lý.</summary>
        private bool lastLevelLoadSucceeded = true;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Level.Value = InGameDataManager.Instance.InGameData.UserData.CurrentLevel;
            Level.Subscribe(OnLevelChanged).AddTo(this);
        }

        public bool IsState(GameState state) => GameState == state;

        public void SetState(GameState state)
        {
            GameState = state;
            if (state != GameState.Win) winFlowStarted = false;
        }

        private void OnLevelChanged(int level)
        {
            UserData.Instance.CurrentLevel = level;
            UserData.Instance.SaveData();
            if (PlayerData.Instance.CurrentLevel.Value != level)
            {
                PlayerData.Instance.CurrentLevel.Value = level;
                PlayerData.Instance.SaveData();
            }
        }
        public bool TryLoadLevelPlaying()
        {
            return Level.CurrentValue <= DefaultGlobalConfig.Instance.DefaultLevelPlaying;
        }

        private static bool HasLifeOrInfinite() =>
            PlayerResourceManager.Instance.HasLife || PlayerResourceManager.Instance.HasInfiniteLife;
        

        /// <summary>false = không nạp được level; khi đó không trừ mạng và số lần retry được trả lại.</summary>
        public async UniTask<bool> LoadCurrentLevel()
        {
            if (string.IsNullOrEmpty(UserData.Instance.FirstSessionDay))
            {
                UserData.Instance.SetFirstSessionDay();
            }

            UserData.Instance.SetLastSessionDay();
            int previousRetryCount = UserData.Instance.LevelRetryCount;
            int previousRetryLevel = UserData.Instance.LevelRetryLevel;
            // Lượt đầu của level = 0; bộ đếm còn của level khác (nhảy level bằng cheat...) thì đếm lại từ 0.
            LevelManager.Instance.CurrentRetryCount = UserData.Instance.GetNextRetry(Level.Value);
            if (!await LevelManager.Instance.LoadLevel())
            {
                // Setter ghi kèm level hiện tại, nên ghi lại cặp cũ sau đó.
                LevelManager.Instance.CurrentRetryCount = previousRetryCount;
                UserData.Instance.SetLevelRetry(previousRetryCount, previousRetryLevel);
                return false;
            }
            PlayerData.Instance.AddLevelPlayed(Level.Value);
            PlayerResourceManager.Instance.SubResourceValue(ResourceType.Life, 1, ResourcePos.play_level.ToString(), "");
            return true;
        }

        /// <summary>Next / Retry: modal Win / Lose còn mở trên bàn cũ nên đóng trước khi nạp.</summary>
        private UniTask LoadCurrentLevelTask() => RunLevelLoadTask(true);

        /// <summary>Play ở Home: giữ ModalHome để nạp lỗi thì còn bật lại nút Play (RecoverFromFailedLoadAtHome).</summary>
        private UniTask LoadCurrentLevelFromMenuTask() => RunLevelLoadTask(false);

        // Chạy BÊN TRONG màn loading: một exception lọt ra sẽ làm màn loading không bao giờ đóng,
        // nên mọi lỗi đều được bắt lại và tính là nạp thất bại.
        private async UniTask RunLevelLoadTask(bool closeModalsFirst)
        {
            lastLevelLoadSucceeded = false;
            await LevelLoadGuard.RunAsync(async () =>
            {
                if (closeModalsFirst) await UIManager.Instance.CloseAllModal();
                bool loaded = await LoadCurrentLevel();
                // Mọi đường vào level đều dựng lại ScreenInGame (như AntFood): HUD mới, không mang state của ván trước.
                if (loaded) await UIManager.Instance.ShowScreenInGame();
                lastLevelLoadSucceeded = loaded;
            }, LogLevelLoadError);
        }

        private static void LogLevelLoadError(Exception e)
        {
            Debug.LogError($"[GameManager] Vào level lỗi: {e}");
        }

        /// <summary>Chạy sau khi màn loading đóng: báo lỗi nếu lần nạp level vừa rồi thất bại.</summary>
        private UniTask RecoverFromFailedLoadAtHome()
        {
            if (lastLevelLoadSucceeded) return UniTask.CompletedTask;
            ActivityNotificationOverlayContext.Events.ShowError?.Invoke(LevelLoadFailedMessage);
            ModalHomeContext.Events.SetPlayInteractable?.Invoke(true);
            return UniTask.CompletedTask;
        }

        private async UniTask RecoverFromFailedLoadInGame()
        {
            if (lastLevelLoadSucceeded) return;
            ActivityNotificationOverlayContext.Events.ShowError?.Invoke(LevelLoadFailedMessage);
            await TryBackToMainMenu();
        }

        private async UniTask BackToMainMenuTask()
        {
            await UIManager.Instance.ShowScreenMainMenu();
            ScreenMainMenuContext.Events.SetInteractable?.Invoke(false);
            UIMainMenuTab.SetTabLock(this, true);
            LevelManager.Instance.ClearLevel().Forget();
        }

        public async UniTask<bool> TryLoadCurrentLevel()
        {
            if (!HasLifeOrInfinite())
            {
                await UIManager.Instance.ShowModalFillHeart();
                return false;
            }

            await UIManager.Instance.ShowLoadingWithTask(LoadCurrentLevelFromMenuTask, RecoverFromFailedLoadAtHome);
            return true;
        }

        /// <summary>Thắng → level kế.</summary>
        public async UniTask TryLoadNextLevel()
        {

            await UIManager.Instance.ShowLoadingWithTask(LoadCurrentLevelTask, RecoverFromFailedLoadInGame);
        }

        /// <summary>Thua → chơi lại (tốn 1 mạng).</summary>
        public async UniTask TryRetryCurrentLevel()
        {
            if (!HasLifeOrInfinite())
            {
                await UIManager.Instance.ShowModalFillHeart();
                return;
            }

            await UIManager.Instance.ShowLoadingWithTask(LoadCurrentLevelTask, RecoverFromFailedLoadInGame);
        }

        /// <summary>Về Home (Win / Lose / Pause / Leave).</summary>
        public async UniTask TryBackToMainMenu()
        {
            await UIManager.Instance.ShowLoadingWithTask(BackToMainMenuTask, BackToMainMenuTaskComplete);
        }
        
        private async UniTask BackToMainMenuTaskComplete()
        {
            await RewardManager.Instance.TryClaimRewardGamePlay();

            UIMainMenuTab.SetTabLock(this, false);
            
            await NoAdsPopupManager.TryShowOnHome();
            ScreenMainMenuContext.Events.SetInteractable?.Invoke(true);

        }

        // ===================== Thắng =====================

        /// <summary>WinLevel đã chạy cho màn này (GameState.Win được đặt sớm ở PrepareWin nên không dùng state làm guard). SetState reset khi rời Win.</summary>
        private bool winFlowStarted;

        /// <summary>
        /// Bàn báo thắng đã chắc trong khi animation cuối còn chạy. Khoá ngay để HUD không bấm được (Pause → Leave / Retry chạy song
        /// song với WinLevel làm nhảy level, booster mở popup mua rồi bị đóng giữa chừng): state Win, pause input bàn, tắt tương tác
        /// HUD, đóng modal đang mở. Level gọi WinLevel ngay sau (xem Level.Win).
        /// </summary>
        public void PrepareWin()
        {
            if (IsState(GameState.Win) || IsState(GameState.Lose)) return;
            SetState(GameState.Win);
            LevelManager.Instance.SetPause(true);
            ScreenInGameContext.Events.SetHudInteractable?.Invoke(false);
            UIManager.Instance.CloseAllModal().Forget();
        }

        /// <summary>Level gọi khi mọi đơn đã xong. Đóng UI, chạy ActivityCompleted, cộng level, mở ModalWin, +1 mạng.</summary>
        [Button]
        public async UniTask WinLevel()
        {
            if (winFlowStarted) return;
            winFlowStarted = true;
            SetState(GameState.Win);
            var startedLevel = LevelManager.Instance.CurrentLevel;
            LevelManager.Instance.SetPause(true);
            ScreenInGameContext.Events.HideBotPanel?.Invoke();
            await UIManager.Instance.CloseAllModal();
            await UIManager.Instance.CloseAllActivities();

            ActivityContainer activityContainer = ActivityContainer.Find(ContainerKey.Activities);
            ViewOptions activityCompleted = new ViewOptions(nameof(ActivityCompleted));
            await activityContainer.ShowAsync(activityCompleted);
            await UniTask.WaitUntil(IsCloseActivityCompleted, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (startedLevel != null && LevelManager.Instance.CurrentLevel != startedLevel) return;
            int playTime = LevelManager.Instance.GetPlayTimeToNow();
            int reviveCount = LevelManager.Instance.CurrentReviveCount;
            int retryCount = LevelManager.Instance.CurrentRetryCount;
            int completedLevel = Level.Value;
            int dayRetention = UserData.Instance.RecordCheckPointDayRetention();
            LastLevelResult = "win";
            EventTrackingManager.TrackEventAppsflyer?.Invoke("af_level_achieved", new AnalyticsParameter("level", Level.Value));
            IngameFirebaseAnalystic.Instance.TrackLevelComplete(playTime, reviveCount, retryCount);
            IngameFirebaseAnalystic.Instance.TrackCheckPointEnd(completedLevel, retryCount, playTime,
                LevelManager.Instance.CurrentLevelBoosterSpend, LevelManager.Instance.CurrentLevelCoinSpend,
                LevelManager.Instance.CurrentLevelAdsCount, dayRetention);
            LevelManager.Instance.CurrentRetryCount = -1;
            Level.Value += 1;
            
            CheckLevelFirstTry();
            
            await UIManager.Instance.ShowModalWin();
            PlayerResourceManager.Instance.AddResourceValue(ResourceType.Life, 1, ResourcePos.win_level.ToString(), ResourcePos.free.ToString());
            return;

            bool IsCloseActivityCompleted()
            {
                if (activityContainer.Activities.Count == 0) return true;
                return !activityContainer.Activities.ContainsKey(nameof(ActivityCompleted));
            }
        }

        // ===================== Kẹt / thua =====================

        private async UniTask LoseLevel(Func<UniTask> showModalTask)
        {
            if (IsState(GameState.Lose) || IsState(GameState.Win)) return;
            await UIManager.Instance.CloseAllModal();
            await UIManager.Instance.CloseAllActivities();
            SetState(GameState.Lose);
            LastLevelResult = "lose";
            LevelManager.Instance.CurrentReviveCount += 1;
            await showModalTask();
        }

        /// <summary>Kẹt (hết nước đi, hết giờ...): mở ModalLose = Play On. Từ chối → ModalConfirmLose → chốt thua.</summary>
        [Button]
        public async UniTask LoseLevel()
        {
            LevelManager.Instance.SetPause(true);
            await LoseLevel(UIManager.Instance.ShowModalLose);
        }

        /// <summary>Chốt thua (sau khi từ chối Play On): ghi nhận rồi mở ModalConfirmLose (Try again / Home).</summary>
        public async UniTask FailedLevel()
        {
            LastLevelResult = "lose";
            await UIManager.Instance.CloseAllModal();
            ViewOptions modalConfirmLose = new ViewOptions(nameof(ModalConfirmLose));
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(modalConfirmLose, true);
        }

        // ===================== Điều kiện phụ =====================

        public bool CanShowAdsInter()
        {
            return Level.Value >= InterLevelRemoteConfigData.Instance.LevelShowInter;
        }

        public bool CanShowRate()
        {
            return Level.Value >= DefaultGlobalConfig.Instance.DefaultlevelShowRate && !RateData.Instance.IsShowRate;
        }

        public bool CanShowInterLose()
        {
            return InterLoseRemoteConfigData.Instance.CanShow(Level.Value);
        }

        /// <summary>Tutorial booster: khoanh sáng nút booster vừa mở khóa, ép bấm thử.</summary>
        public async UniTask ForceUseBooster(ResourceType boosterType)
        {
            if (!BoosterSelector.TryGetButton(boosterType, out Button button)) return;
            ViewOptions viewOptions = new ViewOptions(nameof(ActivityFocusButton));
            await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(viewOptions, button);
        }

        public async UniTask ForceUseBoosterCompleted()
        {
            ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
            await UniTask.WaitUntil(IsCloseActivityFocusButton, cancellationToken: this.GetCancellationTokenOnDestroy());
            return;

            bool IsCloseActivityFocusButton()
            {
                if (container.Activities.Count == 0) return true;
                return !container.Activities.ContainsKey(nameof(ActivityFocusButton));
            }
        }
        
        /// <summary>
        /// Gọi sau khi Remote Config đọc xong (LoadingManager). Chỗ xử lý tiến độ event còn treo của phiên trước (thắng/thua dở khi
        /// tắt app): so PlayerData.GetLastLevelPlayed() với Level hiện tại. Template chưa có event nên để trống.
        /// </summary>
        public void OnFirebaseInitialized()
        {
        }

        [Button]
        private void TestAddInfinity(int amount = 60)
        {
            PlayerResourceManager.Instance.AddResourceValue(ResourceType.InfiniteLife, amount, "", "");
        }
        
        public void CheckLevelFirstTry()
        {
            if (PlayerData.Instance.IsFirstTryLevel())
            {
                PlayerData.Instance.FirstTryCount++;
                PlayerData.Instance.SaveData();
            }
        }


    }
}

public enum BuildType
{
    Release,
    Cheat,
    Test
}
