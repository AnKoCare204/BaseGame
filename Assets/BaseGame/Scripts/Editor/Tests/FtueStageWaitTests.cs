using System.Collections.Generic;
using System.Threading;
using Core.GamePlay;
using Core.Tutorial;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Kịch bản phải đăng ký chờ trước khi mở bấm: người chơi bấm trong lúc khung chữ còn đang nạp thì lần bấm không được lọt
/// (lọt là kẹt vĩnh viễn trong khóa bấm). WaitPickedAll đăng ký ngay lúc gọi, đếm cả lần bấm xảy ra trước khi await.
public class FtueStageWaitTests
{
    private readonly List<GameObject> created = new();

    private Fish MakeFish()
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags("fish", HideFlags.HideAndDontSave, typeof(Fish));
        created.Add(go);
        return go.GetComponent<Fish>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created) Object.DestroyImmediate(go);
        created.Clear();
    }

    [Test]
    public void WaitPickedAll_CountsPicksMadeBeforeTheAwait()
    {
        Fish a = MakeFish(), b = MakeFish(), other = MakeFish();
        var stage = new TutorialStage(null, null, 1);
        var remaining = new List<Fish> { a, b };
        int calls = 0;
        UniTask task = stage.WaitPickedAll(remaining, (fish, left) => calls++, CancellationToken.None);

        GameGlobalEvent.OnFishPicked?.Invoke(other);   // con không nằm trong danh sách: bỏ qua
        GameGlobalEvent.OnFishPicked?.Invoke(a);
        GameGlobalEvent.OnFishPicked?.Invoke(b);

        Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        Assert.AreEqual(2, calls);
        Assert.IsEmpty(remaining);
    }

    [Test]
    public void WaitPickedAll_UnsubscribesWhenDone()
    {
        Fish a = MakeFish();
        var stage = new TutorialStage(null, null, 1);
        int calls = 0;
        UniTask task = stage.WaitPickedAll(new List<Fish> { a }, (fish, left) => calls++, CancellationToken.None);
        GameGlobalEvent.OnFishPicked?.Invoke(a);
        GameGlobalEvent.OnFishPicked?.Invoke(a);
        Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        Assert.AreEqual(1, calls);
    }
}
