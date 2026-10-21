using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using TW.Utility.DesignPattern;

/// <summary>
/// Gom các *RemoteConfigData. KHÔNG tự đọc lúc Start: Firebase lúc đó chưa fetch xong nên mọi key chỉ ra giá trị mặc định.
/// LoadingManager.BackGroundLoadProgressAsync gọi FetchRemote sau khi Firebase Remote Config sẵn sàng (hoặc hết giờ chờ).
/// </summary>
public class RemoteConfigManager : DDOLSingleton<RemoteConfigManager>
{
    public ReviveRemoteConfigData ReviveRemoteConfigData = new();
    public BoosterRemoteConfigData BoosterRemoteConfigData = new();
    public EndGameRewardRemoteConfigData EndGameRewardRemoteConfigData = new();
    public InterAdsCappingRemoteConfigData InterAdsCappingRemoteConfigData = new();
    public StartGoldRemoteConfigData StartGoldRemoteConfigData = new();
    public NoAdsPopupRemoteConfigData NoAdsPopupRemoteConfigData = new();
    public InterLevelRemoteConfigData InterLevelRemoteConfigData = new();
    public InterLoseRemoteConfigData InterLoseRemoteConfigData = new();
    public BannerRemoteConfigData BannerRemoteConfigData = new();
    private bool isRemoteConfigLoaded;

    /// <summary>Đã đọc Remote Config ít nhất một lần trong phiên (kể cả khi chỉ nhận được giá trị mặc định).</summary>
    public bool IsRemoteConfigLoaded => isRemoteConfigLoaded;

    public void FetchRemote()
    {
        ReviveRemoteConfigData.LoadData();
        BoosterRemoteConfigData.LoadData();
        EndGameRewardRemoteConfigData.LoadData();
        InterAdsCappingRemoteConfigData.LoadData();
        StartGoldRemoteConfigData.LoadData();
        NoAdsPopupRemoteConfigData.LoadData();
        InterLevelRemoteConfigData.LoadData();
        InterLoseRemoteConfigData.LoadData();
        BannerRemoteConfigData.LoadData();
        isRemoteConfigLoaded = true;
    }
    
    public void MarkLoadedWithDefaults()
    {
        isRemoteConfigLoaded = true;
    }

    public async UniTask TryApplyStartGoldRemoteConfig()
    {
        await UniTask.WaitUntil(HasLoadedRemoteConfig, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (ResourceData.Instance.ApplyStartGoldRemoteConfig(StartGoldRemoteConfigData.StartGold, UserData.Instance.SessionCount <= 1))
        {
            ResourceData.Instance.SaveData();
        }
    }

    private bool HasLoadedRemoteConfig() => isRemoteConfigLoaded;
}
