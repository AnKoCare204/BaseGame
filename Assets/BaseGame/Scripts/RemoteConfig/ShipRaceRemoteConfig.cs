using System;
using Game.Helper;
using SDK;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class ShipRaceRemoteConfig : IRemoteConfigData
{
    public static ShipRaceRemoteConfig Instance => RemoteConfigManager.Instance.ShipRaceRemoteConfig;
    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.shiprace_config;
    [field: SerializeField] public int LevelUnlock = 10;
    [field: SerializeField] public string ShipRaceRemoteStartDate = "01/01/2026 00:00:00";
    [field: SerializeField] public string ShipRaceRemoteEndDate = "01/01/2027 00:00:00";
    /// <summary>
    /// Per stage tuning, indexed by StageId. Empty (the default) means every stage keeps the value
    /// authored in ShipRaceGlobalConfig, so shipping without this key changes nothing.
    /// </summary>
    [field: SerializeField] public ShipRaceStageRemoteConfig[] Stages = Array.Empty<ShipRaceStageRemoteConfig>();

    public DateTime ShipRaceStartDate => ShipRaceRemoteStartDate.StringToDateTime();
    public DateTime ShipRaceEndDate => ShipRaceRemoteEndDate.StringToDateTime();

    public bool IsBetweenEvent(DateTime now)
    {
        return now >= ShipRaceStartDate && now < ShipRaceEndDate;
    }

    public ShipRaceStageRemoteConfig GetStageConfig(int stageId)
    {
        if (Stages == null) return null;
        foreach (ShipRaceStageRemoteConfig stage in Stages)
        {
            if (stage != null && stage.StageId == stageId) return stage;
        }

        return null;
    }

    /// <summary>
    /// Remote value for this stage, or the local one when the key is absent. Negative remote values
    /// count as "not set" too, so a partially filled entry still falls back per field.
    /// </summary>
    public float GetLimitFastestTime(ShipRaceStageConfig stageConfig)
    {
        ShipRaceStageRemoteConfig remote = GetStageConfig(stageConfig.StageId);
        return remote == null || remote.LimitFastestTime < 0f
            ? stageConfig.LimitFastestTime
            : remote.LimitFastestTime;
    }

    public float GetLimitLongestTime(ShipRaceStageConfig stageConfig)
    {
        ShipRaceStageRemoteConfig remote = GetStageConfig(stageConfig.StageId);
        return remote == null || remote.LimitLongestTime < 0f
            ? stageConfig.LimitLongestTime
            : remote.LimitLongestTime;
    }

    public float GetWinChancePercent(ShipRaceStageConfig stageConfig)
    {
        ShipRaceStageRemoteConfig remote = GetStageConfig(stageConfig.StageId);
        return remote == null || remote.WinChancePercent < 0f
            ? stageConfig.WinChancePercent
            : remote.WinChancePercent;
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

/// <summary>
/// Remote override for one stage. -1 on any field means "leave the GlobalConfig value alone", so a
/// remote entry can tune a single number without having to restate the rest.
/// </summary>
[System.Serializable]
public class ShipRaceStageRemoteConfig
{
    public int StageId;
    public float LimitFastestTime = -1f;
    public float LimitLongestTime = -1f;
    public float WinChancePercent = -1f;
}
