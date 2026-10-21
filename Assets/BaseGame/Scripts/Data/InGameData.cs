using System;
using DataModule;
using UnityEngine;

namespace CoreData
{
    [Serializable]
    [DataStore]
    public partial class InGameData
    {
        public void SaveAllData()
        {
            SaveInternal();
        }

        public void LoadAllData()
        {
            LoadInternal();
            MigrateInternal();
        }
        public virtual void SaveData(IDataModule dataModule)
        {
            dataModule.SaveData();
        }
        public virtual T LoadData<T>() where T : IDataModule, new()
        {
            T data = new T();
            data = DataSerializer.LoadData<T>(data);
            if (data.Version < data.TargetVersion)
            {
                data.Migrate();
            }
            return data;
        }
    }
}
