using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Screen = TW.UGUI.Core.Screens.Screen;

public class ScreenHome : Screen
{
    [field: SerializeField] public ScreenHomeContext.UIPresenter UIPresenter { get; private set; }
    [field: SerializeField] public UIMainMenuTab UIMainMenuTab { get; private set; }
    protected override void Awake()
    {
        base.Awake();
        AddLifecycleEvent(UIPresenter, 1);
        AddLifecycleEvent(UIMainMenuTab, 2);
    }

    public override async UniTask Initialize(Memory<object> args)
    {
        await base.Initialize(args);
    }
}