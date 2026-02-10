using System.Collections.Generic;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.Events;

public class InAppPurchaseManager : Singleton<InAppPurchaseManager>
{
    [field: SerializeField] public List<IAPPackage> IAPPackages { get; set; }
    private UnityAction m_OnBuySuccess;
    private void Start()
    {
        Transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }
}
