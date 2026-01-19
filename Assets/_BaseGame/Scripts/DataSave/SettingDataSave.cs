using System.Collections.Generic;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class SettingDataSave : IDataSave<SettingDataSave>
{
    public static SettingDataSave Instance => InGameDataManager.Instance.InGameData.SettingDataSave;
    [field: SerializeField] public List<SettingData> SettingDatas { get; set; } = new();
    [field: SerializeField] public ReactiveValue<string> Language { get; set; }
    public bool IsDirty { get; set; }
    public SettingDataSave DefaultData()
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

    public SettingDataSave FromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new ReactiveValueContractResolver(),
            Converters = new JsonConverter[]
            {
                new ReactiveValueConverter()
            }
        };
        
        return JsonConvert.DeserializeObject<SettingDataSave>(json, settings);
    }

    public SettingData GetSettingData(SettingType type)
    {
        SettingData settingData = SettingDatas.Find(x => x.Type == type);
        if(settingData == null)
        {
            settingData = new SettingData(type, true);
            SettingDatas.Add(settingData);
        }
        return settingData;
    }
    
    public void SetSettingValue(SettingType type, bool value)
    {
        GetSettingData(type).Value.Value = value;
        this.SaveData();
    }
    public ReactiveValue<string> GetLanguage()
    {
        if (Language == null || string.IsNullOrEmpty(Language.Value))
        {
            SystemLanguage language = Application.systemLanguage;
            if (!LanguageGlobalConfig.Instance.listLanguage.Contains(language))
                language = SystemLanguage.English;
            Language = new(language.ToString());
        }
        return Language;
    }
    
    public void SetLanguage(string language)
    {
        if (Language == null || string.IsNullOrEmpty(Language.Value))
        {
            Language = new(language);
        }
        else
        {
            Language.Value = language;
        }
        this.SaveData();
    }


}

[System.Serializable]
public class SettingData
{
    public SettingType Type;
    [field: SerializeField] public ReactiveValue<bool> Value { get; private set; } = new(true);

    public SettingData(SettingType type, bool value)
    {
        Type = type;
        Value = new(value);
    }
}