using System;
using System.Collections.Generic;
using System.Linq;
using Core.Manager;
using CoreData;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Utility.Extension;
using UnityEngine;

[CreateAssetMenu(fileName = "FishModelGlobalConfig", menuName = "GlobalConfigs/FishModelGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class FishModelGlobalConfig : GlobalConfig<FishModelGlobalConfig>
{
    [field: SerializeField] private FishModelConfig[] DefaultFishModelConfigs {get; set;}
    [field: SerializeField] private FishModel[] RegularFishModelConfigs {get; set;}
    [field: SerializeField] private FishModel[] SpecialFishModelConfigs {get; set;}
    [ShowInInspector]
    private List<FishModelConfig> ShuffleFishModelConfigList {get; set;} = new();
    public int Count => FullFishModelConfigList?.Count ?? 0;
    [ShowInInspector] 
    private List<FishModelConfig> FullFishModelConfigList => GetFullFishModelConfigList();
    /// <summary>
    /// Danh sách ID loài có model (dùng cho tool sinh / import level). Bỏ ID âm: đó là model vật cản dùng chung
    /// (cá ẩn = FishEntry.HiddenModelId), không phải loài cá.
    /// </summary>
    public int[] GetAllIds()
    {
        if (FullFishModelConfigList == null) return Array.Empty<int>();
        var ids = new List<int>(FullFishModelConfigList.Count);
        foreach (FishModelConfig config in FullFishModelConfigList)
        {
            if (config is { ID: >= 0 }) ids.Add(config.ID);
        }

        return ids.ToArray();
    }

    /// <summary>
    /// Bảng đang dùng để tra: danh sách đã xáo nếu ShuffleFishModelConfigs() đã chạy, không thì bảng gốc.
    /// ShuffleFishModelConfigs chỉ chạy lúc vào màn (Level.cs), nên ngoài trận - tool trong Editor, hay bất kỳ
    /// code nào chạy trước khi Level bắt đầu - danh sách xáo còn rỗng và phải lùi về bảng gốc.
    /// </summary>
    private IReadOnlyList<FishModelConfig> ActiveConfigs
    {
        get
        {
            if (ShuffleFishModelConfigList is { Count: > 0 }) return ShuffleFishModelConfigList;
            return FullFishModelConfigList ?? (IReadOnlyList<FishModelConfig>)Array.Empty<FishModelConfig>();
        }
    }

    public FishModelConfig GetFishModelConfig(int index)
    {
        IReadOnlyList<FishModelConfig> configs = ActiveConfigs;
        if (configs.Count == 0) return null;
        return index >= 0 && index < configs.Count ? configs[index] : configs[0];
    }

    /// <summary>
    /// Tìm theo trường ID (FishData.FishId hoặc FishEntry.ModelId). ID âm là model vật cản: phải có entry đúng ID, không có thì
    /// báo lỗi và trả null (không fallback theo index). ID ≥ 0 không có thì thử coi id là index, cuối cùng lấy config đầu.
    /// </summary>
    public FishModelConfig GetFishModelConfigById(int id)
    {
        // Duyệt theo đúng chiều dài của bảng đang tra. Bản cũ duyệt theo FishModelConfigs.Length nhưng index vào
        // ShuffleFishModelConfigList: ngoài trận danh sách đó rỗng nên lời gọi nào cũng ném IndexOutOfRange.
        IReadOnlyList<FishModelConfig> configs = ActiveConfigs;
        for (int i = 0; i < configs.Count; i++)
        {
            if (configs[i] != null && configs[i].ID == id) return configs[i];
        }

        if (id < 0)
        {
            Debug.LogError($"FishModelGlobalConfig: chưa có entry ID {id} (model cá ẩn = {FishEntry.HiddenModelId}). Thêm vào FishModelConfigs.", this);
            return null;
        }

        return GetFishModelConfig(id);
    }

    public void ShuffleFishModelConfigs()
    {
        ShuffleFishModelConfigList.Clear();
        HashSet<int> idList = new HashSet<int>();
        List<FishModel> modalList = new List<FishModel>();
        foreach (FishModelConfig config in DefaultFishModelConfigs)
        {
            ShuffleFishModelConfigList.Add(config);
        }

        int startIndex = 0;
        for (int i = 0; i < RegularFishModelConfigs.Length; i++)
        {
            int id = startIndex;
            while (!idList.Add(id))
            {
                id++;
            }
            modalList.Add(RegularFishModelConfigs[i]);
        }
        FishModel[] shuffleSpecialFishModelConfigs = SpecialFishModelConfigs.Shuffle().ToArray();
        for (int i = 0; i < shuffleSpecialFishModelConfigs.Length/3; i++)
        {
            int id = startIndex;
            while (!idList.Add(id))
            {
                id++;
            }
            modalList.Add(shuffleSpecialFishModelConfigs[i]);
        }
        if (UserData.Instance.CurrentLevel > 10)
        {
            modalList = modalList.Shuffle().ToList();
        }
        int[] idListArray = idList.ToArray();
        for (int i = 0; i < modalList.Count; i++)
        {
            ShuffleFishModelConfigList.Add(new FishModelConfig { ID = idListArray[i], FishModel = modalList[i] });
        }
    }
    
    public List<FishModel> GetPickableFishModels()
    {
        var models = new List<FishModel>();
        if (RegularFishModelConfigs != null) models.AddRange(RegularFishModelConfigs);
        if (SpecialFishModelConfigs != null) models.AddRange(SpecialFishModelConfigs);
        models.RemoveAll(model => model == null);
        return models;
    }
    
    public void ApplyPriorityModels(IReadOnlyList<int> orderQueue, IReadOnlyList<FishModel> preferred)
    {
        if (preferred == null || preferred.Count == 0) return;
        FishModelPriority.ApplyShuffled(ShuffleFishModelConfigList, orderQueue, preferred, new System.Random());
    }
    
    public List<FishModelConfig> GetFullFishModelConfigList()
    {
        List<FishModelConfig> list = new List<FishModelConfig>(DefaultFishModelConfigs.Length);
        HashSet<int> idList = new HashSet<int>();
        List<FishModel> modalList = new List<FishModel>();
        foreach (FishModelConfig config in DefaultFishModelConfigs)
        {
            list.Add(config);
        }

        int startIndex = 0;
        for (int i = 0; i < RegularFishModelConfigs.Length; i++)
        {
            int id = startIndex;
            while (!idList.Add(id))
            {
                id++;
            }
            modalList.Add(RegularFishModelConfigs[i]);
        }
        // FishModel[] shuffleSpecialFishModelConfigs = SpecialFishModelConfigs.Shuffle().ToArray();
        // for (int i = 0; i < shuffleSpecialFishModelConfigs.Length/3; i++)
        // {
        //     int id = startIndex;
        //     while (!idList.Add(id))
        //     {
        //         id++;
        //     }
        //     modalList.Add(shuffleSpecialFishModelConfigs[i]);
        // }
        int[] idListArray = idList.ToArray();
        for (int i = 0; i < modalList.Count; i++)
        {
            list.Add(new FishModelConfig { ID = idListArray[i], FishModel = modalList[i] });
        }

        return list;
    }
}
[Serializable]
public class FishModelConfig
{
    public int ID;
    public FishModel FishModel;
}