using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Manager
{
    /// <summary>Kho dữ liệu lưu (khung AntFood): mỗi module [DataGenerate] tự load/save theo key PlayerPrefs riêng.</summary>
    public class InGameDataManager : Singleton<InGameDataManager>
    {
        [field: SerializeField] public InGameData InGameData { get; private set; } = new();

        public static bool IsDataLoaded { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            LoadData();
        }

        [Button]
        public void LoadData()
        {
            InGameData.LoadAllData();
            IsDataLoaded = true;
            if (InGameData.ResourceData.ApplyDataMigrations())
            {
                InGameData.ResourceData.SaveData();
            }

            UserData.Instance.IncreaseSessionCount();
            if (RemoteConfigManager.Instance != null)
            {
                RemoteConfigManager.Instance.TryApplyStartGoldRemoteConfig().Forget();
            }
        }

        [Button]
        public void SaveAllData()
        {
            InGameData.SaveAllData();
        }

        [Button]
        public void ResetData()
        {
            PlayerPrefs.DeleteAll();
            InGameData = new InGameData();
            InGameData.LoadAllData();
            InGameData.SaveAllData();
        }
    }
}
