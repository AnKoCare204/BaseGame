using System;

public enum ResourceType
{
    None,
    Currency,
}

public static class ResourceTypeExtension
{
    public static ResourceType GetResourceType(this Enum specificType)
    {
        return specificType switch
        {
            CurrencyType => ResourceType.Currency,
            _ => ResourceType.None
        };
    }
}