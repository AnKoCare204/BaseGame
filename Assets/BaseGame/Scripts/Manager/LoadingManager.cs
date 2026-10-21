using System.Threading;
using Core;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using SDK;
using Sirenix.OdinInspector;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Màn loading đầu game (khung AntFood, 1 scene): đợi manager sẵn sàng rồi vào thẳng level (người mới) hoặc Home.
/// Catalog level trên CDN và Remote Config nạp ở nền, không chặn màn loading.
/// </summary>
public class LoadingManager : DDOLSingleton<LoadingManager>
{
    [field: SerializeField, ReadOnly] private Reactive<float> LoadProgress { get; set; } = new(0f);
    [field: SerializeField] private GameObject CanvasFirstLoading { get; set; }
    [field: SerializeField] private Slider LoadProgressSlider { get; set; }
    [field: SerializeField] private TextMeshProUGUI LoadProgressText { get; set; }
    [field: SerializeField] private TextMeshProUGUI LoadDescriptionText { get; set; }

    [field: SerializeField,
            Tooltip(
                "ShaderVariantCollection ghi từ 1 lượt chơi; warm-up ở màn loading đầu để lần đầu tap/pop/outline không biên dịch shader tại chỗ")]
    private ShaderVariantCollection ShaderVariants { get; set; }

    public bool IsLoadingComplete { get; private set; }
    private MotionHandle LoadProgressMotion { get; set; }

    private static bool HasStartedFirstLoading { get; set; }
    private const float RemoteConfigWaitSeconds = 10f;
    private bool isBackgroundLoadStarted;

    private void Start()
    {
        if (Instance != this || HasStartedFirstLoading) return;
        HasStartedFirstLoading = true;
        LoadFirstProgressAsync().Forget();
    }

    public async UniTask LoadFirstProgressAsync()
    {
        if (CanvasFirstLoading != null) CanvasFirstLoading.SetActive(true);
        IsLoadingComplete = false;
        SetDescription("Loading Assets...");
        UpdateProgress(0.2f);

        await UniTask.WaitUntil(ManagersReady, cancellationToken: this.GetCancellationTokenOnDestroy());

        // Catalog level trên CDN nạp nền (không chặn loading): xong thì tải chunk quanh người chơi rồi sync mọi chunk đã đổi.
        // Level bake trong APK chơi được ngay dù chưa nạp catalog.
        LevelAddressableProvider.StartBootSync(UserData.Instance.CurrentLevel);

        SetDescription("Warming up...");
        UpdateProgress(0.4f);
        await WarmUpShadersAsync();

        SetDescription("Loading UI...");
        UpdateProgress(0.6f);
        await UIManager.Instance.PreloadCoreViewsAsync(!InGameDataManager.Instance.InGameData.TutData.IsPassFirstTut);
        UpdateProgress(0.9f);
        // Lỗi ở bước vào game không được chặn màn loading đầu, cũng không được chặn bước đọc Remote Config.
        if (!await LevelLoadGuard.RunAsync(DoneFirstLoading, LogFirstLoadingError))
        {
            await LevelLoadGuard.RunAsync(() => UIManager.Instance.ShowScreenMainMenu(), LogFirstLoadingError);
        }

        BackGroundLoadProgressAsync().Forget();
        UpdateProgress(1f);
        await WaitLoadingProgressCompleteAsync(this.GetCancellationTokenOnDestroy());
        IsLoadingComplete = true;
        if (CanvasFirstLoading != null) CanvasFirstLoading.SetActive(false);
    }

    /// <summary>
    /// Đợi Firebase Remote Config fetch xong (tối đa RemoteConfigWaitSeconds) rồi đọc mọi key. Hết giờ mà chưa xong
    /// (mở app offline, mạng chậm) thì vẫn đọc: Firebase trả giá trị đã activate ở phiên trước hoặc giá trị mặc định,
    /// và đọc lại một lần nữa nếu fetch xong muộn hơn trong phiên.
    /// </summary>
    private async UniTask BackGroundLoadProgressAsync()
    {
        if (isBackgroundLoadStarted) return;
        isBackgroundLoadStarted = true;
        if (!await LoadSeverTimeAsync())
        {
            isBackgroundLoadStarted = false;
            return;
        }
        await LoadRemoteConfigBackground();
    }
    private async UniTask<bool> LoadSeverTimeAsync()
    {
        LoadDescriptionText.text = "Syncing with server...";
        bool isInternetConnection = Application.internetReachability != NetworkReachability.NotReachable;
        if (!isInternetConnection)
        {
            LoadDescriptionText.text = "No internet connection. Cannot load server time.";
            Debug.LogError("No internet connection. Cannot load server time.");
            return false;
        }

        await DateTimeManager.TryGetTimeFromGoogle();
        await DateTimeManager.TrySyncWithServerAsync();
        if (!DateTimeManager.IsSynced)
        {
            LoadDescriptionText.text = "Failed to sync time with server.";
            Debug.LogError("Failed to sync time with server.");
            return false;
        }

        return true;
    }
    private async UniTask LoadRemoteConfigBackground()
    {
        Debug.Log("Loading Config...");
        await UniTask.WaitUntil(() => FirebaseRemoteConfigManager.IsReady, cancellationToken: this.GetCancellationTokenOnDestroy());
        DefaultGlobalConfig.Instance.ReloadRemoteConfig();
        RemoteConfigManager.Instance.FetchRemote();
        LevelGlobalConfig.Instance.ReloadRemoteConfig();
        LevelAddressableProvider.RequestSyncAllChunks();
        await StarBlazeManager.Instance.Initialize();
        await TreetopManager.Instance.Initialized();
        await ShipRaceManager.Instance.Initialize();
        AdsController.Instance.GetRemoteConfig();
        GameManager.Instance.OnFirebaseInitialized();
    }

    private static void LogFirstLoadingError(System.Exception e)
    {
        Debug.LogError($"[LoadingManager] Bước vào game lúc mở app lỗi, chuyển về Home: {e}");
    }

    private async UniTask WarmUpShadersAsync()
    {
        if (ShaderVariants == null || ShaderVariants.isWarmedUp) return;
        float deadline = Time.realtimeSinceStartup + 3f; // không kẹt loading nếu máy quá chậm
        while (!ShaderVariants.isWarmedUp && Time.realtimeSinceStartup < deadline)
        {
            ShaderVariants.WarmUpProgressively(64);
            await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        }

        Debug.Log(
            $"[LoadingManager] shader warm-up {(ShaderVariants.isWarmedUp ? "xong" : "hết giờ")} ({ShaderVariants.variantCount} variant)");
    }

    private static bool ManagersReady()
    {
        return GameManager.Instance != null && InGameDataManager.Instance != null &&
               PlayerResourceManager.Instance != null && UIManager.Instance != null;
    }

    private void SetDescription(string text)
    {
        if (LoadDescriptionText != null) LoadDescriptionText.text = text;
    }

    private void UpdateProgress(float targetProgress)
    {
        LoadProgressMotion.TryCancel();
        float current = LoadProgress.CurrentValue;
        float duration = Mathf.Max(0.05f, targetProgress - current);
        LoadProgressMotion = LMotion.Create(current, targetProgress, duration).Bind(OnUpdateProgress).AddTo(this);
    }

    private void OnUpdateProgress(float progress)
    {
        LoadProgress.Value = progress;
        if (LoadProgressSlider != null) LoadProgressSlider.value = Mathf.Lerp(0.1f, 1f, progress);
        if (LoadProgressText != null) LoadProgressText.text = $"{(int)(progress * 100)}%";
    }

    private async UniTask WaitLoadingProgressCompleteAsync(CancellationToken ct)
    {
        if (LoadProgress.CurrentValue >= 0.99f) return;
        await UniTask.WaitUntil(() => LoadProgress.CurrentValue >= 0.99f, cancellationToken: ct);
    }

    public async UniTask WaitForLoadingCompleteAsync(CancellationToken ct)
    {
        if (IsLoadingComplete) return;
        await UniTask.WaitUntil(() => IsLoadingComplete, cancellationToken: ct);
    }

    private async UniTask DoneFirstLoading()
    {
        // Level tự vào lúc mở app phải có sẵn trên máy; không có thì rơi về Home thay vì vào level trống.
        if (GameManager.Instance.TryLoadLevelPlaying() &&
            await GameManager.Instance.EnsureUpcomingLevelReadyAsync(LevelGateContext.Boot))
        {
            if (!PlayerResourceManager.Instance.HasLife && !PlayerResourceManager.Instance.HasInfiniteLife)
            {
                PlayerResourceManager.Instance.AddResourceValue(ResourceType.Life, 1, "first_loading", "first_loading");
            }

            if (await GameManager.Instance.LoadCurrentLevel())
            {
                await UIManager.Instance.ShowScreenInGame();
                return;
            }
        }

        await UIManager.Instance.ShowScreenMainMenu();
        LevelCatalog.CheckForUpdateAsync().Forget();
    }
}