using System.Collections.Generic;
using CoreData;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ItemGroup
{
    [HorizontalGroup("ItemGroup", width: 110)]
    [ShowInInspector, PreviewField(height: 100), HideLabel]
    public Sprite Sprite => ItemGroupType.GetItemGroupSprite();
    
    [field: VerticalGroup("ItemGroup/Info")]
    [field: SerializeField] public ItemGroupType ItemGroupType { get; set; }
    [field: VerticalGroup("ItemGroup/Info")]
    [field: SerializeField] public List<GameResource> ItemList { get; set; } = new();



}

public enum ItemGroupType
{
    None = 0,
    Money = 1,
    Life = 2,
    
    MoneyGroup1 = 10,
    MoneyGroup2 = 11,
    MoneyGroup3 = 12,
    MoneyGroup4 = 13,
    
    NoAds = 20,
    Hint = 51,
    Undo = 52,
    Shuffle = 53,
    
    InfiniteLife = 102,
    BonusLife = 103,
    
    Chest1 = 104,
    Chest2 = 105,
    Chest3 = 106,
    
    CardPack_0 = 107,
    CardPack_1 = 108,
    CardPack_2 = 109,
    CardPack_3 = 110,
    CardPack_4 = 111,
    
}