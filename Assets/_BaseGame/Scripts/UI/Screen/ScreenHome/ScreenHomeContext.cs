using System;
using Cysharp.Threading.Tasks;
using Manager;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Screens;
using TW.UGUI.MVPPattern;
using UnityEngine;

[Serializable]
public class ScreenHomeContext
{
    public static class Events
    {
        public static Action ShowModalShop { get; set; }
        public static Action ShowModalTeam { get; set; }

    }

    [HideLabel]
    [Serializable]
    public class UIModel : IAModel
    {
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
        [field: SerializeField]
        public TabButton ShopButton {get; private set;}
        [field: SerializeField]
        public TabButton TeamButton {get; private set;}
        public UniTask Initialize(Memory<object> args)
        {
            return UniTask.CompletedTask;
        }

        public void OnTabButtonClick(TabButton tabButton)
        {
            tabButton.ForceClick();
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
            // LoadingManager.Instance.BackGroundLoadProgressAsync().Forget();
            Events.ShowModalShop += ShowModalShop;
            Events.ShowModalTeam += ShowModalTeam;
        }

        private void ShowModalShop()
        {
            View.OnTabButtonClick(View.ShopButton);
        }
        private void ShowModalTeam()
        {
            View.OnTabButtonClick(View.TeamButton);
        }
        public UniTask WillPopEnter(Memory<object> args)
        {
            SoundManager.Instance.PlayMusic(SoundType.BgMainMenu);

            return UniTask.CompletedTask;
        }

        public UniTask WillPushEnter(Memory<object> args)
        {
            SoundManager.Instance.PlayMusic(SoundType.BgMainMenu);
            
            return UniTask.CompletedTask;
        }

        public UniTask Cleanup(Memory<object> args)
        {
            Events.ShowModalShop -= ShowModalShop;
            Events.ShowModalTeam -= ShowModalTeam;
            return UniTask.CompletedTask;
        }
        
    }
}