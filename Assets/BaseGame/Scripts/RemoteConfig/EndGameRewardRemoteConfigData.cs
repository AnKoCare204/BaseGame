using Core.GamePlay;
using SDK;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>Thưởng coin khi thắng theo độ khó: Regular 20 / Hard 60 / SuperHard 100, x2 bằng ads từ level 5 (GDD).</summary>
[Serializable]
public class EndGameRewardRemoteConfigData : IRemoteConfigData
{
    public static EndGameRewardRemoteConfigData Instance => RemoteConfigManager.Instance.EndGameRewardRemoteConfigData;

    [Serializable]
    public class RewardConfig
    {
        public string LevelDifficultyName;
        public int RewardCoin;
    }

    public KeyRemoteConfig KeyRemoteConfig => KeyRemoteConfig.endgame_reward_config;
    public RewardConfig[] RewardConfigs;
    public int HasX2RewardAds = 1;
    public int StartLevelX2Reward = 5;
    public int AdsMultiplier = 2;

    public EndGameRewardRemoteConfigData()
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
        RewardConfigs = new[]
        {
            new RewardConfig { LevelDifficultyName = nameof(LevelDifficulty.Normal), RewardCoin = 20 },
            new RewardConfig { LevelDifficultyName = nameof(LevelDifficulty.Hard), RewardCoin = 60 },
            new RewardConfig { LevelDifficultyName = nameof(LevelDifficulty.SuperHard), RewardCoin = 100 },
        };
        HasX2RewardAds = 0;
        StartLevelX2Reward = 5;
        AdsMultiplier = 2;
    }

    [Button]
    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        EndGameRewardRemoteConfigData data = JsonUtility.FromJson<EndGameRewardRemoteConfigData>(json);
        if (data == null) return;
        if (data.RewardConfigs != null && data.RewardConfigs.Length > 0) RewardConfigs = data.RewardConfigs;
        HasX2RewardAds = data.HasX2RewardAds;
        StartLevelX2Reward = data.StartLevelX2Reward;
    }

    public int GetRewardCoin(LevelDifficulty levelDifficulty)
    {
        string name = levelDifficulty.ToString();
        if (RewardConfigs != null)
        {
            for (int i = 0; i < RewardConfigs.Length; i++)
            {
                if (RewardConfigs[i].LevelDifficultyName == name) return RewardConfigs[i].RewardCoin;
            }
        }

        return 20;
    }

    public int GetAdsRewardCoin(LevelDifficulty levelDifficulty) => GetRewardCoin(levelDifficulty) * Mathf.Max(1, AdsMultiplier);

    public bool CanShowX2Reward(int completedLevel) => HasX2RewardAds == 1 && completedLevel >= StartLevelX2Reward;
}
