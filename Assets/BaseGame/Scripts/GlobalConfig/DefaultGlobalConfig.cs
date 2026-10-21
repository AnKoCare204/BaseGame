using Sirenix.Utilities;
using UnityEngine;

/// <summary>Tham số toàn cục của game: mạng, giá Play On, mốc level hiện rate / vào thẳng bàn...</summary>
[CreateAssetMenu(fileName = "DefaultGlobalConfig", menuName = "GlobalConfigs/DefaultGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class DefaultGlobalConfig : GlobalConfig<DefaultGlobalConfig>
{
    [field: SerializeField] public bool PlayGameAtStart { get; set; }

    [field: SerializeField, Tooltip("Thắng level ≤ mức này thì ModalWin đi thẳng level kế, lớn hơn thì về Home")]
    public int DefaultBackToMenuLevel { get; set; } = 3;

    [field: SerializeField] public int DefaultX2CoinLevel { get; set; } = 5;

    [field: SerializeField, Tooltip("Level ≤ mức này vào thẳng bàn khi mở app, không qua Home")]
    public int DefaultLevelPlaying { get; set; } = 1;

    [field: SerializeField, Tooltip("Phút hồi 1 mạng")] public double DefaultFillHeartTime { get; set; } = 30;
    [field: SerializeField] public int DefaultLife { get; set; } = 5;
    [field: SerializeField, Tooltip("Giá nạp đầy mạng bằng coin")] public int DefaultFullHeartCoin { get; set; } = 900;
    [field: SerializeField] public bool IsHeart { get; set; } = true;
    [field: SerializeField] public int WinGameReward { get; set; } = 20;

    [field: SerializeField, Tooltip("Giá Play On theo lần trong level")] public int[] ReviveCoins { get; set; } = { 900, 1900, 2300 };
    [field: SerializeField] public float TimeDelayLose { get; set; } = 0.5f;

    [field: SerializeField] public int[] NumberShowNoAds { get; set; } = { 3 };
    [field: SerializeField] public int CountShowNoAdsMax { get; set; } = 1;
    [field: SerializeField] public bool IsUseADS { get; set; } = true;
    [field: SerializeField] public bool IsShowAdsFillHeart { get; set; } = true;
    [field: SerializeField] public int DefaultlevelShowRate { get; set; } = 12;

    /// <summary>Chưa có remote config; giữ giá trị trong asset.</summary>
    public void ReloadRemoteConfig()
    {
    }
}
