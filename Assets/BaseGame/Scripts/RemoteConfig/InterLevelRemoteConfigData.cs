using System;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Level bắt đầu hiện interstitial. Firebase key inter_level_config, ví dụ: {"LevelShowInter":15}
/// Hai chỗ so sánh khác nhau (giữ như khi còn nằm trong DefaultGlobalConfig):
///   - Thắng (ModalWin): hiện inter khi level > LevelShowInter.
///   - Thoát level (ModalLeaveGame qua GameManager.CanShowAdsInter): hiện khi level >= LevelShowInter.
/// Thiếu key, JSON lỗi hoặc thiếu trường thì dùng mặc định 15.
/// </summary>
[Serializable]
public class InterLevelRemoteConfigData : IRemoteConfigData
{
    public static InterLevelRemoteConfigData Instance => RemoteConfigManager.Instance.InterLevelRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.inter_level_config;

    public int LevelShowInter;

    public InterLevelRemoteConfigData()
    {
        ResetData();
    }

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
        LevelShowInter = 15;
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        ResetData();
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            // Ghi đè lên mặc định vừa reset: trường JSON không có thì giữ mặc định.
            JsonUtility.FromJsonOverwrite(json, this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InterLevelRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            ResetData();
        }
    }
}
