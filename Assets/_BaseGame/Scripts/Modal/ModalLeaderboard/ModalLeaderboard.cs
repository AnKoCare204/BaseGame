using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Modals;
using UnityEngine;

public class ModalLeaderboard : Modal, IUITabLifecycleEvent
{
    [field: SerializeField] public ModalLeaderboardContext.UIPresenter UIPresenter { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        AddLifecycleEvent(UIPresenter, 1);
    }
    public override async UniTask Initialize(Memory<object> args)
    { 
        await base.Initialize(args);
    }

    public void Initialize()
    {
        UIPresenter.Initialize(null).Forget();
    }

    public void OnTabEnter()
    {

    }

    public void OnTabExit()
    {

    }
}
