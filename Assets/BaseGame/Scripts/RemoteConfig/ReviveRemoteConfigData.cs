using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>
/// Giá Play On (chơi tiếp khi kẹt) theo số lần đã dùng trong level: 900 → 1900 → 2300 (Docs/GDD/09_Economy.tsv).
/// Stub: chưa có remote config, giữ giá trị mặc định.
/// </summary>
[Serializable]
public class ReviveRemoteConfigData : IRemoteConfigData
{
    public static ReviveRemoteConfigData Instance => RemoteConfigManager.Instance.ReviveRemoteConfigData;

    [Serializable]
    public class ReviveConfig
    {
        public int ReviveCount;
        public int ReviveCost;
    }

    [Serializable]
    public class AdsReviveConfig
    {
        public int ReviveCount;
        public int HasAds;
    }

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.revive_config;
    public ReviveConfig[] ReviveConfigs;
    public AdsReviveConfig[] AdsReviveConfigs;

    public ReviveRemoteConfigData()
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
        ReviveConfigs = new[]
        {
            new ReviveConfig { ReviveCount = 0, ReviveCost = 900 },
            new ReviveConfig { ReviveCount = 1, ReviveCost = 1900 },
            new ReviveConfig { ReviveCount = 2, ReviveCost = 2300 },
        };
        AdsReviveConfigs = new[]
        {
            new AdsReviveConfig { ReviveCount = 0, HasAds = 1 },
            new AdsReviveConfig { ReviveCount = 1, HasAds = 1 },
            new AdsReviveConfig { ReviveCount = 2, HasAds = 1 },
        };
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        ReviveRemoteConfigData data = JsonUtility.FromJson<ReviveRemoteConfigData>(json);
        if (data == null) return;
        if (data.ReviveConfigs != null && data.ReviveConfigs.Length > 0) ReviveConfigs = data.ReviveConfigs;
        if (data.AdsReviveConfigs != null && data.AdsReviveConfigs.Length > 0) AdsReviveConfigs = data.AdsReviveConfigs;
    }

    public int GetCurrentReviveCoin(int reviveCount)
    {
        if (ReviveConfigs == null || ReviveConfigs.Length == 0) return 900;
        for (int i = 0; i < ReviveConfigs.Length; i++)
        {
            if (ReviveConfigs[i].ReviveCount == reviveCount) return ReviveConfigs[i].ReviveCost;
        }

        return ReviveConfigs[^1].ReviveCost;
    }

    public int GetCurrentReviveAds(int reviveCount)
    {
        if (AdsReviveConfigs == null || AdsReviveConfigs.Length == 0) return 1;
        for (int i = 0; i < AdsReviveConfigs.Length; i++)
        {
            if (AdsReviveConfigs[i].ReviveCount == reviveCount) return AdsReviveConfigs[i].HasAds;
        }

        return AdsReviveConfigs[^1].HasAds;
    }
}
