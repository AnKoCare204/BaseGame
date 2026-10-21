using System;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Bật / tắt banner trong màn chơi. Firebase key ingame_banner_config, ví dụ: {"IsActive":1}
/// Thiếu key, JSON lỗi hoặc thiếu trường thì TẮT. Chỉ chặn việc hiện banner, SDK vẫn khởi tạo / tải banner như cũ.
/// ScreenInGame.LoadLevel chốt một lần lúc vào level (kéo theo vị trí thanh booster qua NoAdsSwitchPosition),
/// nên config về giữa level thì từ level sau mới áp.
/// </summary>
[Serializable]
public class BannerRemoteConfigData : IRemoteConfigData
{
    public static BannerRemoteConfigData Instance => RemoteConfigManager.Instance.BannerRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.ingame_banner_config;

    public int IsActive;

    public BannerRemoteConfigData()
    {
        ResetData();
    }

    /// <summary>Banner trong màn chơi được hiện khi config bật và người chơi chưa mua Remove Ads.</summary>
    public bool CanShow(bool isRemoveAds) => IsActive == 1 && !isRemoveAds;

    public void LoadData()
    {
        try
        {
            string json = FirebaseManager.Instance.GetConfigValue(KeyRemoteConfig.ToString()).StringValue;
            Debug.Log($"--- Load remote config for {KeyRemoteConfig}: {json}");
            FromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load remote config for {KeyRemoteConfig}: {e.Message}");
            ResetData();
        }
    }

    public void ResetData()
    {
        IsActive = 0;
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        ResetData();
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            // Ghi đè lên mặc định vừa reset: trường JSON không có thì giữ mặc định (tắt).
            JsonUtility.FromJsonOverwrite(json, this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BannerRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            ResetData();
        }
    }
}
