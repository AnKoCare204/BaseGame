using DataModule;
using Manager;
using UnityEngine;

[System.Serializable]
[DataGenerate(6)]
public class RateData : IDataModule
{
    public static RateData Instance => InGameDataManager.Instance.InGameData.RateData;
    public string Key => nameof(RateData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;
    public bool IsShowRate = false;

    public void SetDefault()
    {
        IsShowRate = false;
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
        return DataSerializer.FromJson<RateData>(json);
    }
}
