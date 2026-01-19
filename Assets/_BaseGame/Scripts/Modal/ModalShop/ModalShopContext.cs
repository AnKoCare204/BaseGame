using System;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using BaseGame.Scripts.Common;
using Core.UI;
using Cysharp.Threading.Tasks;
using Manager;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.Events;
using Button = UnityEngine.UI.Button;

[Serializable]
public class ModalShopContext
{
    public static class Events
    {

    }

    [HideLabel]
    [Serializable]
    public class UIModel : IAModel
    {
        [field: Title(nameof(UIModel))]


        public UniTask Initialize(Memory<object> args)
        {
            return UniTask.CompletedTask;
        }
    }

    [HideLabel]
    [Serializable]
    public class UIView : IAView
    {
        [field: Title(nameof(UIView))]
        [field: SerializeField]
        public CanvasGroup MainView { get; private set; }

        public UniTask Initialize(Memory<object> args)
        {
            return UniTask.CompletedTask;
        }
    }

    [HideLabel]
    [Serializable]
    public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
    {
        [field: SerializeField] public UIModel Model { get; private set; } = new();
        [field: SerializeField] public UIView View { get; set; } = new();

        public async UniTask Initialize(Memory<object> args)
        {
            await Model.Initialize(args);
            await View.Initialize(args);
        }


        public UniTask Cleanup(Memory<object> args)
        {
            return UniTask.CompletedTask;
        }
    }
}