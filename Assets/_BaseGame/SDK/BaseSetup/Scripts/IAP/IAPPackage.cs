using System;
using UnityEngine;
using UnityEngine.Purchasing;

[System.Serializable]
public class IAPPackage
{
    [field: SerializeField] public string ProductID { get; private set;}
    [field: SerializeField] private string Price { get; set;}
    private string LocalizedPriceString { get; set;}
    private decimal LocalizedPrice { get; set;}
    private string CurrencyCode { get; set;}
    
    public IAPPackage(string productID,string price) 
    {
        ProductID = productID;
        Price = $"${price}";
    }
    public string GetPrice() 
    {
#if UNITY_EDITOR
        return Price;
#endif
        try
        {
            Product product = Purchaser.Instance.FindProduct(ProductID);
            LocalizedPriceString = product.metadata.localizedPriceString;
            LocalizedPrice = product.metadata.localizedPrice;
            CurrencyCode = product.metadata.isoCurrencyCode;
            return LocalizedPriceString;
        }
        catch (Exception e) 
        {
            Debug.LogError($"GetPrice Error: {e.Message}");
            return Price;
        }
    }
}
