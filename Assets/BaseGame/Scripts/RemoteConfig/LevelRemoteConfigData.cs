using System;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Remote config cho level Addressables: level tối đa chơi được của từng bản build.
/// Thứ tự LiveOps khi thêm level: upload bundle lên CDN TRƯỚC (Build Levels + Upload), rồi mới tăng
/// LevelMaxCheat / LevelMaxRelease trên Firebase. Làm ngược thì người chơi tới level chưa có trên CDN và gặp ComingSoon.
/// Giá trị 0 = chưa cấu hình, client dùng LevelGlobalConfig.LevelMaxInConfig.
/// Firebase key level_ingame_config, ví dụ: {"LevelMaxCheat":150,"LevelMaxRelease":150}
/// </summary>
[Serializable]
public class LevelRemoteConfigData : IRemoteConfigData
{
    public static LevelRemoteConfigData Instance => RemoteConfigManager.Instance.LevelRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.level_ingame_config;

    public int LevelMaxCheat;
    public int LevelMaxRelease;

    /// <summary>Level tối đa cho bản build hiện tại, chọn theo define của Build Profile.</summary>
    public int LevelMax =>
#if CHEAT_ONLY
        LevelMaxCheat;
#else
        LevelMaxRelease;
#endif

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
        LevelMaxCheat = 0;
        LevelMaxRelease = 0;
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        ResetData();
        if (string.IsNullOrEmpty(json)) return;

        LevelRemoteConfigData data;
        try
        {
            data = JsonUtility.FromJson<LevelRemoteConfigData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            return;
        }

        if (data == null) return;
        LevelMaxCheat = Mathf.Max(0, data.LevelMaxCheat);
        LevelMaxRelease = Mathf.Max(0, data.LevelMaxRelease);
    }
}
