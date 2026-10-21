using System.Collections.Generic;
using UnityEngine;
using Sirenix.Utilities;
using CoreData;

[CreateAssetMenu(fileName = "TreetopGlobalConfig", menuName = "GlobalConfigs/TreetopGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class TreetopGlobalConfig : GlobalConfig<TreetopGlobalConfig>
{
    [field: SerializeField] public TreetopStreakConfig[] Streaks { get; set; }

    public TreetopStreakConfig GetConfigByLevel(int level)
    {
        foreach (var streak in Streaks)
        {
            if (streak.Level == level)
            {
                return streak;
            }
        }

        return null;
    }
    public TreetopStreakConfig GetLastConfigByLevel(int level)
    {
        for (var i = Streaks.Length -1 ; i > 0; i--)
        {
            if(Streaks[i].Level < level)
            {
                return Streaks[i];
            }
        }
        return Streaks[0];
    }

    public TreetopStreakConfig GetNextConfigByLevel(int level)
    {
        for (var i = 0; i < Streaks.Length; i++)
        {
            if(Streaks[i].Level > level)
            {
                return Streaks[i];
            }
        }
        return null;
    }

    public TreetopStreakConfig GetLastCheckpointByLevel(int level)
    {
        for (var i = Streaks.Length -1 ; i > 0; i--)
        {
            if(level >= Streaks[i].Level && Streaks[i].IsCheckPoint)
            {
                return Streaks[i];
            }
        }
        return Streaks[0];
    }
}

[System.Serializable]
public class TreetopStreakConfig
{
    public int Level;
    public List<GameResource> Rewards;
    public List<GameResource> DefaultRewards;
    public bool IsCheckPoint;

}