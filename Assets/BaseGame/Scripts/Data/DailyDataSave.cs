using TW.Reactive.CustomComponent;
using System;
using DataModule;
using Manager;
using UnityEngine;

namespace CoreData
{
    [Serializable]
    [DataGenerate(5)]
    public class DailyDataSave : IDataModule
    {
        public static DailyDataSave Instance => InGameDataManager.Instance.InGameData.DailyDataSave;
        public string Key => nameof(DailyDataSave);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;
        [field: SerializeField] public Reactive<string> TimeDailyReset { get; set; } = new("");
        [field: SerializeField] public Reactive<int> DailyRewardClaimCount { get; set; } = new(0);
        [field: SerializeField] public Reactive<int> DayClaimed { get; set; } = new(-1);

        public void SetDefault()
        {
            TimeDailyReset = new("");
            DailyRewardClaimCount = new(0);
            DayClaimed = new(-1);
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
            return DataSerializer.FromJson<DailyDataSave>(json);
        }
    }
}
