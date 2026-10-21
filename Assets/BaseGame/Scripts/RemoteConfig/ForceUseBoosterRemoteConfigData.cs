using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>Ép dùng thử booster ngay level mở khóa (GDD TutorialConfigModel.BoosterTutorialAtUnlock = true).</summary>
[Serializable]
public class ForceUseBoosterRemoteConfigData : IRemoteConfigData
{
    public static ForceUseBoosterRemoteConfigData Instance => RemoteConfigManager.Instance.ForceUseBoosterRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.force_use_booster_config;
    public int IsForceUseBooster = 1;

    public bool IsEnabled => IsForceUseBooster == 1;

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
        IsForceUseBooster = 1;
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        ForceUseBoosterRemoteConfigData data = JsonUtility.FromJson<ForceUseBoosterRemoteConfigData>(json);
        if (data != null) IsForceUseBooster = data.IsForceUseBooster;
    }
}
