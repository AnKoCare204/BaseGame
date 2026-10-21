using Core.Tutorial;
using NUnit.Framework;

/// Phiên tutorial cũ (đã bị thay) không được đụng trạng thái chung: chỉ phiên hiện tại mới được khóa bấm / làm tối / hiện chữ.
/// Kiểm bằng TutorialRunContext + luật chọn phiên thuần TutorialStage.IsOwner.
public class FtueStageOwnershipTests
{
    [Test]
    public void OnlyCurrentStageOwnsSharedState()
    {
        object current = new object();
        object stale = new object();
        Assert.IsTrue(TutorialStage.IsOwner(current, current));
        Assert.IsFalse(TutorialStage.IsOwner(stale, current));
        Assert.IsFalse(TutorialStage.IsOwner(current, null));
    }

    [Test]
    public void RunContext_IsDone_HandlesNull()
    {
        var ctx = new TutorialRunContext(2, null);
        Assert.IsFalse(ctx.IsDone(TutorialIds.L1Match));
        var ctx2 = new TutorialRunContext(2, new[] { TutorialIds.L1Match });
        Assert.IsTrue(ctx2.IsDone(TutorialIds.L1Match));
        Assert.AreEqual(2, ctx2.Level);
    }
}
