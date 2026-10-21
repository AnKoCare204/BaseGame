using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;

public class MainMenuModalLifecycle : IModalLifecycleEventSimple, IActivityLifecycleEventSimple
{
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