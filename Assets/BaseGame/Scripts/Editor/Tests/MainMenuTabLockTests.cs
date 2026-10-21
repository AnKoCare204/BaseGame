using System;
using NUnit.Framework;

/// Khóa vuốt tab Home: popup (DailyReward, FillHeart...) đang mở thì toast / loading đóng không được mở lại tab.
/// Lỗi gốc: tắt mạng -> DailyReward -> xem ads -> toast "No internet" -> đóng toast -> vuốt được tab dù DailyReward còn mở.
public class MainMenuTabLockTests
{
    private readonly object toast = new();
    private readonly object loading = new();

    [SetUp]
    public void SetUp() => UIMainMenuTab.ResetTabLocks();

    [TearDown]
    public void TearDown() => UIMainMenuTab.ResetTabLocks();

    private static void Open(MainMenuPopupLifecycle popup) => popup.Initialize(Memory<object>.Empty);

    private static void Close(MainMenuPopupLifecycle popup) => popup.Cleanup(Memory<object>.Empty);

    [Test]
    public void PopupOpen_ToastShownThenClosed_TabsStayLocked()
    {
        var dailyReward = new MainMenuPopupLifecycle();
        Open(dailyReward);
        UIMainMenuTab.SetTabLock(toast, true);
        UIMainMenuTab.SetTabLock(toast, false);
        Assert.IsTrue(UIMainMenuTab.IsTabLocked);
    }

    [Test]
    public void ModalOpen_LoadingShownThenHidden_TabsStayLocked()
    {
        var noAds = new MainMenuModalLifecycle();
        noAds.Initialize(Memory<object>.Empty);
        UIMainMenuTab.SetTabLock(loading, true);
        UIMainMenuTab.SetTabLock(loading, false);
        Assert.IsTrue(UIMainMenuTab.IsTabLocked);
    }

    [Test]
    public void PopupAndToastBothClosed_TabsUnlocked()
    {
        var dailyReward = new MainMenuPopupLifecycle();
        Open(dailyReward);
        UIMainMenuTab.SetTabLock(toast, true);
        UIMainMenuTab.SetTabLock(toast, false);
        Close(dailyReward);
        Assert.IsFalse(UIMainMenuTab.IsTabLocked);
    }

    // Toast còn mở khi popup đóng trước: vẫn khóa tới khi toast đóng.
    [Test]
    public void PopupClosedWhileToastOpen_TabsStayLockedUntilToastCloses()
    {
        var dailyReward = new MainMenuPopupLifecycle();
        Open(dailyReward);
        UIMainMenuTab.SetTabLock(toast, true);
        Close(dailyReward);
        Assert.IsTrue(UIMainMenuTab.IsTabLocked);
        UIMainMenuTab.SetTabLock(toast, false);
        Assert.IsFalse(UIMainMenuTab.IsTabLocked);
    }

    [Test]
    public void TwoPopupsNested_StayLockedUntilBothClosed()
    {
        var dailyReward = new MainMenuPopupLifecycle();
        var claimReward = new MainMenuPopupLifecycle();
        Open(dailyReward);
        Open(claimReward);
        Close(claimReward);
        Assert.IsTrue(UIMainMenuTab.IsTabLocked);
        Close(dailyReward);
        Assert.IsFalse(UIMainMenuTab.IsTabLocked);
    }

    // ShowLoadingBlock có thể gọi 2 lần liên tiếp rồi mới Hide 1 lần.
    [Test]
    public void SameOwnerLockedTwice_OneUnlock_Unlocks()
    {
        UIMainMenuTab.SetTabLock(loading, true);
        UIMainMenuTab.SetTabLock(loading, true);
        UIMainMenuTab.SetTabLock(loading, false);
        Assert.IsFalse(UIMainMenuTab.IsTabLocked);
    }

    // Toast đóng khi không có gì khác đang mở: tab mở lại như cũ.
    [Test]
    public void OnlyToast_Closed_TabsUnlocked()
    {
        UIMainMenuTab.SetTabLock(toast, true);
        UIMainMenuTab.SetTabLock(toast, false);
        Assert.IsFalse(UIMainMenuTab.IsTabLocked);
    }
}
