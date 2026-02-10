using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Manager;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Utility.Extension;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ShopGlobalConfig", menuName = "GlobalConfigs/ShopGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ShopGlobalConfig : GlobalConfig<ShopGlobalConfig>
{
    private string linkSheet = "1CfvMgG4uKwWG_6-GMKni8ArPv4paKwC-AF1oiD6GWUU";
    private string sheetTab = "ShopConfig";
    public List<ShopPackageDataConfig> shopPackages;
    public List<IAPSuggestCondition> iapSuggestCondition;
    public List<PackageName> eventIapSuggestPackages;
    public List<PackageName> iapSuggestPackages;
    
    #if UNITY_EDITOR
    
    [Button]
    private async UniTask LoadDataFromGoogleSheet()
    {
        linkSheet = GameDefaultConfig.LinkSheet;
        shopPackages.Clear();
        List<Dictionary<string, string>> tableData = await ABakingSheet.GetDataTable(linkSheet, sheetTab);
        foreach (var item in tableData)
        {
            ShopPackageDataConfig shopPackageDataConfig;
            if (item["PackageId"] == ";")
            {
                shopPackageDataConfig = shopPackages[^1];
            }
            else
            {
                shopPackageDataConfig = new ShopPackageDataConfig();
                shopPackages.Add(shopPackageDataConfig);
                shopPackageDataConfig.nonConsume = int.Parse(item["NonConsume"]) == 1;
                shopPackageDataConfig.packageNameToUI = item["PackageNameToUI"];
                shopPackageDataConfig.packageId = $"{item["PackageId"]}";
                string packName = item["PackageId"];
                if (packName.Contains("."))
                {
                    string[] split = packName.Split('.');
                    packName = split[^1];
                }
                shopPackageDataConfig.packageName = (PackageName)Enum.Parse(typeof(PackageName), packName);
                shopPackageDataConfig.GameResources = new List<GameResource>();
                shopPackageDataConfig.priceType = (PriceType)Enum.Parse(typeof(PriceType), item["PriceType"]);
                shopPackageDataConfig.price = float.Parse(item["Price"]);
                
            }
            
            ResourceType gameResourceType = (ResourceType)Enum.Parse(typeof(ResourceType), item["ResourceType"]);
            if (gameResourceType != ResourceType.None)
            {
                float amount = float.Parse(item["Value"]);
                string specificType = item["SpecificType"];
                GameResource gameResource = new GameResource(gameResourceType, GetSpecificType(gameResourceType, specificType), amount);
                shopPackageDataConfig.GameResources.Add(gameResource);
            }
        }
    }
    #endif
    
    public int GetSpecificType(ResourceType resourceType, string specificType)
    {
        switch (resourceType)
        {
            // case ResourceType.None:
            //     return 0;
            // case ResourceType.Currency:
            //     return (int)((CurrencyType)Enum.Parse(typeof(CurrencyType), specificType));
            // case ResourceType.Booster:
            //     return (int)((BoosterType)Enum.Parse(typeof(BoosterType), specificType));
            // case ResourceType.Special:
            //     return (int)((SpecialResourceType)Enum.Parse(typeof(SpecialResourceType), specificType));
            default:
                return 0;           
        }
    }
    
    public ShopPackageDataConfig GetShopPackageDataConfig(PackageName packageName)
    {
        return shopPackages.Find(x => x.packageName == packageName);
    }
    public bool TryGetShopPackageDataConfig(PackageName packageName, out ShopPackageDataConfig shopPackageDataConfig)
    {
        shopPackageDataConfig = shopPackages.Find(x => x.packageName == packageName);
        return shopPackageDataConfig != null;
    }
}

public enum PackageName
{
    None,
    removeads = 1,
    revivepack = 2,
    starterpack = 3,
    limitedpack = 4,
    bigbundle = 5,
    greatbundle = 6,
    ultrabundle = 7,
    superiorbundle = 8,
    legendarybundle = 9,
    removeadssale = 10,

    schooltotalsale = 50,
    schoolpack1 = 51,
    schoolpack2 = 52,
    schoolpack3 = 53,

    coin1 = 101,
    coin2 = 102,
    coin3 = 103,
    coin4 = 104,
    coin5 = 105,
    coin6 = 106,
    coin7 = 107,

    battlepass = 200,

    endless1 = 300,
    endless2 = 301,
    endless3 = 302,

    foodiepass = 400,

    newlimitedpack1 = 500,
    newlimitedpack2 = 501,
    newlimitedpack3 = 502,
    newlimitedpack4 = 503,
    newlimitedpack5 = 504,
    newlimitedpack6 = 505,
    newlimitedpack7 = 506,

    mooncakepack1 = 600,
    mooncakepack2 = 601,
    mooncakepack3 = 602,

    grouppack1 = 700,
    grouppack2 = 701,
    grouppack3 = 702,
    grouppack4 = 703,

    halloweenpack1 = 800,
    x2foodiestarpack = 900,
    x2thanksgivingboost = 901,
    x2xmasstreakboost = 902,
    x2streakboost = 903,
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
    public bool nonConsume;
    public string packageNameToUI;
    public string packageTag;
    public string packageId;
    public PriceType priceType;
    public float price;
    public List<GameResource> GameResources;
}

[System.Serializable]
public class IAPSuggestCondition
{
    public PackageName packageName;
    public IAPSuggestTrigger trigger;
    public int level;
    public int dayRetention;
    public int limitRetention;
    public int repeatTime; // Hours
}

public enum IAPSuggestTrigger
{
    None,
    OpenApp,
    BackToMenu,
    Both,
}