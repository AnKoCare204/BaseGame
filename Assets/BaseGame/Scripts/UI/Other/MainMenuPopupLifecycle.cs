using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;

public class MainMenuPopupLifecycle : IModalLifecycleEventSimple, IActivityLifecycleEventSimple
{
    // Mỗi popup giữ khóa riêng nên popup lồng nhau tự đếm, không cần bộ đếm tĩnh.
    public UniTask Initialize(Memory<object> args)
    {
        UIMainMenuTab.SetTabLock(this, true);
        return UniTask.CompletedTask;
    }

    public UniTask Cleanup(Memory<object> args)
    {
        UIMainMenuTab.SetTabLock(this, false);
        return UniTask.CompletedTask;
    }
}