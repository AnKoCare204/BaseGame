using System.Collections.Generic;
using DataModule;
using Manager;
using UnityEngine;

[System.Serializable]
[DataGenerate(7)]
public class ObstacleTutorialData : IDataModule
{
    public static ObstacleTutorialData Instance => InGameDataManager.Instance.InGameData.ObstacleTutorialData;
    public string Key => nameof(ObstacleTutorialData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;

    // Stores obstacle feature ids that have been introduced to the player.
    public List<string> SeenObstacleFeatures = new List<string>();
    // One-time import of the old PlayerPrefs "Obstacle_{FeatureName}" flags.
    public bool LegacyPrefsImported = false;

    public void SetDefault()
    {
        SeenObstacleFeatures = new List<string>();
        LegacyPrefsImported = false;
    }

    public void Migrate()
    {
    }

    public string ToJson()
    {
        return DataSerializer.ToJson(this);
    }

    public IDataModule FromJson(string json)
    {
        return DataSerializer.FromJson<ObstacleTutorialData>(json);
    }

    public bool IsSeen(string featureId)
    {
        // Null-guard: an old save that predates this field may deserialize the list as null.
        if (SeenObstacleFeatures == null) SeenObstacleFeatures = new List<string>();
        if (string.IsNullOrEmpty(featureId)) return true; // unknown feature -> never show
        return SeenObstacleFeatures.Contains(featureId);
    }

    public void MarkSeen(string featureId)
    {
        if (string.IsNullOrEmpty(featureId)) return;
        if (SeenObstacleFeatures == null) SeenObstacleFeatures = new List<string>();
        if (SeenObstacleFeatures.Contains(featureId)) return;
        SeenObstacleFeatures.Add(featureId);
        this.SaveData();
    }

    // Players from before this system saw obstacle intros tracked via
    // PlayerPrefs "Obstacle_{FeatureName}" (old NewFeatureGlobalConfig flow).
    // Import those flags once so they are never re-introduced.
    // Returns true if an import ran (caller should save).
    public bool TryImportLegacyPrefs()
    {
        if (LegacyPrefsImported) return false;
        LegacyPrefsImported = true;
        if (SeenObstacleFeatures == null) SeenObstacleFeatures = new List<string>();
        NewFeatureData[] configs = NewFeatureGlobalConfig.Instance.NewFeatureDatas;
        if (configs != null)
        {
            for (int i = 0; i < configs.Length; i++)
            {
                NewFeatureData feature = configs[i];
                if (feature == null) continue;
                if (PlayerPrefs.GetInt($"Obstacle_{feature.FeatureName}", 0) != 1) continue;
                if (SeenObstacleFeatures.Contains(feature.FeatureId)) continue;
                SeenObstacleFeatures.Add(feature.FeatureId);
            }
        }
        return true;
    }
}
