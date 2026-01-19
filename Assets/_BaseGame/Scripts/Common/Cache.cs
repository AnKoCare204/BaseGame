using System;
using System.Collections.Generic;
using UnityEngine;

public static class Cache
{
    private static Dictionary<float, WaitForSeconds> m_WFS = new Dictionary<float, WaitForSeconds>();

    public static WaitForSeconds GetWFS(float key)
    {
        if (!m_WFS.ContainsKey(key))
        {
            m_WFS[key] = new WaitForSeconds(key);
        }
        return m_WFS[key];
    }

    private static WaitForEndOfFrame _WaitForEndOfFrame;

    public static WaitForEndOfFrame GetWaitForEndOfFrame => _WaitForEndOfFrame ??= new WaitForEndOfFrame();

    public static string ConvertToString(DateTime value)
    {
        var result = value.ToString("yyyy-MM-dd HH:mm:ss");
        return result;
    }

    public static string GetSpecificName(ResourceType type, int specificType)
    {
        switch (type)
        {
            case ResourceType.Currency:
                return ((CurrencyType)specificType).ToString();
            default:
                return string.Empty;           
        }
    }
    
    public static int GetIndexInSprite(ResourceType type, int specificType)
    {
        switch (type)
        {
            case ResourceType.Currency:
                return (CurrencyType)specificType switch
                {
                    CurrencyType.Coin => 0,
                    CurrencyType.Gem => 1,
                    _ => -1
                };

            default:
                return -1;           
        }
    }
}
