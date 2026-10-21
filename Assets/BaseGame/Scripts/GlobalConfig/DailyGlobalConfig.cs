using CoreData;
using Sirenix.Utilities;
using UnityEngine;

namespace CoreGame.GameGlobalConfig
{
    [CreateAssetMenu(fileName = "DailyGlobalConfig", menuName = "GlobalConfigs/DailyGlobalConfig")]
    [GlobalConfig("Assets/Resources/GlobalConfig/")]
    public class DailyGlobalConfig : GlobalConfig<DailyGlobalConfig>
    {
        public DailyResourceConfig[] DailyResourceConfigs;

        public DailyResourceConfig GetDailyResourceConfig(int index)
        {
            index = Mathf.Max(0, index);
            int day = index % DailyResourceConfigs.Length;
            foreach (var dailyResourceConfig in DailyResourceConfigs)
            {
                if (dailyResourceConfig.Day == day)
                {
                    return dailyResourceConfig;
                }
            }
            return null;
        }
    }

    [System.Serializable]
    public class DailyResourceConfig
    {
        public int Day;
        public GameResource[] dailyRewardData;
        // public GameResource[] collectionDailyRewardData;

        public GameResource GetGameResource(int index)
        {
            if (index >= dailyRewardData.Length || index < 0)
                return null;
            // if (CardCollectionManager.Instance.IsActiveForReward())
            // {
            //     return collectionDailyRewardData[index];
            // }
            return dailyRewardData[index];
        }
    }
}