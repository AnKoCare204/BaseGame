using System;
using Cysharp.Threading.Tasks;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Screens;
using Screen = TW.UGUI.Core.Screens.Screen;

namespace Core.UI.Screens
{
    public class ScreenMainMenu : Screen
    {
        [field: SerializeField] public ScreenMainMenuContext.UIPresenter UIPresenter { get; private set; }
        [field: SerializeField] private UIMainMenuTab UIMainMenuTab {get; set;}

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

        public override UniTask Cleanup(Memory<object> args)
        {
            UIMainMenuTab.Cleanup();
            return UniTask.CompletedTask;
        }
    }


    [Serializable]
    public class ScreenMainMenuContext
    {
        public static class Events
        {
            public static Action<bool> SetInteractable { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }

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
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public GameObject BlockGroup { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IScreenLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                AudioManager.Instance.ChangeMusic(AudioKey.BgMainMenu, 0.5f);
                GameManager.Instance.SetState(GameState.MainMenu);
                Events.SetInteractable = SetInteractable;

            }
            public UniTask Cleanup(Memory<object> args)
            {
                Events.SetInteractable = null;
                return UniTask.CompletedTask;
            }
            private void SetInteractable(bool value)
            {
                View.BlockGroup.SetActive(!value);
            }
        }
    }
}