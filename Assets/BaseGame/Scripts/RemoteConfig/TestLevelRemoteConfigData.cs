using System;
using CoreData;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Remote config bật / tắt bộ level test và chọn Version (A/B test, chia nhóm bằng condition trên Firebase).
/// Khi bật: level nào có bản test cùng số trong bộ đó thì chơi bản test, level còn lại chơi bản production.
/// TestVersion = tên thư mục con trong TextAssets/TestLevel, phân biệt hoa thường, ví dụ "VersionA"
/// (address "Levels/TEST/VersionA/Level_{n}", bundle riêng label lvl_test_VersionA).
/// Firebase key test_level_ingame_config, ví dụ:
///   {"IsActiveCheat":1,"IsActiveRelease":0,"TestVersionCheat":"VersionA","TestVersionRelease":""}
/// User property test_level_version nhận "off" hoặc tên Version để tách số liệu theo nhóm.
/// </summary>
[Serializable]
public class TestLevelRemoteConfigData : IRemoteConfigData
{
    public const string UserPropertyName = "test_level_version";

    public static TestLevelRemoteConfigData Instance => RemoteConfigManager.Instance.TestLevelRemoteConfigData;

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.test_level_ingame_config;

    public int IsActiveCheat;
    public int IsActiveRelease;
    public string TestVersionCheat = "";
    public string TestVersionRelease = "";

    public int IsActive =>
#if CHEAT_ONLY
        IsActiveCheat;
#else
        IsActiveRelease;
#endif

    /// <summary>Version cho bản build hiện tại, đã chuẩn hoá. Rỗng = không có bộ nào.</summary>
    public string TestVersion => LevelAddress.SanitizeVersion(
#if CHEAT_ONLY
        TestVersionCheat
#else
        TestVersionRelease
#endif
    );

    /// <summary>Bật mà không chỉ Version hợp lệ thì coi như tắt.</summary>
    public bool IsEnabled => IsActive == 1 && TestVersion.Length > 0;

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

        if (IsActive == 1 && TestVersion.Length == 0)
        {
            Debug.LogWarning($"[TestLevelRemoteConfig] {KeyRemoteConfig} bật nhưng Version rỗng hoặc không hợp lệ, coi như tắt.");
        }

        // Lúc này chưa biết catalog có bộ đó không; LevelAddressableProvider báo lại khi đã có catalog.
        ReportUserProperty(null);
    }

    /// <summary>
    /// Giá trị user property test_level_version. availableInCatalog = false nghĩa là catalog đã nạp và KHÔNG có bộ này
    /// (sai tên, sai hoa thường, chưa upload): người chơi thực tế chơi bản production nên phải ghi "off".
    /// null = chưa kiểm tra được.
    /// </summary>
    public static string DescribeUserProperty(bool enabled, string version, bool? availableInCatalog)
    {
        if (!enabled || string.IsNullOrEmpty(version)) return "off";
        if (availableInCatalog == false) return "off";
        return version;
    }

    public void ReportUserProperty(bool? availableInCatalog)
    {
        UserPropertyManager.SetUserPropertyFirebase?.Invoke(UserPropertyName,
            DescribeUserProperty(IsEnabled, TestVersion, availableInCatalog));
    }

    public void ResetData()
    {
        IsActiveCheat = 0;
        IsActiveRelease = 0;
        TestVersionCheat = "";
        TestVersionRelease = "";
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        ResetData();
        if (string.IsNullOrEmpty(json)) return;

        TestLevelRemoteConfigData data;
        try
        {
            data = JsonUtility.FromJson<TestLevelRemoteConfigData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TestLevelRemoteConfig] JSON không hợp lệ, dùng mặc định: {e.Message}");
            return;
        }

        if (data == null) return;
        IsActiveCheat = data.IsActiveCheat;
        IsActiveRelease = data.IsActiveRelease;
        TestVersionCheat = data.TestVersionCheat ?? "";
        TestVersionRelease = data.TestVersionRelease ?? "";
    }
}
