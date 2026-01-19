using System;
using System.Collections.Generic;
using System.Globalization;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;
using UnityEngine;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
using CoreData;

[Serializable]
public class UserProfileDataSave : IDataSave<UserProfileDataSave>
{
    public static UserProfileDataSave Instance => InGameDataManager.Instance.InGameData.UserProfileDataSave;

    [field: SerializeField] public ReactiveValue<string> UserName { get; set; } = new("Player");
    [field: SerializeField] public ReactiveValue<string> PlayerID { get; set; } = new("");
    [field: SerializeField] public ReactiveValue<int> AvatarIndex { get; set; } = new(0);
    [field: SerializeField] public ReactiveValue<int> AvatarFrameIndex { get; set; } = new(0);
    [field: SerializeField] public ReactiveValue<int> BadgeIndex { get; set; } = new(-1);
    [field: SerializeField] public ReactiveValue<int> EffectNameID { get; set; } = new(0);
    [field: SerializeField] public ReactiveValue<int> PlayedLevel { get; set; } = new(0);
    [field: SerializeField] public ReactiveValue<int> Level { get; set; } = new(1);
    [field: SerializeField] public ReactiveValue<int> Retry { get; set; } = new(0);
    [field: SerializeField] public ReactiveValue<int> WinStreak { get; set; } = new(0);

    [field: SerializeField] public int TotalCoinEarn { get; set; }
    [field: SerializeField] public int TotalCoinRemain { get; set; }
    
    [field: SerializeField] public int TotalGemEarn { get; set; }
    [field: SerializeField] public int TotalGemRemain { get; set; }

    public bool IsDirty { get; set; }
    public UserProfileDataSave DefaultData()
    {
        return this;
    }
    [Button]
    public string ToJson()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new ReactiveValueContractResolver(),
            Converters = new JsonConverter[]
            {
                new ReactiveValueConverter()
            }
        };

        return JsonConvert.SerializeObject(this, settings);
    }

    public UserProfileDataSave FromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            ContractResolver = new ReactiveValueContractResolver(),
            Converters = new JsonConverter[]
            {
                new ReactiveValueConverter()
            }
        };

        return JsonConvert.DeserializeObject<UserProfileDataSave>(json, settings);
    }
    public string GetPlayerId()
    {
        return PlayerID.Value;
    }


    public void SetPlayerId(string id)
    {
        PlayerID.Value = id;
        this.SaveData();
    }

    public string GetUserName()
    {
        return UserName.Value;
    }
}

[Serializable]
public class CurrentEventData
{
    [field: SerializeField] public string CurrentEvent { get; set; }
    [field: SerializeField] public string StartDate { get; set; }
}

