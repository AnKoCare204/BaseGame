using UnityEngine;

namespace DataModule
{
    public interface IDataModule
    {
        string Key { get; }
        int TargetVersion { get; }
        int Version { get; }
        void SetDefault();
        void Migrate();
        string ToJson();
        IDataModule FromJson(string json);
    }

    public static class DataModuleExtensions
    {
        public static void SaveData<T>(this T dataSave) where T : class, IDataModule
        {
#if UNITY_EDITOR
            Debug.Log($"Saving data module: {dataSave.Key} v{dataSave.Version}");
#endif
            EventManager.AddEventNextFrame(dataSave.SaveDataImmediate);
        }

        private static void SaveDataImmediate<T>(this T dataSave) where T : IDataModule
        {
            DataSerializer.SaveData(dataSave);
        }
    }
}
