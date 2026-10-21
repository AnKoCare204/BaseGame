using Core.GamePlay.GlobalEnum;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFeatureGlobalConfig", menuName = "GlobalConfigs/NewFeatureGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class NewFeatureGlobalConfig : GlobalConfig<NewFeatureGlobalConfig>
{
    [field: SerializeField] public NewFeatureData[] NewFeatureDatas { get; private set; }

    // Lookup by obstacle kind (used when a level is detected to contain an obstacle).
    public NewFeatureData GetNewFeatureDataByType(ObstacleFeatureType featureType)
    {
        if (featureType == ObstacleFeatureType.None) return null;
        for (int i = 0; i < NewFeatureDatas.Length; i++)
        {
            if (NewFeatureDatas[i].FeatureType == featureType)
            {
                return NewFeatureDatas[i];
            }
        }
        return null;
    }

    // Lookup by stable id (used by ModalNewObstacle to display the correct entry).
    public NewFeatureData GetNewFeatureDataById(string featureId)
    {
        if (string.IsNullOrEmpty(featureId)) return null;
        for (int i = 0; i < NewFeatureDatas.Length; i++)
        {
            if (NewFeatureDatas[i].FeatureId == featureId)
            {
                return NewFeatureDatas[i];
            }
        }
        return null;
    }
}

[System.Serializable]
public class NewFeatureData
{
    public string FeatureName;
    public string FeatureDes;
    // Which obstacle kind this entry describes. Set per-entry in the config asset.
    // The intro popup is driven entirely by this (actual presence in the level).
    public ObstacleFeatureType FeatureType = ObstacleFeatureType.None;
    [PreviewField] public Sprite FeatureIcon;

    // Stable, save-friendly id for the "already seen" record.
    // Falls back to FeatureName if the type is unset (backward compatible).
    public string FeatureId =>
        FeatureType != ObstacleFeatureType.None ? FeatureType.ToString() : FeatureName;
}
