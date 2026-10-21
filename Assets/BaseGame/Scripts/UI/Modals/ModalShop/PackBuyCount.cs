using CoreData;
using UnityEngine;

public class PackBuyCount : IPackHandlerCondition
{
    [field: SerializeField] PackageName PackName { get; set; }
    [field: SerializeField] int RequiredBuyCount { get; set; }
    public bool IsConditionMet()
    {
        int purchasedCount = 0;
        for (int i = 0; i < ShopData.Instance.PurchasedPacks.Count; i++)
        {
            if (ShopData.Instance.PurchasedPacks[i] == PackName.ToString())
            {
                purchasedCount++;
            }
        }
        return purchasedCount >= RequiredBuyCount;
    }
}