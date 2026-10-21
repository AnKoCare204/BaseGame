using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

[Serializable]
public class NoAdsPopupRemoteConfigData : IRemoteConfigData
{
    public static NoAdsPopupRemoteConfigData Instance => RemoteConfigManager.Instance.NoAdsPopupRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.noads_popup_config;

    public int IsActive = 1;
    public int ShowPerDay = 2;
    public int GapSeconds = 300;

    public NoAdsPopupRemoteConfigData()
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
        IsActive = 1;
        ShowPerDay = 2;
        GapSeconds = 300;
    }

    [Button]
    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            ResetData();
            return;
        }

        NoAdsPopupRemoteConfigData data = JsonUtility.FromJson<NoAdsPopupRemoteConfigData>(json);
        if (data == null)
        {
            ResetData();
            return;
        }

        IsActive = data.IsActive;
        ShowPerDay = data.ShowPerDay;
        GapSeconds = data.GapSeconds;
    }

    public bool IsEnabled => IsActive == 1;
}
