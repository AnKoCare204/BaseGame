using TW.Reactive.CustomComponent;
using System.Collections.Generic;
using DataModule;
using GameEnum;
using Manager;
using R3;
using UnityEngine;

namespace CoreData
{
    [System.Serializable]
    [DataGenerate(1)]
    public class SettingData : IDataModule
    {
        public static SettingData Instance => InGameDataManager.Instance.InGameData.SettingData;
        public string Key => nameof(SettingData);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;
        [field: SerializeField] public List<SettingSubData> SettingSubDataList { get; set; } = new();
        [field: SerializeField] public Reactive<string> LanguageCode { get; private set; } = new("");

        public void SetDefault()
        {
            SettingSubDataList = new();
            LanguageCode = new("");
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
            return DataSerializer.FromJson<SettingData>(json);
        }

        public SettingSubData GetSettingSubData(SettingType type)
        {
            for (int i = 0; i < SettingSubDataList.Count; i++)
            {
                if (SettingSubDataList[i].Type == type)
                {
                    return SettingSubDataList[i];
                }
            }
            bool defaultValue = type switch
            {
                SettingType.Music => false,
                SettingType.Sound => true,
                SettingType.Vibration => true,
                SettingType.LightMode => true,
                _ => true
            };
            SettingSubData newSetting = new(type, defaultValue);
            SettingSubDataList.Add(newSetting);
            return newSetting;
        }
        public void SetSettingValue(SettingType type, bool value)
        {
            GetSettingSubData(type).Value.Value = value;
            this.SaveData();
        }

        public void SetLanguage(string code)
        {
            LanguageCode.Value = code;
            this.SaveData();
        }
    }

    [System.Serializable]
    public class SettingSubData
    {
        [field: SerializeField] public SettingType Type  { get; private set; }
        [field: SerializeField] public Reactive<bool> Value { get; private set; }

        public SettingSubData()
        {
        }

        public SettingSubData(SettingType type, bool value)
        {
            Type = type;
            Value = new(value);
        }
    }
}
