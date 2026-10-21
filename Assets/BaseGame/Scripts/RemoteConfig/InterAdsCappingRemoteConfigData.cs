using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>Khoảng cách tối thiểu giữa hai interstitial theo band level. Mặc định 120 giây (GDD AdConfigModel.InterShowFrequencyInSeconds).</summary>
[Serializable]
public class InterAdsCappingRemoteConfigData : IRemoteConfigData
{
    public static InterAdsCappingRemoteConfigData Instance => RemoteConfigManager.Instance.InterAdsCappingRemoteConfigData;

    [Serializable]
    public class LevelCappingConfig
    {
        public int MinLevel;
        public int MaxLevel;
        public float InterCapping;
        public int SkipFirstInterCountOnReopen;
    }

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.inter_ads_capping_config;
    public LevelCappingConfig[] LevelCappingConfigs;

    public InterAdsCappingRemoteConfigData()
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
        LevelCappingConfigs = new[]
        {
            new LevelCappingConfig { MinLevel = 1, MaxLevel = 100000, InterCapping = 120f, SkipFirstInterCountOnReopen = 0 },
        };
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        InterAdsCappingRemoteConfigData data = JsonUtility.FromJson<InterAdsCappingRemoteConfigData>(json);
        if (data?.LevelCappingConfigs != null && data.LevelCappingConfigs.Length > 0) LevelCappingConfigs = data.LevelCappingConfigs;
    }

    public float GetInterCapping(int level)
    {
        if (LevelCappingConfigs != null)
        {
            for (int i = 0; i < LevelCappingConfigs.Length; i++)
            {
                LevelCappingConfig c = LevelCappingConfigs[i];
                if (level >= c.MinLevel && level <= c.MaxLevel) return c.InterCapping;
            }
        }

        return 120f;
    }

    public int GetSkipFirstInterCountOnReopen(int level)
    {
        if (LevelCappingConfigs != null)
        {
            for (int i = 0; i < LevelCappingConfigs.Length; i++)
            {
                LevelCappingConfig c = LevelCappingConfigs[i];
                if (level >= c.MinLevel && level <= c.MaxLevel) return c.SkipFirstInterCountOnReopen;
            }
        }

        return 0;
    }
}
