using System;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Gợi ý khi đứng yên (IdleHintController). Firebase key idle_hint_config, ví dụ:
/// {"IsActive":1,"MaxLevel":15,"EarlyMaxLevel":3,"EarlyDelay":5,"LateDelay":8}
/// Level 1..EarlyMaxLevel chờ EarlyDelay giây, tới MaxLevel chờ LateDelay giây, sau đó tắt.
/// Thiếu key hoặc JSON lỗi thì dùng mặc định trên; thiếu trường nào thì trường đó giữ mặc định.
/// </summary>
[Serializable]
public class IdleHintRemoteConfigData : IRemoteConfigData
{
    public static IdleHintRemoteConfigData Instance => RemoteConfigManager.Instance.IdleHintRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.idle_hint_config;

    public int IsActive;
    public int MaxLevel;
    public int EarlyMaxLevel;
    public float EarlyDelay;
    public float LateDelay;

    public IdleHintRemoteConfigData()
    {
        ResetData();
    }

    /// <summary>Số giây đứng yên trước khi gợi ý ở level này; -1 = không gợi ý (tắt, ngoài khoảng level, hoặc delay không dương).</summary>
    public float GetDelay(int level)
    {
        if (IsActive != 1 || level < 1 || level > MaxLevel) return -1f;
        float delay = level <= EarlyMaxLevel ? EarlyDelay : LateDelay;
        return delay > 0f ? delay : -1f;
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
        MaxLevel = 15;
        EarlyMaxLevel = 3;
        EarlyDelay = 5f;
        LateDelay = 8f;
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
            Debug.LogWarning($"[IdleHintRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            ResetData();
        }
    }
}
