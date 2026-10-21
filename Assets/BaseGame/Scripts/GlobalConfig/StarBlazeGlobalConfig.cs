using System;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "StarBlazeGlobalConfig", menuName = "GlobalConfigs/StarBlazeGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class StarBlazeGlobalConfig : GlobalConfig<StarBlazeGlobalConfig>
{
    public StarBlazeConfig StarBlazeConfig;
}
[Serializable]
public class StarBlazeConfig
{
    public StarBlazeStageConfig[] StageConfigs;
}

[Serializable]
public class StarBlazeStageConfig
{
    public int Index;
#if UNITY_ANDROID
    [ShowInInspector] public int Point => AndroidPoint;
#elif UNITY_IOS
    [ShowInInspector] public int Point => IOSPoint;
#endif
    public int AndroidPoint;
    public int IOSPoint;
    public ItemGroup Reward;
    public ItemGroup DefaultReward;
}