using System;
using SDK;
using Sirenix.OdinInspector;
using TW.Utility.Extension;
using UnityEngine;

[System.Serializable]
public class StarBlazeRemoteConfig : IRemoteConfigData
{
    public static StarBlazeRemoteConfig Instance => RemoteConfigManager.Instance.StarBlazeRemoteConfig;
    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.starblaze_config;
    [field: SerializeField] public int LevelUnlock = -1;
    [field: SerializeField] public string StarBlazeRemoteStartDate = "09/15/2025 00:00:00";
    [field: SerializeField] public string StarBlazeRemoteEndDate = "09/21/2025 00:00:00";
    public DateTime StarBlazeStartDate => StarBlazeRemoteStartDate.StringToDateTime();
    public DateTime StarBlazeEndDate => StarBlazeRemoteEndDate.StringToDateTime();
    public bool IsBetweenEvent(DateTime now)
    {
        return now >= StarBlazeStartDate && now < StarBlazeEndDate;
    }
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

    }

    [Button]
    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public void FromJson(string json)
    {
        JsonUtility.FromJsonOverwrite(json, this);
    }
}