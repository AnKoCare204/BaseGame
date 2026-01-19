using CoreData;
using Manager;
using SDK;
using UnityEngine;

public interface IDataSave<out T> where T : class, IDataSave<T>
{
    bool IsDirty { get; set; }
    T DefaultData();
    string ToJson();
    T FromJson(string json);
}

public static class DataSaveExtensions
{
    public static void SaveData<T>(this T dataSave) where T : class, IDataSave<T>
    {
        EventManager.AddEventNextFrame(() => SaveDataImmediate(dataSave));
    }

    private static void SaveDataImmediate<T>(this T dataSave) where T : class, IDataSave<T>
    {
        // #if UNITY_EDITOR
        // Debug.Log("Save Data: " + typeof(T).Name + " - " + JsonUtility.ToJson(dataSave, true));
        // #endif
        DataSerializer.SaveDataPrefs(dataSave);
    }
}