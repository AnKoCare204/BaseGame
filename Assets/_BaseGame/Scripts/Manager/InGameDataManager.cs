using System;
using CoreData;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

public class InGameDataManager : DDOLSingleton<InGameDataManager>
{
    [field: SerializeField] public InGameData InGameData { get; private set; } = new();
    private bool IsSavingData { get; set; } = false;
    private float SaveInterval { get; set; }= 5f;
    private float SaveTimer { get; set; } = 0f;
    protected override void Awake()
    {
        base.Awake();
        LoadData();
    }
    
    [Button]
    public void LoadData()
    {
        int dataVersion = PlayerPrefs.GetInt("DataVersion", 0);
        if (dataVersion == 1)
        {
            PlayerPrefs.SetInt("DataVersion", 1);
            InGameData.LoadData();
        }
        else
        {
            PlayerPrefs.SetInt("DataVersion", 1);
            InGameData.LoadDataFromFile();
        }
    }
    [Button]
    public void ClearData()
    {
        InGameData = new InGameData();
        InGameData.SaveAllData();
    }
}