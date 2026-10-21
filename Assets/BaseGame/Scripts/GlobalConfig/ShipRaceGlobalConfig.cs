using System.Collections.Generic;
using CoreData;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "ShipRaceGlobalConfig", menuName = "GlobalConfigs/ShipRaceGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ShipRaceGlobalConfig : GlobalConfig<ShipRaceGlobalConfig>
{
    [Title("Race")]
    public ShipRaceStageConfig[] Stages =
    {
        new ShipRaceStageConfig { StageId = 0, Target = 3, TotalRacers = 5, TimePassLvMin = 500, TimePassLvMax = 700 },
        new ShipRaceStageConfig { StageId = 1, Target = 5, TotalRacers = 5, TimePassLvMin = 450, TimePassLvMax = 600 },
        new ShipRaceStageConfig { StageId = 2, Target = 7, TotalRacers = 5, TimePassLvMin = 400, TimePassLvMax = 500 },
    };

    [Title("Loop")]
    [Tooltip("How many times the 3 stage circuit can be completed in one run. 1 = the event closes " +
             "after the first full clear, 2+ = it loops back to stage 0 that many times")]
    [Min(1)]
    public int MaxCompleteLoop = 1;

    [Title("Run cycle")]
    [Tooltip("Seconds a single run lasts before it expires and a fresh one opens (86400 = 1 day)")]
    public float ExistTime = 86400f;

    [Title("Bot names")]
    [Tooltip("Codenames are built as Prefix + Suffix, so 25 x 25 entries already gives 625 unique names")]
    public string[] BotNamePrefixes =
    {
        "Nova", "Shadow", "Quantum", "Astro", "Cyber", "Turbo", "Solar", "Lunar", "Neon", "Blaze",
        "Frost", "Storm", "Iron", "Ghost", "Rapid", "Hyper", "Star", "Void", "Delta", "Omega",
        "Vortex", "Titan", "Pulse", "Zero", "Echo",
    };

    public string[] BotNameSuffixes =
    {
        "Rex", "Pulse", "Rider", "Blade", "Wing", "Runner", "Dash", "Flare", "Comet", "Drift",
        "Hawk", "Bolt", "Fang", "Racer", "Nova", "Surge", "Chaser", "Streak", "Falcon", "Spark",
        "Jet", "Wave", "Claw", "Hunter", "Zoom",
    };

    [Tooltip("Optional explicit pool. When not empty it is used instead of Prefix + Suffix")]
    public string[] BotNamePool;

    [Tooltip("How many names of the generated pool a single stage can draw from")]
    public int BotPoolSizePerStage = 100;

    public ShipRaceStageConfig GetStageConfig(int stageId)
    {
        foreach (ShipRaceStageConfig stage in Stages)
        {
            if (stage.StageId == stageId) return stage;
        }

        return null;
    }

    public int LastStageId => Stages.Length > 0 ? Stages[^1].StageId : 0;
    public int StageCount => Stages.Length;

    /// <summary>
    /// Full codename pool. Deterministic: same config always produces the same list in the same
    /// order, so a bot saved by index keeps its name across sessions.
    /// </summary>
    public List<string> GetNamePool()
    {
        List<string> pool = new List<string>();
        if (BotNamePool != null && BotNamePool.Length > 0)
        {
            pool.AddRange(BotNamePool);
            return pool;
        }

        if (BotNamePrefixes == null || BotNameSuffixes == null) return pool;
        foreach (string prefix in BotNamePrefixes)
        {
            foreach (string suffix in BotNameSuffixes)
            {
                // Skip silly duplicates like "NovaNova"
                if (prefix == suffix) continue;
                pool.Add(prefix + suffix);
            }
        }

        return pool;
    }
}

[System.Serializable]
public class ShipRaceStageConfig
{
    public int StageId;
    [Tooltip("Points (levels) needed to finish this stage")]
    public int Target = 3;
    [Tooltip("Player + bots. 5 means the player races 4 bots")]
    public int TotalRacers = 5;
    [Tooltip("Fastest bot pace, seconds per level")]
    public float TimePassLvMin = 500;
    [Tooltip("Slowest bot pace, seconds per level")]
    public float TimePassLvMax = 700;
    [Title("Result window")]
    [Tooltip("Finish the stage faster than this (seconds) and the win is guaranteed")]
    public float LimitFastestTime = 1800f;

    [Tooltip("Stage deadline. The rival is paced to reach the target exactly here, and the stage is lost when it does")]
    public float LimitLongestTime = 7200f;

    [Tooltip("Win chance (%) used when the finish lands between the two limits. 40 means 40% to win")]
    [Range(0f, 100f)]
    public float WinChancePercent = 40f;

    [Title("Rival bot")]
    [Tooltip("Gap band kept against the player: x = furthest it may fall behind, y = furthest it may pull ahead")]
    public Vector2Int RivalOffsetRange = new Vector2Int(-1, 1);

    [Title("Filler bots")]
    [Tooltip("Chance (0-1) that a filler bot fails a level instead of passing it")]
    [Range(0f, 1f)]
    public float BotLoseChance = 0.15f;

    [Tooltip("Points a filler bot loses when it fails. 0 wipes it back to zero, like the player")]
    public int BotLosePoint = 0;

    [Title("Rewards")]
    [Tooltip("Granted when the player wins this stage while a card collection event is running")]
    public ClaimGameResourceGroup Rewards = new ClaimGameResourceGroup();

    [Tooltip("Granted when the player wins this stage and no collection event is running")]
    public ClaimGameResourceGroup DefaultRewards = new ClaimGameResourceGroup();

    public int BotCount => Mathf.Max(0, TotalRacers - 1);
}
