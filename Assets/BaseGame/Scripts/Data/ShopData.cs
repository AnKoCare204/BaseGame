using TW.Reactive.CustomComponent;
using System;
using System.Collections.Generic;
using DataModule;
using Manager;
using UnityEngine;

namespace CoreData
{
    [Serializable]
    [DataGenerate(3)]
    public class ShopData : IDataModule
    {
        public static ShopData Instance => InGameDataManager.Instance.InGameData.ShopData;
        public string Key => nameof(ShopData);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;

        public bool RemoveADs = false;
        public Reactive<bool> IsRemoveADs = new(false);
        public List<string> PurchasedPacks = new List<string>();

        public void SetDefault()
        {
            RemoveADs = false;
            IsRemoveADs = new(false);
            PurchasedPacks = new List<string>();
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
            return DataSerializer.FromJson<ShopData>(json);
        }

        public bool IsPackPurchased(string packName)
        {
            return PurchasedPacks.Contains(packName);
        }
        public void AddPurchasedPack(string packName)
        {
            PurchasedPacks.Add(packName);
            this.SaveData();
        }
    }
}
