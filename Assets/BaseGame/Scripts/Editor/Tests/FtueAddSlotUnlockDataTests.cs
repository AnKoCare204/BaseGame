using CoreData;
using NUnit.Framework;
using UnityEditor;

/// Add Slot mở lúc vào L3 như mọi booster khác (popup nhận → bay về nút → ép dùng thử), không còn tut giữa level. Đọc dữ liệu thật.
public class FtueAddSlotUnlockDataTests
{
    private static BoosterInfoData AddSlot() =>
        AssetDatabase.LoadAssetAtPath<ItemGlobalConfig>("Assets/Resources/GlobalConfig/ItemGlobalConfig.asset").GetBoosterData(ResourceType.AddSlot);

    [Test]
    public void UnlocksAtLevelThree_NotMidLevel()
    {
        BoosterInfoData data = AddSlot();
        Assert.AreEqual(3, data.LevelUnlock);
        Assert.IsFalse(data.UnlockMidLevel);
    }

    [Test]
    public void ClaimFlowRunsWhenEnteringLevelThree() => Assert.IsTrue(BoosterUnlockRules.NeedsUnlockFlowAtLevelStart(AddSlot(), 3, false));

    [Test]
    public void ButtonOpenFromLevelThree_EvenBeforeClaim() => Assert.IsTrue(BoosterUnlockRules.IsSelectorUnlocked(AddSlot(), 3, false));
}
