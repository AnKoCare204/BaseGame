using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>Vàng tặng thêm cho user mới qua remote. Mặc định 0 vì coin khởi tạo 1000 đã nằm trong ResourceData (GDD InitialCoin).</summary>
[Serializable]
public class StartGoldRemoteConfigData : IRemoteConfigData
{
    public static StartGoldRemoteConfigData Instance => RemoteConfigManager.Instance.StartGoldRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.start_gold_config;
    public int StartGold;

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
        
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            ResetData();
            return;
        }

        StartGoldRemoteConfigData data = JsonUtility.FromJson<StartGoldRemoteConfigData>(json);
        StartGold = data != null ? data.StartGold : 0;
    }
}
