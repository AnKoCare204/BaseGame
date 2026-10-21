using System;
using System.Reflection;
using Core.GamePlay;
using Core.UI.Modals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// ModalShop bị huỷ phải gỡ listener khỏi GameGlobalEvent.OnPurchaseIapSuccess.
/// Lỗi gốc (starter pack mua được nhiều lần): mở ModalShop dạng popup rồi đóng, pooling tắt nên modal bị Destroy nhưng
/// presenter vẫn nằm trong event. Lần mua sau delegate chết ném MissingReferenceException ở UIShopPack.InitUI, event dừng
/// giữa chừng và PackHandler không ẩn được gói vừa mua.
public class ModalShopListenerTests
{
    private const string ModalShopPrefab = "Assets/BaseGame/Addressable/UI/ModalShop.prefab";

    private GameObject first;
    private GameObject second;

    [TearDown]
    public void TearDown()
    {
        Unload(ref first);
        Unload(ref second);
    }

    [Test]
    public void DestroyedShop_StopsListeningToPurchaseSuccess()
    {
        ModalShop shop = LoadShop(ref first);
        Listen(shop);

        SendOnDestroy(shop);

        Assert.IsFalse(IsListening(shop), "ModalShop đã huỷ vẫn còn trong OnPurchaseIapSuccess");
    }

    [Test]
    public void DestroyedShop_KeepsOtherShopListening()
    {
        ModalShop destroyed = LoadShop(ref first);
        ModalShop alive = LoadShop(ref second);
        Listen(destroyed);
        Listen(alive);

        SendOnDestroy(destroyed);

        Assert.IsTrue(IsListening(alive), "Huỷ một ModalShop không được gỡ listener của ModalShop khác");
    }

    private static ModalShop LoadShop(ref GameObject root)
    {
        root = PrefabUtility.LoadPrefabContents(ModalShopPrefab);
        ModalShop shop = root.GetComponent<ModalShop>();
        Assert.IsNotNull(shop, "Prefab ModalShop không có component ModalShop ở gốc");
        return shop;
    }

    // Initialize đăng ký đúng handler này; không gọi Initialize thật được vì nó cần GameManager, IAPController và player loop.
    private static void Listen(ModalShop shop) =>
        GameGlobalEvent.OnPurchaseIapSuccess += shop.UIPresenter.OnBuyNoConsumablePack;

    // Edit mode không gửi OnDestroy cho MonoBehaviour không có ExecuteAlways, nên gửi hộ như Unity làm khi Destroy.
    private static void SendOnDestroy(ModalShop shop)
    {
        MethodInfo onDestroy = shop.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(onDestroy, "Không tìm thấy OnDestroy trên ModalShop");
        onDestroy.Invoke(shop, null);
    }

    private static bool IsListening(ModalShop shop)
    {
        Action handlers = GameGlobalEvent.OnPurchaseIapSuccess;
        if (handlers == null) return false;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            if (ReferenceEquals(handler.Target, shop.UIPresenter)) return true;
        }
        return false;
    }

    private static void Unload(ref GameObject root)
    {
        if (root == null) return;
        ModalShop shop = root.GetComponent<ModalShop>();
        if (shop != null) GameGlobalEvent.OnPurchaseIapSuccess -= shop.UIPresenter.OnBuyNoConsumablePack;
        PrefabUtility.UnloadPrefabContents(root);
        root = null;
    }
}
