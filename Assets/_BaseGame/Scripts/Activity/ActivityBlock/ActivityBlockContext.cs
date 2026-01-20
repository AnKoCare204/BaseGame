using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Activities;
using TW.UGUI.MVPPattern;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

[Serializable]
public class ActivityBlockContext 
{
    public static class Events
    {
        public static Action CallAdsUnAvailable { get; set; }
        public static Action CallNoMovesLeft { get; set; }
        public static Action<int> CallNotification { get; set; }
        public static Action<bool> WaitForPurchase { get; set; }
        public static Action<bool> LoadingData { get; set; }
        public static Action<bool, GameResource[], Vector3> ShowInfo { get; set; }
        public static Action<int, Vector3> PlayUICoinEffect { get; set; }
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
        [field: SerializeField] public CanvasGroup MainView { get; private set; }
        [field: SerializeField] public CanvasGroup AdsUnAvailable { get; private set; }
        [field: SerializeField] public Transform AdsUnAvailablePos0 { get; private set; }
        [field: SerializeField] public Transform AdsUnAvailablePos1 { get; private set; }
        [field: SerializeField] public CanvasGroup NoMovesLeft { get; private set; }
        [field: SerializeField] public Transform NoMovesLeftPos0 { get; private set; }
        [field: SerializeField] public Transform NoMovesLeftPos1 { get; private set; }
        [field: SerializeField] public CanvasGroup Notification { get; private set; }
        [field: SerializeField] public Transform NotificationPos0 { get; private set; }
        [field: SerializeField] public Transform NotificationPos1 { get; private set; }
        [field: SerializeField] public TextMeshProUGUI NotificationText { get; private set; }
        [field: SerializeField] public GameObject WaitPurchasingGroup { get; private set; }
        [field: SerializeField] public GameObject LoadingDataGroup { get; private set; }
        [field: SerializeField] public GameObject ShowInfoGroup { get; private set; }
        [field: SerializeField] public RectTransform UIItemInfoContainer { get; private set; }
        [field: SerializeField] public RectTransform UICoinEffectContainer { get; private set; }
        // [field: SerializeField] public UICoinEffect UICoinEffect { get; private set; }
        private MotionHandle adsUnAvailableMoveMotion;
        private MotionHandle adsUnAvailableFadeMotion;
        private MotionHandle noMovesLeftMoveMotion;
        private MotionHandle noMovesLeftFadeMotion;
        private MotionHandle notificationMoveMotion;
        private MotionHandle notificationFadeMotion;
        public UniTask Initialize(Memory<object> args)
        {
            ShowInfoGroup.UpdateAsObservable()
                .Where(_ => Input.GetKeyDown(KeyCode.Mouse0))
                .Subscribe(OnClickAnywhere).AddTo(ShowInfoGroup);
            return UniTask.CompletedTask;
        }

        [Button]
        public void CallNoMovesLeft()
        {
            CancelNoMovesLeftMotion();
            NoMovesLeft.gameObject.SetActive(true);
            noMovesLeftMoveMotion = LMotion.Create(NoMovesLeftPos0.position, NoMovesLeftPos1.position, 0.75f)
                .Bind(x => NoMovesLeft.transform.position = x)
                .AddTo(MainView);
            noMovesLeftFadeMotion = LMotion.Create(1, 0, 1.5f)
                .Bind(x => NoMovesLeft.alpha = x)
                .AddTo(MainView);
        }
        public void CancelNoMovesLeftMotion()
        {
            if (noMovesLeftMoveMotion.IsActive())
                noMovesLeftMoveMotion.Cancel();
            if (noMovesLeftFadeMotion.IsActive())
                noMovesLeftFadeMotion.Cancel();
        }

        [Button]
        public void CallAdsUnAvailable()
        {
            CancelAdsUnAvailableMotion();
            AdsUnAvailable.gameObject.SetActive(true);
            adsUnAvailableMoveMotion = LMotion.Create(AdsUnAvailablePos0.position, AdsUnAvailablePos1.position, 0.75f)
                .Bind(x => AdsUnAvailable.transform.position = x)
                .AddTo(MainView);
            adsUnAvailableFadeMotion = LMotion.Create(1, 0, 1.5f)
                .Bind(x => AdsUnAvailable.alpha = x)
                .AddTo(MainView);
        }
        public void CancelAdsUnAvailableMotion()
        {
            if (adsUnAvailableMoveMotion.IsActive())
                adsUnAvailableMoveMotion.Cancel();
            if (adsUnAvailableFadeMotion.IsActive())
                adsUnAvailableFadeMotion.Cancel();
        }

        [Button]
        public void CallNotification(int idx)
        {
            NotificationText.text = GameManager.Instance.GetTextData(idx).textValue;
            CancelNotificationMotion();
            Notification.gameObject.SetActive(true);
            notificationMoveMotion = LMotion.Create(NotificationPos0.position, NotificationPos1.position, 0.75f)
                .Bind(x => Notification.transform.position = x)
                .AddTo(MainView);
            notificationFadeMotion = LMotion.Create(1, 0, 1.5f)
                .Bind(x => Notification.alpha = x)
                .AddTo(MainView);
        }
        public void CancelNotificationMotion()
        {
            if (notificationMoveMotion.IsActive())
                notificationMoveMotion.Cancel();
            if (notificationFadeMotion.IsActive())
                notificationFadeMotion.Cancel();
        }

        public void WaitForPurchase(bool isPurchasing)
        {
            WaitPurchasingGroup.SetActive(isPurchasing);
        }
        
        public void LoadingData(bool isLoading)
        {
            LoadingDataGroup.SetActive(isLoading);
        }
        public void ShowInfo(bool isShow, GameResource[] rewards, Vector3 pos)
        {
            // ShowInfoGroup.transform.localScale = Vector3.zero;
            // ShowInfoGroup.transform.position = pos;
            // ShowInfoGroup.SetActive(isShow);
            // if (!isShow) return;
            // ShowInfoGroup.transform.localScale = Vector3.one;
            // for (int i = 0; i < UIItemInfoList.Count; i++)
            // {
            //     Object.Destroy(UIItemInfoList[i].gameObject);
            // }
            // UIItemInfoList.Clear();
            // for (int i = 0; i < rewards.Length; i++)
            // {
            //     UIItemInfo uiItem = Object.Instantiate(UIItemInfo, UIItemInfoContainer);
            //     uiItem.gameObject.SetActive(true);
            //     uiItem.Init(rewards[i]);
            //     UIItemInfoList.Add(uiItem);
            // }
        }
        private void OnClickAnywhere(Unit _)
        {
            Vector2 mousePos = Input.mousePosition;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(UIItemInfoContainer, mousePos, null);
            if (inside) return;
            ShowInfo(false, null, Vector3.zero);
        }
        [Button]
        public void PlayEffect(int coin, Vector3 pos)
        {
            // UICoinEffect uiCoin = SimplePool.Spawn(UICoinEffect, UICoinEffectContainer);
            // uiCoin.Play(coin, pos);
        }
    }

    [HideLabel]
    [Serializable]
    public class UIPresenter : IAPresenter, IActivityLifecycleEventSimple
    {
        [field: SerializeField] public UIModel Model {get; private set;} = new();
        [field: SerializeField] public UIView View { get; set; } = new();        

        public async UniTask Initialize(Memory<object> args)
        {
            await Model.Initialize(args);
            await View.Initialize(args);
            Events.CallAdsUnAvailable = View.CallAdsUnAvailable;
            Events.CallNoMovesLeft = View.CallNoMovesLeft;
            Events.CallNotification = View.CallNotification;
            Events.WaitForPurchase = View.WaitForPurchase;
            Events.LoadingData = View.LoadingData;
            Events.ShowInfo = View.ShowInfo;
            Events.PlayUICoinEffect = View.PlayEffect;
        }   
        public UniTask Cleanup(Memory<object> args)
        {
            Events.CallAdsUnAvailable = null;
            Events.CallNoMovesLeft = null;
            Events.CallNotification = null;
            Events.WaitForPurchase = null;
            Events.LoadingData = null;
            Events.ShowInfo = null;
            Events.PlayUICoinEffect = null;
            return UniTask.CompletedTask;
        }
    }
}