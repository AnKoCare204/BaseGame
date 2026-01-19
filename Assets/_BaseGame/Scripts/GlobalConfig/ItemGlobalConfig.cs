using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Manager;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Utility.Extension;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[CreateAssetMenu(fileName = "ItemGlobalConfig", menuName = "GlobalConfigs/ItemGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ItemGlobalConfig : GlobalConfig<ItemGlobalConfig>
{
    public List<CurrencyData> CurrencyDatas;
    public List<SpecialItemData> SpecialItemDatas;
    
    public Sprite GetItemSprite(CurrencyType currencyType)
    {
        return CurrencyDatas.Find(x => x.CurrencyType == currencyType).Sprite;
    }
    public Sprite GetItemSprite(SpecialResourceType specialResourceType)
    {
        return SpecialItemDatas.Find(x => x.SpecialResourceType == specialResourceType).Sprite;
    }
}

[System.Serializable]
public class CurrencyData
{
    public CurrencyType CurrencyType;
    [PreviewField] public Sprite Sprite;
}

[System.Serializable]
public class SpecialItemData
{
    public SpecialResourceType SpecialResourceType;
    [PreviewField] public Sprite Sprite;
}