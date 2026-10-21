using System.Collections.Generic;
using Core.Tutorial;
using DataModule;
using Manager;
using UnityEngine;

[System.Serializable]
[DataGenerate(4)]
public class TutData : IDataModule
{
    public static TutData Instance => InGameDataManager.Instance.InGameData.TutData;
    public string Key => nameof(TutData);
    public int TargetVersion => 2;
    [field: SerializeField] public int Version { get; set; } = 1;
    public bool IsPassFirstTut = false;

    /// <summary>Id các bước FTUE đã xong (TutorialIds). Save v1 không có trường này: Migrate thêm ftue_l1_match nếu IsPassFirstTut.</summary>
    public List<string> DoneSteps = new List<string>();

    public void SetDefault()
    {
        IsPassFirstTut = false;
        DoneSteps = new List<string>();
    }

    /// <summary>
    /// v1 → v2: người đã qua tutorial cũ coi như xong bước L1. Không lưu ở đây (đang trong lúc load); lần lưu kế tiếp ghi v2,
    /// trước đó mỗi lần mở app chạy lại cũng vô hại.
    /// </summary>
    public void Migrate()
    {
        DoneSteps ??= new List<string>();
        if (IsPassFirstTut && !DoneSteps.Contains(TutorialIds.L1Match)) DoneSteps.Add(TutorialIds.L1Match);
        Version = TargetVersion;
    }

    public bool IsDone(string id) => DoneSteps != null && DoneSteps.Contains(id);

    /// <summary>Ghi một bước đã xong (bên gọi tự SaveData). Bước L1 bật luôn IsPassFirstTut (LoadingManager / GameManager còn đọc). false = đã có.</summary>
    public bool MarkDone(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        DoneSteps ??= new List<string>();
        if (DoneSteps.Contains(id)) return false;
        DoneSteps.Add(id);
        if (id == TutorialIds.L1Match) IsPassFirstTut = true;
        return true;
    }

    public string ToJson()
    {
        return DataSerializer.ToJson(this);
    }

    public IDataModule FromJson(string json)
    {
        return DataSerializer.FromJson<TutData>(json);
    }
}
