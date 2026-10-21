using System;
using Game.Helper;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class TreetopRemoteConfig : IRemoteConfigData
{
    public static TreetopRemoteConfig Instance => RemoteConfigManager.Instance.TreetopRemoteConfig;
    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.treetop_mission_config;
    [field: SerializeField] public int LevelUnlock = 10;
    [field: SerializeField] public string TreetopRemoteStartDate = "01/01/2026 00:00:00";
    [field: SerializeField] public string TreetopRemoteEndDate = "01/01/2027 00:00:00";
    public DateTime TreetopStartDate => TreetopRemoteStartDate.StringToDateTime();
    public DateTime TreetopEndDate => TreetopRemoteEndDate.StringToDateTime();
    
    public bool IsBetweenEvent(DateTime now)
    {
        return now >= TreetopStartDate && now < TreetopEndDate;
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
        LevelUnlock = 10;
        TreetopRemoteStartDate = "01/01/2026 00:00:00";
        TreetopRemoteEndDate = "01/01/2027 00:00:00";
    }

    [Button]
    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public void FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return;
        JsonUtility.FromJsonOverwrite(json, this);
    }
}
