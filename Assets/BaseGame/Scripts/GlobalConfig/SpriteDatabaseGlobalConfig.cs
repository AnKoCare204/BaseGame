using System;
using CoreData;
using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetDatabaseGlobalConfig", menuName = "GlobalConfigs/AssetDatabaseGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class SpriteDatabaseGlobalConfig  : GlobalConfig<SpriteDatabaseGlobalConfig>
{
    [field: SerializeField] public ResourceSpriteDatabaseConfig[] ResourceSpriteDatabaseConfigs {get; private set;}
    [field: SerializeField] public ItemGroupSpriteDatabaseConfig[] ItemGroupSpriteDatabaseConfigs { get; private set; }
    public Sprite GetResourceSprite(ResourceType resourceType)
    {
        for (int i = 0; i < ResourceSpriteDatabaseConfigs.Length; i++)
        {
            if (ResourceSpriteDatabaseConfigs[i].Key != resourceType) continue;
            return ResourceSpriteDatabaseConfigs[i].Sprite;
        }
        return null;
    }

    public Sprite GetItemGroupSprite(ItemGroupType itemGroupType)
    {
        for (int i = 0; i < ItemGroupSpriteDatabaseConfigs.Length; i++)
        {
            if (ItemGroupSpriteDatabaseConfigs[i].Key != itemGroupType) continue;
            return ItemGroupSpriteDatabaseConfigs[i].Sprite;
        }

        return null;
    }
}

public static class SpriteDatabaseGlobalConfigExtension
{
    public static Sprite GetSprite(this ResourceType resourceType)
    {
        return SpriteDatabaseGlobalConfig.Instance.GetResourceSprite(resourceType);
    }

    public static Sprite GetItemGroupSprite(this ItemGroupType itemGroupType)
    {
        return SpriteDatabaseGlobalConfig.Instance.GetItemGroupSprite(itemGroupType);
    }
}

public interface ISpriteDatabaseConfig<T>
{
    public T Key { get; }
    public Sprite Sprite { get; }
}

[Serializable]
public class ResourceSpriteDatabaseConfig : ISpriteDatabaseConfig<ResourceType>
{
    [field: SerializeField] public ResourceType Key { get; set; }
    [field: SerializeField] public Sprite Sprite { get; set; }
}[Serializable]
public class ItemGroupSpriteDatabaseConfig : ISpriteDatabaseConfig<ItemGroupType>
{
    [field: SerializeField] public ItemGroupType Key { get; set; }
    [field: SerializeField] public Sprite Sprite { get; set; }
}

