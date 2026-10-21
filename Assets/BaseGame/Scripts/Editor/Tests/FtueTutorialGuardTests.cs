using System;
using System.Collections.Generic;
using Core.Tutorial;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// Bước tutorial chạy ngoài kịch bản (câu cứu miễn phí) cũng cần lưới an toàn như RunScriptAsync: lỗi thì log và trả lớp tối / khóa bấm,
/// không bao giờ để người chơi kẹt; bị huỷ (level kết thúc) thì im lặng vì EndLevel đã dọn.
public class FtueTutorialGuardTests
{
    private readonly List<Exception> logged = new();

    [SetUp]
    public void SetUp() => logged.Clear();

    [Test]
    public void Throws_LogsAndCallsOnFail_WithoutRethrowing()
    {
        int fails = 0;
        UniTask task = TutorialGuard.RunAsync(() => UniTask.FromException(new InvalidOperationException("boom")), () => fails++, logged.Add);

        Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        Assert.AreEqual(1, fails);
        Assert.AreEqual(1, logged.Count);
        Assert.AreEqual("boom", logged[0].Message);
    }

    [Test]
    public void Cancelled_DoesNotCallOnFail()
    {
        int fails = 0;
        UniTask task = TutorialGuard.RunAsync(() => UniTask.FromCanceled(), () => fails++, logged.Add);

        Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        Assert.AreEqual(0, fails);
        Assert.IsEmpty(logged);
    }

    [Test]
    public void Succeeds_DoesNotCallOnFail()
    {
        int fails = 0;
        UniTask task = TutorialGuard.RunAsync(() => UniTask.CompletedTask, () => fails++, logged.Add);

        Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        Assert.AreEqual(0, fails);
        Assert.IsEmpty(logged);
    }
}
