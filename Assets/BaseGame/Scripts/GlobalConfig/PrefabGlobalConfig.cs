using Sirenix.Utilities;
using UnityEngine;

/// <summary>Prefab gameplay dùng chung (khung AntFood). LevelManager sinh levelPrefab rồi Level sinh bóng (BubbleView) và cá.</summary>
[CreateAssetMenu(fileName = "PrefabGlobalConfig", menuName = "GlobalConfigs/PrefabGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class PrefabGlobalConfig : GlobalConfig<PrefabGlobalConfig>
{
    public Level levelPrefab;
    public BubbleView bubblePrefab;
    public Fish fishPrefab;
    public UITreetopCheckpoint UITreetopCheckpointPrefab;
}
