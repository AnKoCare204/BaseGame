using System;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Inter khi bấm X ở ModalConfirmLose (đã thua, về Home): hiện khi IsActive = 1 và level >= Level.
/// Firebase key inter_lose_config, ví dụ: {"IsActive":1,"Level":50}
/// Thiếu key hoặc JSON lỗi thì dùng mặc định (bật, level 50); thiếu trường nào thì trường đó giữ mặc định.
/// </summary>
[Serializable]
public class InterLoseRemoteConfigData : IRemoteConfigData
{
    public static InterLoseRemoteConfigData Instance => RemoteConfigManager.Instance.InterLoseRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.inter_lose_config;

    public int IsActive;
    public int Level;

    public InterLoseRemoteConfigData()
    {
        ResetData();
    }

    public bool CanShow(int level) => IsActive == 1 && level >= Level;

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
        Level = 50;
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
            Debug.LogWarning($"[InterLoseRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            ResetData();
        }
    }
}
