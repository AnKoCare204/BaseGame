using System;
using System.Collections.Generic;
using CoreData;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Utility.Extension;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ShopGlobalConfig", menuName = "GlobalConfigs/ShopGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ShopGlobalConfig : GlobalConfig<ShopGlobalConfig>
{
    public List<ShopPackageDataConfig> shopPackages;
    
    #if UNITY_EDITOR
    
    
    [Button]
    private async UniTask LoadDataFromGoogleSheet()
    {
        EditorUtility.SetDirty(this);
        shopPackages.Clear();
        List<Dictionary<string, string>> tableData = await ABakingSheet.GetDataTable("1zGWdbe707IwQtYSoIygGJQUBKzX-ioXyGEnj_COeHTs", "ShopConfig");
        foreach (var item in tableData)
        {
            ShopPackageDataConfig shopPackageDataConfig;
            if (item["PackageId"].Trim().Equals(""))
            {
                shopPackageDataConfig = shopPackages[^1];
            }
            else
            {
                shopPackageDataConfig = new ShopPackageDataConfig();
                shopPackages.Add(shopPackageDataConfig);
                shopPackageDataConfig.consumable = int.Parse(item["Consumable"]) == 1 ? true : false;
                shopPackageDataConfig.packageNameToUI = item["PackageNameToUI"];
                shopPackageDataConfig.packageId = $"{item["PackageId"]}";
                string packName = item["PackageName"];

                shopPackageDataConfig.packageName = (PackageName)Enum.Parse(typeof(PackageName), packName);
                // shopPackageDataConfig.purchaseCount = int.Parse(item["PurchaseCount"]);
                shopPackageDataConfig.GameResources = new List<GameResource>();
                shopPackageDataConfig.priceType = (PriceType)Enum.Parse(typeof(PriceType), item["PriceType"]);
                shopPackageDataConfig.price = float.Parse(item["Price"]);
            }
            
            ResourceType gameResourceType = (ResourceType)Enum.Parse(typeof(ResourceType), item["ResourceType"]);
            if (gameResourceType != ResourceType.None)
            {
                int amount = int.Parse(item["Value"]);
                GameResource gameResource = new GameResource(gameResourceType, amount);
                shopPackageDataConfig.GameResources.Add(gameResource);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #endif
    
    public ShopPackageDataConfig GetShopPackageDataConfig(PackageName packageName)
    {
        return shopPackages.Find(x => x.packageName == packageName);
    }
    public ShopPackageDataConfig GetShopPackageDataConfig(string packageId)
    {
        return shopPackages.Find(x => x.packageId == packageId);
    }
}

public enum PackageName
{
    None = 0,
    no_ads = 1,
    no_ads_bundle = 2,
    limited_bundle = 3,
    small_bundle = 4,
    medium_bundle = 5,
    ultra_bundle = 6,
    fail_offer = 7,
    starter_pack = 8,


    coin1 = 101,
    coin2 = 102,
    coin3 = 103,
    coin4 = 104,
    coin5 = 105,
    coin6 = 106,

    group_pack_1 = 200,
    group_pack_2 = 201,
    group_pack_3 = 202,
    group_pack_4 = 203,
}
public enum PriceType
{
    IAP,
    Ads,
    Money,
}

[System.Serializable]
public class ShopPackageDataConfig
{
    public PackageName packageName;
    public int purchaseCount;
    public bool consumable;
    public string packageNameToUI;
    public string packageTag;
    public string packageId;
    public PriceType priceType;
    public float price;
    public List<GameResource> GameResources;
}

[System.Serializable]
public class GameResourceData
{
    public ResourceType resourceType;
    public Sprite icon;
}