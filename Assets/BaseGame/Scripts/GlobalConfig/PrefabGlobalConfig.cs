using Sirenix.Utilities;
using UnityEngine;

/// <summary>Prefab gameplay dùng chung. LevelManager sinh levelPrefab (template: SampleLevel) vào LevelContainer.</summary>
[CreateAssetMenu(fileName = "PrefabGlobalConfig", menuName = "GlobalConfigs/PrefabGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class PrefabGlobalConfig : GlobalConfig<PrefabGlobalConfig>
{
    public Level levelPrefab;
}
