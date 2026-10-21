using CoreData;
using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>
/// Cấu hình mua booster: có nút xem ads theo số lần, giá remote ghi đè. Stub: mặc định luôn có ads, giá lấy từ ItemGlobalConfig.
/// </summary>
[Serializable]
public class BoosterRemoteConfigData : IRemoteConfigData
{
    public static BoosterRemoteConfigData Instance => RemoteConfigManager.Instance.BoosterRemoteConfigData;

    [Serializable]
    public class AdsBoosterConfig
    {
        public int AdsCount;
        public int HasAds;
    }

    [Serializable]
    public class BoosterAdsConfigEntry
    {
        public string BoosterTypeName;
        public AdsBoosterConfig[] AdsBoosterConfigs;
        public int IsRemotePrice;
        public int NewPrice;
    }

    public BoosterAdsConfigEntry[] BoosterAdsConfigs = Array.Empty<BoosterAdsConfigEntry>();

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.ingame_booster_config;

    public void LoadData()
    {
        try
        {
            string json = FirebaseManager.Instance.GetConfigValue(KeyRemoteConfig.ToString()).StringValue;
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
        BoosterAdsConfigs = Array.Empty<BoosterAdsConfigEntry>();
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        BoosterRemoteConfigData data = JsonUtility.FromJson<BoosterRemoteConfigData>(json);
        if (data?.BoosterAdsConfigs != null) BoosterAdsConfigs = data.BoosterAdsConfigs;
    }

    private BoosterAdsConfigEntry FindEntry(ResourceType boosterType)
    {
        if (BoosterAdsConfigs == null) return null;
        string name = boosterType.ToString();
        for (int i = 0; i < BoosterAdsConfigs.Length; i++)
        {
            if (BoosterAdsConfigs[i] != null && BoosterAdsConfigs[i].BoosterTypeName == name) return BoosterAdsConfigs[i];
        }

        return null;
    }

    public int GetCurrentBoosterAds(ResourceType boosterType, int adsCount)
    {
        BoosterAdsConfigEntry entry = FindEntry(boosterType);
        if (entry?.AdsBoosterConfigs == null || entry.AdsBoosterConfigs.Length == 0) return 1;
        for (int i = 0; i < entry.AdsBoosterConfigs.Length; i++)
        {
            if (entry.AdsBoosterConfigs[i].AdsCount == adsCount) return entry.AdsBoosterConfigs[i].HasAds;
        }

        return entry.AdsBoosterConfigs[^1].HasAds;
    }

    public bool HasAdsForBooster(ResourceType boosterType, int adsCount) => GetCurrentBoosterAds(boosterType, adsCount) == 1;

    public int GetBoosterPriceX3(ResourceType boosterType)
    {
        BoosterAdsConfigEntry entry = FindEntry(boosterType);
        if (entry != null && entry.IsRemotePrice == 1 && entry.NewPrice > 0) return entry.NewPrice;
        BoosterInfoData data = ItemGlobalConfig.Instance.GetBoosterData(boosterType);
        return data != null ? data.PriceX3 : 900;
    }

    public bool IsRemotePriceActive(ResourceType boosterType)
    {
        BoosterAdsConfigEntry entry = FindEntry(boosterType);
        return entry != null && entry.IsRemotePrice == 1;
    }
}
