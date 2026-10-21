using System;
using System.Collections.Generic;
using Core.GamePlay;
using Core.Manager;
using Core.UI.Activities;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Screens;
using Screen = TW.UGUI.Core.Screens.Screen;
using Core.UI.Modals;
using CoreData;
using LitMotion;
using MoreMountains.Feedbacks;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine.UI;
using Spine.Unity;

namespace Core.UI.Screens
{
    /// <summary>Phần HUD bật / tắt riêng được (tutorial ẩn bớt HUD ở level đầu).</summary>
    public enum HudPart
    {
        Progress,
        BoosterBar,
    }

    /// <summary>HUD trong bàn: level, tiến độ x/Total, nút pause, thanh booster, thông báo, cảnh báo độ khó.</summary>
    public class ScreenInGame : Screen
    {
        [field: SerializeField] public ScreenInGameContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }

    [Serializable]
    public class ScreenInGameContext
    {
        public static class Events
        {
            public static Action<ResourceType> ShowBoosterHint { get; set; }
            public static Action HideBoosterHint { get; set; }
            public static Action CheckShowAlertDifficult { get; set; }
            public static Func<ResourceType, UniTask> ClaimBooster { get; set; }
            public static Action<ResourceType> OnUnlockWaitClaimBooster { get; set; }
            public static Action<string> ShowNotification { get; set; }
            public static Action ShowBackGroundBlack { get; set; }
            public static Action HideBackGroundBlack { get; set; }
            public static Action LoadLevel { get; set; }
            public static Action<bool> BoosterGroupSetActive { get; set; }
            public static Action<int> UpdateTxtWarning { get; set; }
            public static Action<bool> SetActiveWarning { get; set; }
            public static Action ShowBotPanel { get; set; }
            public static Action HideBotPanel { get; set; }
            public static Action<bool> SetActiveFirstTut { get; set; }
            public static Action<bool> SetBlockObjectActive { get; set; }
            public static Action<int, int> UpdateProgress { get; set; }
            public static Action<bool> SetHudInteractable { get; set; }
            /// <summary>FTUE: bật / tắt bộ đếm (Progress) hoặc thanh booster (BoosterBar); animate = hiện có hiệu ứng.</summary>
            public static Action<HudPart, bool, bool> SetHudPart { get; set; }
            /// <summary>RectTransform của phần HUD (khung chữ tutorial trỏ vào). null nếu chưa nối.</summary>
            public static Func<HudPart, RectTransform> GetHudPartRect { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }

            [field: SerializeField] public Reactive<int> CurLevel { get; private set; } = new(0);

            public UniTask Initialize(Memory<object> args)
            {
                CurLevel = GameManager.Instance.Level;
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

            [field: SerializeField] public Button ButtonPause { get; private set; }
            [field: SerializeField] public TextMeshProUGUI[] TextLevel { get; private set; }
            [field: SerializeField] public TextMeshProUGUI[] TextProgresses { get; private set; }
            [field: SerializeField] public Button BtnSpeedUp { get; private set; }
            [field: SerializeField] public Button BtnBuySpeedUp { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TextSpeedLevel { get; private set; }
            [field: SerializeField] public Transform BoosterBarPos { get; private set; }
            [field: SerializeField] public CanvasGroup BoosterBG { get; private set; }
            [field: SerializeField] public CanvasGroup BoosterCG { get; private set; }
            [field: SerializeField] public AnimationCurve CurveBoosterBarMoveUp { get; private set; }
            [field: SerializeField] public List<BoosterSelector> BoosterSelectors { get; private set; }
            [field: SerializeField] public AlertLevelDifficult AlertLevelDifficult { get; private set; }
            [field: SerializeField] public Transform BoosterClaimRoot { get; private set; }
            [field: SerializeField] public NotificationItemUI NotificationItemUIPrefab { get; private set; }
            [field: SerializeField] public Transform NotificationContainer { get; private set; }
            [field: SerializeField] public Image BackGroundBlackImage { get; private set; }
            [field: SerializeField] public GameObject BoosterGroup { get; private set; }
            [field: SerializeField] public NoAdsSwitchPosition NoAdsSwitchPosition { get; private set; }
            public List<NotificationItemUI> NotificationItems { get; private set; }

            [field: SerializeField] public GameObject PanelWarning { get; private set; }
            [field: SerializeField] public MMF_Player AnimWarning { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TxtWarning { get; private set; }
            [field: SerializeField] public MMF_Player AnimShowBotPanel { get; private set; }
            [field: SerializeField] public MMF_Player AnimHideBotPanel { get; private set; }
            [field: SerializeField] public MMF_Player AnimShowTopPanel { get; private set; }
            [field: SerializeField] public MMF_Player AnimHideTopPanel { get; private set; }
            [field: SerializeField] public GameObject BlockObject { get; private set; }
            [field: SerializeField, Tooltip("Khung bộ đếm tiến độ (TopPanel/...); tutorial có thể ẩn ở level đầu")]
            public GameObject ProgressRoot { get; private set; }
            [field: SerializeField, Tooltip("Thanh booster (ButtonGroup/BoosterBar); FTUE ẩn ở L1-L2 và L3 tới lúc mở Add Slot")]
            public GameObject BoosterBarRoot { get; private set; }
            private bool IsActiveWarning { get; set; }
            private MotionHandle blackGroundMotionHandle;

            public NotificationItemUI GetNotificationItem()
            {
                NotificationItems ??= new List<NotificationItemUI>();
                foreach (NotificationItemUI item in NotificationItems)
                {
                    if (!item.gameObject.activeSelf) return item;
                }

                NotificationItemUI newItem = GameObject.Instantiate(NotificationItemUIPrefab, NotificationContainer);
                newItem.gameObject.SetActive(false);
                NotificationItems.Add(newItem);
                return newItem;
            }

            public UniTask Initialize(Memory<object> args)
            {
                if (BackGroundBlackImage != null) BackGroundBlackImage.gameObject.SetActive(false);
                // Template không có tăng tốc / hút bụi: ẩn phần thừa của HUD.
                if (BtnSpeedUp != null) BtnSpeedUp.gameObject.SetActive(false);
                if (BtnBuySpeedUp != null) BtnBuySpeedUp.gameObject.SetActive(false);
                UpdateProgress(0, 0);
                return UniTask.CompletedTask;
            }

            public void UpdateProgress(int collected, int total)
            {
                string progressText = total > 0 ? $"{collected/3}/{total/3}" : "";

                if (TextProgresses != null)
                {
                    for (int i = 0; i < TextProgresses.Length; i++)
                    {
                        TextProgresses[i].text = progressText;
                    }
                }
            }

            public async UniTask ClaimBooster(ResourceType boosterType)
            {
                ItemGlobalConfig.Instance.ShowBoosterUnlock(boosterType);
                SetBlockObjectActive(true);
                foreach (BoosterSelector selector in BoosterSelectors)
                {
                    if (selector.BoosterType != boosterType) continue;
                    selector.BoosterCG.alpha = 1;
                    ShowBackGroundBlack();
                    await selector.GetBoosterOnTutorial(BoosterClaimRoot.position);
                    break;
                }

                SetBlockObjectActive(false);
                // Báo LevelManager trọn luồng nhận booster đã xong để popup obstacle mới / cảnh báo độ khó hiện tiếp, không đè lên nhau.
                Level level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
                if (level != null)
                {
                    level.SetIsClaimBooster(true);
                    level.SetIsBoosterClaimFlowDone(true);
                }
            }

            public void SetBlockObjectActive(bool isActive)
            {
                if (BlockObject != null) BlockObject.SetActive(isActive);
            }

            public void OnUnlockWaitClaimBooster(ResourceType boosterType)
            {
                foreach (BoosterSelector selector in BoosterSelectors)
                {
                    if (selector.BoosterType != boosterType) continue;
                    selector.OnUnlockClaim();
                    break;
                }
            }

            public void ShowNotification(string message)
            {
                if (NotificationItemUIPrefab == null) return;
                NotificationItemUI notificationItem = GetNotificationItem();
                notificationItem.ShowNotification(message);
            }

            public void ShowBackGroundBlack()
            {
                if (BackGroundBlackImage == null) return;
                BackGroundBlackImage.color = new Color(0, 0, 0, 0);
                BackGroundBlackImage.gameObject.SetActive(true);
                if (blackGroundMotionHandle.IsActive()) blackGroundMotionHandle.TryCancel();
                blackGroundMotionHandle = LMotion.Create(0f, 0.8f, 0.3f)
                    .WithEase(Ease.OutQuad)
                    .Bind(x => BackGroundBlackImage.color = new Color(0, 0, 0, x));
            }

            public void HideBackGroundBlack()
            {
                if (BackGroundBlackImage == null) return;
                if (blackGroundMotionHandle.IsActive()) blackGroundMotionHandle.TryCancel();
                blackGroundMotionHandle = LMotion.Create(BackGroundBlackImage.color.a, 0f, 0.3f)
                    .WithEase(Ease.InQuad)
                    .WithOnComplete(() => BackGroundBlackImage.gameObject.SetActive(false))
                    .Bind(x => BackGroundBlackImage.color = new Color(0, 0, 0, x));
            }

            public void BoosterGroupSetActive(bool isActive)
            {
                if (BoosterGroup != null) BoosterGroup.SetActive(isActive);
            }

            public void UpdateTextWarning(int value)
            {
                if (TxtWarning != null) TxtWarning.text = value.ToString();
            }

            public void SetActivePanelWarning(bool isActive)
            { 
                if (IsActiveWarning == isActive) return;
                IsActiveWarning = isActive;
                PanelWarning.SetActive(isActive);
                if (isActive)
                {
                    AnimWarning.PlayFeedbacks();
                }
                else
                {
                    AnimWarning.StopFeedbacks();
                }
            }

            public void ShowInteractPanel()
            {
                StopPanelAnims();
                if (AnimShowBotPanel != null) AnimShowBotPanel.PlayFeedbacks();
                if (AnimShowTopPanel != null) AnimShowTopPanel.PlayFeedbacks();
            }

            public void HideInteractPanel()
            {
                StopPanelAnims();
                if (AnimHideBotPanel != null) AnimHideBotPanel.PlayFeedbacks();
                if (AnimHideTopPanel != null) AnimHideTopPanel.PlayFeedbacks();
            }

            /// <summary>MainView là CanvasGroup gốc của prefab, nút pause và thanh booster đều nằm dưới, không CanvasGroup con nào ignoreParentGroups.</summary>
            public void SetInteractable(bool value)
            {
                if (MainView != null) MainView.interactable = value;
            }

            private void StopPanelAnims()
            {
                if (AnimHideBotPanel != null) AnimHideBotPanel.StopFeedbacks();
                if (AnimShowBotPanel != null) AnimShowBotPanel.StopFeedbacks();
                if (AnimHideTopPanel != null) AnimHideTopPanel.StopFeedbacks();
                if (AnimShowTopPanel != null) AnimShowTopPanel.StopFeedbacks();
            }

            public void SetActiveFirstTut(bool isActive)
            {
            }

            public void SetHudPart(HudPart part, bool visible, bool animate)
            {
                GameObject target = part switch
                {
                    HudPart.Progress => ProgressRoot,
                    HudPart.BoosterBar => BoosterBarRoot,
                    _ => null
                };
                if (target == null) return;
                bool wasActive = target.activeSelf;
                target.SetActive(visible);
                if (!visible || !animate || wasActive) return;
                if (part == HudPart.BoosterBar && AnimShowBotPanel != null)
                {
                    // Trượt thanh dưới lên như lúc vào level.
                    StopPanelAnims();
                    AnimShowBotPanel.PlayFeedbacks();
                    return;
                }

                Transform t = target.transform;
                t.localScale = Vector3.zero;
                LMotion.Create(Vector3.zero, Vector3.one, 0.35f)
                    .WithEase(Ease.OutBack)
                    .Bind(x => { if (t != null) t.localScale = x; })
                    .AddTo(target);
            }

            public RectTransform GetHudPartRect(HudPart part) => part switch
            {
                HudPart.Progress => ProgressRoot != null ? ProgressRoot.transform as RectTransform : null,
                HudPart.BoosterBar => BoosterBarRoot != null ? BoosterBarRoot.transform as RectTransform : null,
                _ => null
            };

            public void OnLevelChanged(int level)
            {
                if (TextLevel == null) return;
                for (int i = 0; i < TextLevel.Length; i++)
                {
                    if (TextLevel[i] != null) TextLevel[i].SetText($"Level {level}");
                }
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

                Model.CurLevel.Subscribe(View.OnLevelChanged).AddTo(View.MainView);

                AudioManager.Instance.ChangeMusic(AudioKey.BgGamePlay, 0.5f);
                View.ButtonPause.SetOnClickDestination(OnClickButtonPause);
                Events.ShowBoosterHint += ShowBoosterHint;
                Events.HideBoosterHint += HideBoosterHint;
                Events.CheckShowAlertDifficult += CheckShowAlertDifficult;
                Events.ClaimBooster = View.ClaimBooster;
                Events.OnUnlockWaitClaimBooster = View.OnUnlockWaitClaimBooster;
                Events.ShowNotification = View.ShowNotification;
                Events.ShowBackGroundBlack = View.ShowBackGroundBlack;
                Events.HideBackGroundBlack = View.HideBackGroundBlack;
                Events.LoadLevel = LoadLevel;
                Events.BoosterGroupSetActive = View.BoosterGroupSetActive;
                Events.UpdateTxtWarning = View.UpdateTextWarning;
                Events.SetActiveWarning = View.SetActivePanelWarning;
                Events.ShowBotPanel = View.ShowInteractPanel;
                Events.HideBotPanel = View.HideInteractPanel;
                Events.SetActiveFirstTut = View.SetActiveFirstTut;
                Events.SetBlockObjectActive = View.SetBlockObjectActive;
                Events.UpdateProgress = View.UpdateProgress;
                Events.SetHudInteractable = View.SetInteractable;
                Events.SetHudPart = View.SetHudPart;
                Events.GetHudPartRect = View.GetHudPartRect;

                GameManager.Instance.IsPauseGame.Subscribe(CheckPauseGame).AddTo(View.MainView);

                GameGlobalEvent.OnPlayerSelectBoosterTarget += OnPlayerSelectBoosterTarget;
                GameGlobalEvent.OnCancelBoosterTarget += OnCancelBoosterTarget;
                GameGlobalEvent.OnPlayerUseBoosterCompleted += OnPlayerUseBoosterCompleted;
                GameGlobalEvent.OnProgressChanged += View.UpdateProgress;
            }

            public UniTask Cleanup(Memory<object> args)
            {
                Events.ShowBoosterHint -= ShowBoosterHint;
                Events.HideBoosterHint -= HideBoosterHint;
                Events.CheckShowAlertDifficult -= CheckShowAlertDifficult;
                Events.ClaimBooster = null;
                Events.OnUnlockWaitClaimBooster = null;
                Events.ShowNotification = null;
                Events.ShowBackGroundBlack = null;
                Events.HideBackGroundBlack = null;
                Events.LoadLevel = null;
                Events.BoosterGroupSetActive = null;
                Events.UpdateTxtWarning = null;
                Events.SetActiveWarning = null;
                Events.ShowBotPanel = null;
                Events.HideBotPanel = null;
                Events.SetActiveFirstTut = null;
                Events.SetBlockObjectActive = null;
                Events.UpdateProgress = null;
                Events.SetHudInteractable = null;
                Events.SetHudPart = null;
                Events.GetHudPartRect = null;

                GameGlobalEvent.OnPlayerSelectBoosterTarget -= OnPlayerSelectBoosterTarget;
                GameGlobalEvent.OnCancelBoosterTarget -= OnCancelBoosterTarget;
                GameGlobalEvent.OnPlayerUseBoosterCompleted -= OnPlayerUseBoosterCompleted;
                GameGlobalEvent.OnProgressChanged -= View.UpdateProgress;
                EventAdsManager.ShowBannerAds?.Invoke(false);
                return UniTask.CompletedTask;
            }

            private void CheckShowAlertDifficult()
            {
                if (LevelManager.Instance.LevelConfig == null || View.AlertLevelDifficult == null) return;
                LevelDifficulty type = LevelManager.Instance.LevelConfig.LevelType;
                if (type == LevelDifficulty.Hard || type == LevelDifficulty.SuperHard)
                {
                    View.AlertLevelDifficult.OnInit(type).TryStartMoveAnimation().Forget();
                }
            }

            private async UniTask OnClickButtonPause()
            {
                // Thắng đã chắc (PrepareWin) hoặc đã thua: không mở pause nữa; CanvasGroup đã khoá, đây là lưới thứ hai.
                if (!GameManager.Instance.IsState(GameState.InGame)) return;
                ViewOptions viewOptions = new ViewOptions(nameof(ModalPause));
                await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
            }

            public void ShowBoosterHint(ResourceType boosterType)
            {
                BoosterSelector selector = GetBoosterSelector(boosterType);
                if (selector != null) selector.SetSuggestion(true);
            }

            public void HideBoosterHint()
            {
                for (int i = 0; i < View.BoosterSelectors.Count; i++) View.BoosterSelectors[i].SetSuggestion(false);
            }

            private BoosterSelector GetBoosterSelector(ResourceType boosterType)
            {
                for (int i = 0; i < View.BoosterSelectors.Count; i++)
                {
                    if (View.BoosterSelectors[i].BoosterType == boosterType) return View.BoosterSelectors[i];
                }

                return null;
            }

            public void LoadLevel()
            {
                View.SetInteractable(true);   // màn trước khoá ở PrepareWin
                // Chốt banner một lần lúc vào level: thanh booster và banner theo cùng một quyết định,
                // config đổi giữa level thì level sau mới áp. Đặt vị trí trước khi thanh booster trượt lên.
                bool hasBanner = BannerRemoteConfigData.Instance.CanShow(ShopData.Instance.IsRemoveADs.Value);
                if (View.NoAdsSwitchPosition != null) View.NoAdsSwitchPosition.Apply(hasBanner);
                View.ShowInteractPanel();
                InitBoosterSelectors();
                // Level phát GameGlobalEvent.OnProgressChanged khi dựng bàn; ở đây chỉ xoá số của ván trước.
                View.UpdateProgress(0, 0);
                if (hasBanner) EventAdsManager.ShowBannerAds?.Invoke(true);
            }

            private void InitBoosterSelectors()
            {
                for (int i = 0; i < View.BoosterSelectors.Count; i++)
                {
                    View.BoosterSelectors[i].OnChangeLevel(GameManager.Instance.Level.Value);
                }
            }

            private void CheckPauseGame(bool pause)
            {
                if (View.BoosterBG != null) View.BoosterBG.alpha = pause ? 0.5f : 1f;
            }

            private void OnPlayerSelectBoosterTarget(ResourceType boosterType)
            {
                OpenTipBooster(boosterType).Forget();
                LevelManager.Instance.SetPause(true);
            }

            private async UniTask OpenTipBooster(ResourceType boosterType)
            {
                View.BoosterCG.interactable = false;
                ViewOptions activityTipBooster = new ViewOptions(nameof(ActivityTipBooster));
                await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(activityTipBooster, boosterType);
            }

            private async UniTask CloseTipBooster()
            {
                ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
                if (container.Activities.ContainsKey(nameof(ActivityTipBooster)))
                {
                    await container.HideAsync(nameof(ActivityTipBooster));
                }

                View.BoosterCG.interactable = true;
            }

            private void OnCancelBoosterTarget(ResourceType boosterType)
            {
                LevelManager.Instance.SetPause(false);
                CloseTipBooster().Forget();
            }

            private void OnPlayerUseBoosterCompleted(ResourceType boosterType)
            {
                LevelManager.Instance.SetPause(false);
                CloseTipBooster().Forget();
            }
        }
    }
}
