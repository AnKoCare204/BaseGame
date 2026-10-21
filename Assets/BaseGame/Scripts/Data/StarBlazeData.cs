using System;
using DataModule;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using Manager;

[Serializable]
[DataGenerate(10)]
public class StarBlazeData : IDataModule
{
    public static StarBlazeData Instance => InGameDataManager.Instance.InGameData.StarBlazeData;
    public string Key => nameof(StarBlazeData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;
    [field: SerializeField] public int LastWinStreak { get; set; } = 0;
    [field: SerializeField] public int CurrentWinStreak { get; set; } = 0;
    [field: SerializeField] public int CurrentPoint { get; set; } = 0;
    [field: SerializeField] public int WillAddPoint { get; set; } = 0;
    [field: SerializeField] public bool IsStartEvent { get; set; } = false;
    [field: SerializeField] public bool IsDoublePoint { get; set; } = false;
    [field: SerializeField] public string LastStartStarBlazeTime { get; set; } = string.Empty;
    [field: SerializeField] public string LastStartDoublePointTime { get; set; } = string.Empty;
    [field: SerializeField] public bool IsShowTut { get; set; } = false;
    
    public void SetDefault()
    {
        CurrentWinStreak = 0;
        CurrentPoint = 0;
        IsStartEvent = false;
        IsDoublePoint = false;
        LastStartStarBlazeTime = string.Empty;
        LastStartDoublePointTime = string.Empty;
        IsShowTut = false;
    }
    [Button]
    public void ResetDataEvent()
    {
        CurrentWinStreak = 0;
        CurrentPoint = 0;
        IsStartEvent = false;
        IsDoublePoint = false;
        IsShowTut = false;
        this.SaveData();
    }

    public void Migrate()
    {

    }
    
    public string ToJson()
    {
        var settings = new JsonSerializerSettings
        {
            // Formatting = Formatting.Indented,
            // ContractResolver = new ReactiveValueContractResolver(),
            // Converters = new JsonConverter[]
            // {
            //     new ReactiveValueConverter()
            // }
        };

        return JsonConvert.SerializeObject(this, settings);
    }

    public IDataModule FromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            // ObjectCreationHandling = ObjectCreationHandling.Replace,
            // ContractResolver = new ReactiveValueContractResolver(),
            // Converters = new JsonConverter[]
            // {
            //     new ReactiveValueConverter()
            // }
        };

        return JsonConvert.DeserializeObject<IDataModule>(json, settings);
    }
}