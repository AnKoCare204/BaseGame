using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DataModule;
using LitMotion;
using MoreMountains.Feedbacks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using Sirenix.OdinInspector;
using Spine.Unity;
using TW.UGUI.Core.Modals;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Core.UI.Modals
{
    public class ModalStarBlazeInfo : Modal
    {
        [field: SerializeField] public ModalStreakInfoContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
            AddLifecycleEvent(new MainMenuPopupLifecycle(), 2);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ModalStreakInfoContext
    {
        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
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

            [field: SerializeField] public Button ButtonShowInfo { get; set; }
            [field: SerializeField] public Button ButtonHideInfo { get; set; }
            [field: SerializeField] public Button ButtonClose { get; set; }
            [field: SerializeField] public UIStreakStageInfo UIStreakStageInfo { get; set; }
            [field: SerializeField] public Transform UIStreakStageInfoContainer { get; set; }
            [field: SerializeField] public MMF_Player AnimShow { get; private set; }
            [field: SerializeField] public MMF_Player AnimHide { get; private set; }
            [field: SerializeField] public RectTransform RectContent { get; private set; }
            [field: SerializeField] public RectTransform ViewContent { get; private set; }
            [field: SerializeField] public ScrollRect ScrollRect { get; private set; }
            [field: SerializeField] public RectTransform CurrentStageTarget { get; set; }
            // Vị trí trong modal mà CurrentGroup sẽ được cuộn tới khi mở. Null -> giữa viewport.
            [field: SerializeField] public RectTransform CurrentStageAnchor { get; private set; }
            [field: SerializeField] public float ScrollToCurrentDuration { get; private set; } = 1f;
            // Bắt đầu cuộn từ stage cách current bao nhiêu stage về phía trước.
            [field: SerializeField] public int ScrollFromPreviousCount { get; private set; } = 3;
            // Đặt ở sibling đầu của container (Ignore Layout) để luôn vẽ dưới tất cả UIStreakStageInfo.
            [field: SerializeField] public RectTransform LightSelect { get; private set; }
            [field: SerializeField] public Transform Top { get; private set; }
            [field: SerializeField] public Transform Bottom { get; private set; }
            [field: SerializeField] public SkeletonGraphic SkeletonFish { get; private set; }
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
            [field: SerializeField] public AnimationCurve CurveScroll { get; set; } = new();
            private UIStreakStageInfo CurrentStageInfo { get; set; }
            private List<UIStreakStageInfo> StageInfos { get; } = new();
            private MotionHandle scrollMotionHandle;

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                View.ButtonClose.SetOnClickDestination(OnButtonCloseClick);
                View.ButtonShowInfo.SetOnClickDestination(OnButtonShowInfoClick);
                View.ButtonHideInfo.SetOnClickDestination(OnButtonHideInfoClick);

                InitStreakInfo();
                // Item vừa Instantiate chưa được layout, phải rebuild trước khi đọc vị trí.
                LayoutRebuilder.ForceRebuildLayoutImmediate(View.RectContent);
                UpdateLightSelect();
                ForceMoveScrollToCurrentStage();
                PlayFishAnim();
                CheckShowTut();
            }

            // Chạy "start" một lần, xong thì chuyển sang "idle" loop.
            private void PlayFishAnim()
            {
                if (View.SkeletonFish == null) return;
                View.SkeletonFish.Initialize(false);
                View.SkeletonFish.AnimationState.SetAnimation(0, "start", false);
                View.SkeletonFish.AnimationState.AddAnimation(0, "idle", true, 0f);
            }

            private void CheckShowTut()
            {
                if (!StarBlazeData.Instance.IsShowTut)
                {
                    OnButtonShowInfoClick().Forget();
                    StarBlazeData.Instance.IsShowTut = true;
                    StarBlazeData.Instance.SaveData();
                }
            }

            private void InitStreakInfo()
            {
                for (var index = 0;
                     index < StarBlazeGlobalConfig.Instance.StarBlazeConfig
                         .StageConfigs.Length;
                     index++)
                {
                    var starBlazeStageConfig = StarBlazeGlobalConfig.Instance.StarBlazeConfig
                        .StageConfigs[index];
                    UIStreakStageInfo uiStreakStageInfo =
                        Object.Instantiate(View.UIStreakStageInfo, View.UIStreakStageInfoContainer);
                    uiStreakStageInfo.Init(starBlazeStageConfig);
                    StageInfos.Add(uiStreakStageInfo);
                    if (uiStreakStageInfo.IsUnlock || uiStreakStageInfo.IsCurrent)
                    {
                        View.CurrentStageTarget = (RectTransform)uiStreakStageInfo.transform;
                    }

                    if (uiStreakStageInfo.IsCurrent)
                    {
                        CurrentStageInfo = uiStreakStageInfo;
                    }

                    if (index == 0)
                    {
                        View.CurrentStageTarget = (RectTransform)uiStreakStageInfo.transform;
                    }
                }
                
            }
            
            [Button]
            private void ForceMoveScrollToCurrentStage()
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(View.RectContent);
                scrollMotionHandle.TryCancel();
                View.ScrollRect.StopMovement();

                // Ưu tiên stage current; hết event thì về stage unlock cuối.
                int targetIndex = CurrentStageInfo != null
                    ? StageInfos.IndexOf(CurrentStageInfo)
                    : StageInfos.FindIndex(x => x.transform == View.CurrentStageTarget);
                if (targetIndex < 0) return;
                if (!TryGetNormalizedToAnchor(GetScrollPoint(StageInfos[targetIndex]), out float targetNormalized)) return;

                // Bắt đầu từ stage trước current ScrollFromPreviousCount stage (dùng cùng điểm để offset khớp).
                int startIndex = Mathf.Max(0, targetIndex - View.ScrollFromPreviousCount);
                TryGetNormalizedToAnchor(GetScrollPoint(StageInfos[startIndex]), out float startNormalized);

                View.ScrollRect.verticalNormalizedPosition = startNormalized;
                if (Mathf.Approximately(startNormalized, targetNormalized) || View.ScrollToCurrentDuration <= 0f)
                {
                    View.ScrollRect.verticalNormalizedPosition = targetNormalized;
                    return;
                }

                scrollMotionHandle = LMotion.Create(startNormalized, targetNormalized, View.ScrollToCurrentDuration)
                    .WithEase(CurveScroll)
                    .Bind(x => View.ScrollRect.verticalNormalizedPosition = x)
                    .AddTo(View.ScrollRect);
            }

            // Có stage current thì canh theo CurrentGroup, không thì theo cả item.
            private RectTransform GetScrollPoint(UIStreakStageInfo stageInfo)
            {
                return CurrentStageInfo != null ? stageInfo.CurrentGroupRect : (RectTransform)stageInfo.transform;
            }

            // Tính verticalNormalizedPosition để tâm target trùng tâm CurrentStageAnchor (null -> ViewContent).
            private bool TryGetNormalizedToAnchor(RectTransform target, out float normalized)
            {
                normalized = View.ScrollRect.verticalNormalizedPosition;
                // Normalized position của ScrollRect tính theo viewport thật của nó.
                RectTransform viewport = View.ScrollRect.viewport != null
                    ? View.ScrollRect.viewport
                    : (RectTransform)View.ScrollRect.transform;

                // Tính toàn bộ trong không gian viewport để không bị ảnh hưởng bởi scale của modal.
                float contentHeight = GetHeightInSpace(View.RectContent, viewport);
                float scrollableHeight = contentHeight - viewport.rect.height;
                if (scrollableHeight <= 0) return false;

                RectTransform anchor = View.CurrentStageAnchor != null ? View.CurrentStageAnchor : View.ViewContent;
                float targetY = viewport.InverseTransformPoint(GetWorldCenter(target)).y;
                float anchorY = viewport.InverseTransformPoint(GetWorldCenter(anchor)).y;

                // Content dịch lên (anchorY - targetY) => normalized giảm tương ứng (0 = đáy, 1 = đỉnh).
                float delta = anchorY - targetY;
                normalized = Mathf.Clamp01(normalized - delta / scrollableHeight);
                return true;
            }

            private static Vector3 GetWorldCenter(RectTransform rectTransform)
            {
                return rectTransform.TransformPoint(rectTransform.rect.center);
            }

            private static float GetHeightInSpace(RectTransform rectTransform, RectTransform space)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                return space.InverseTransformPoint(corners[1]).y - space.InverseTransformPoint(corners[0]).y;
            }

            // Gọi sau khi layout đã rebuild: copy rect của LightAnchor (stage current) sang LightSelect.
            private void UpdateLightSelect()
            {
                RectTransform lightSelect = View.LightSelect;
                if (lightSelect == null) return;
                if (CurrentStageInfo == null || CurrentStageInfo.LightAnchor == null)
                {
                    lightSelect.gameObject.SetActive(false);
                    return;
                }

                lightSelect.gameObject.SetActive(true);
                lightSelect.SetAsFirstSibling();

                RectTransform parent = (RectTransform)lightSelect.parent;
                Vector3[] corners = new Vector3[4];
                CurrentStageInfo.LightAnchor.GetWorldCorners(corners);
                Vector2 min = parent.InverseTransformPoint(corners[0]);
                Vector2 max = parent.InverseTransformPoint(corners[2]);

                lightSelect.anchorMin = lightSelect.anchorMax = parent.pivot;
                lightSelect.pivot = new Vector2(0.5f, 0.5f);
                lightSelect.sizeDelta = max - min;
                lightSelect.localPosition = (min + max) * 0.5f;
            }

            private async UniTask OnButtonCloseClick()
            {
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            private async UniTask OnButtonShowInfoClick()
            {
                View.AnimShow.PlayFeedbacks();
                await UniTask.Delay(500, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
            }

            private async UniTask OnButtonHideInfoClick()
            {
                View.AnimHide.PlayFeedbacks();
                await UniTask.Delay(500, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
            }
        }
    }
}