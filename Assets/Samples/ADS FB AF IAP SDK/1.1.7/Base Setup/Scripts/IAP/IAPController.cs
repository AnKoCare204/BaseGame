using System;
using System.Collections.Generic;
using System.Globalization;
using TW.Utility.DesignPattern;
using UnityEngine;

public class IAPController : Singleton<IAPController>
{
    [field: SerializeField] public List<IAPPackage> IAPPackages { get; set; }
    protected override void Awake()
    {
        base.Awake();
        IAPEventManager.OnPurchase += PurchaseIAPProduct;
    }
    
    
    private void OnDestroy()
    {
        IAPEventManager.OnPurchase -= PurchaseIAPProduct;
    }

    private void Start()
    {
        Load();
    }

    private void PurchaseIAPProduct(string packageID, Action onSuccess, Action onFailure)
    {
        OnBuyIAP(packageID, onSuccess, onFailure);
        // InGameAnalyticController.EventTrackIAPClick?.Invoke(packageID, "shop");
    }
    
    private void Load()
    {
        IAPPackages = new List<IAPPackage>();
        List<ShopPackageDataConfig> shopPackages = ShopGlobalConfig.Instance.shopPackages;
        for (int i = 0; i < shopPackages.Count; i++)
        {
            if (shopPackages[i].priceType == PriceType.IAP)
            {
                InitIAPPackage(shopPackages[i].packageId, shopPackages[i].price.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
    public void InitIAPPackage(string productId, string price)
    {
        IAPPackage iapPackage = new IAPPackage(productId, price);
        IAPPackages.Add(iapPackage);
    }
    public IAPPackage GetIAPPackage(string productId)
    {
        return IAPPackages.Find(x => x.ProductID == productId);
    }
    public void OnBuyIAP(string productId, Action onBuySuccess, Action onBuyFail)
    {
        Purchaser.Instance.BuyIAPProduct(new IAPProduct(productId, onBuySuccess, onBuyFail));
    }
}
