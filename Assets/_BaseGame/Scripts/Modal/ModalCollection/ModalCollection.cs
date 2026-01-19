using System;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Modals;
using UnityEngine;

public class ModalCollection : Modal, IUITabLifecycleEvent
{
    [field: SerializeField] public ModalCollectionContext.UIPresenter UIPresenter { get; private set; }

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
