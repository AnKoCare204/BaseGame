using System;
using System.Collections.Generic;
using Core;
using Core.GamePlay;
using CoreData;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemGlobalConfig", menuName = "GlobalConfigs/ItemGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ItemGlobalConfig : GlobalConfig<ItemGlobalConfig>
{
    public List<ResourcesData> ResourceDatas;
    public List<BoosterInfoData> BooosterInfoDatas;
    public List<LevelDifficultyData> LevelDifficultyDatas;

    public Sprite GetItemSprite(ResourceType resourceType)
    {
        for (int i = 0; i < ResourceDatas.Count; i++)
        {
            if (ResourceDatas[i].ResourceType == resourceType)
            {
                return ResourceDatas[i].Sprite;
            }
        }
        return null;
    }

    public BoosterInfoData GetBoosterData(ResourceType resourceType)
    {
        for (int i = 0; i < BooosterInfoDatas.Count; i++)
        {
            if (BooosterInfoDatas[i].ResourceType == resourceType)
            {
                return BooosterInfoDatas[i];
            }
        }
        return null;
    }

    public int GetBoosterPriceX3(ResourceType resourceType)
    {
        return BoosterRemoteConfigData.Instance.GetBoosterPriceX3(resourceType);
    }

    /// <summary>Đã nhận booster qua luồng mở khóa (ScreenInGame.ClaimBooster ghi PlayerPrefs "Booster_&lt;Type&gt;" = 1).</summary>
    public bool IsBoosterClaimed(ResourceType resourceType) => PlayerPrefs.GetInt($"Booster_{resourceType}", 0) == 1;

    /// <summary>Booster cần chạy luồng nhận lúc vào level này ("Booster_&lt;Type&gt;"), rỗng nếu không có. Luật ở BoosterUnlockRules.</summary>
    public string GetNewBoosterUnlock(int level)
    {
        for (var i = 0; i < BooosterInfoDatas.Count; i++)
        {
            BoosterInfoData data = BooosterInfoDatas[i];
            if (BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(data, level, IsBoosterClaimed(data.ResourceType)))
                return $"Booster_{data.ResourceType}";
        }

        return string.Empty;
    }

    public bool IsNeedShowBoosterUnlock(int level) => !string.IsNullOrEmpty(GetNewBoosterUnlock(level));
    public void ShowBoosterUnlock(ResourceType boosterType)
    {
       string boosterName = $"Booster_{boosterType}";
        int isShown = PlayerPrefs.GetInt(boosterName, 0);
        if (isShown != 1)
        {
            PlayerPrefs.SetInt(boosterName, 1);
        }
    }
    public void FetchRemote()
    {
        
    }
    
    public LevelDifficultyData GetLevelDifficultyData(LevelDifficulty type)
    {
        for (int i = 0; i < LevelDifficultyDatas.Count; i++)
        {
            if (LevelDifficultyDatas[i].Type == type)
            {
                return LevelDifficultyDatas[i];
            }
        }
        return null;
    }
}

[System.Serializable]
public class ResourcesData
{
    public ResourceType ResourceType;
    [PreviewField] public Sprite Sprite;
}

[System.Serializable]
public class BoosterInfoData
{
    public ResourceType ResourceType;
    public string Name;
    public string Description;
    public int LevelUnlock;
    [Tooltip("Số booster tặng khi mở khóa (GDD 08_Boosters: Hint 3, Magnet 2, Shuffle 2, ExtraSlot 1, Swap 2; Undo đã bỏ)")]
    public int UnlockGiftAmount = 1;
    [Tooltip("Ép dùng thử khi mở khóa: số cá tự bấm lên ô chờ trước khi ép bấm (PackedUp cần cá trong ô chờ mới có tác dụng); 0 = không bấm trước. Luôn chừa ít nhất một ô chờ trống.")]
    public int ForceUsePrefillCount;
    public string MessageNoUse;
    [Tooltip("Thông báo riêng khi không dùng được vì ô chờ đầy (Shuffle); trống = dùng MessageNoUse")]
    public string MessageHoldingFull;
    [PreviewField] public Sprite Sprite;
    public int Price;
    public int PriceX3;
    [Tooltip("Mở khóa giữa level LevelUnlock bằng một kịch bản gọi LevelManager.RunBoosterUnlockFlowAsync, không mở lúc vào level; level sau mà chưa nhận thì mở lúc vào level như cũ. Nút chỉ mở khi đã nhận. Hiện không booster nào dùng.")]
    public bool UnlockMidLevel;
}

[Serializable]
public class LevelDifficultyData
{
    public LevelDifficulty Type;
    public AlertLevelDifficultyData AlertLevelDifficultyData;
}

[Serializable]
public class AlertLevelDifficultyData
{ 
    [PreviewField] public Sprite LineTopAlertSprite;
    [PreviewField] public Sprite LineBottomAlertSprite;
    [PreviewField] public Sprite WarningAlertSprite;
    [PreviewField] public Sprite BodySprite;
    public string LevelDifficultyName;
    public Material LevelDifficultyColor;
}