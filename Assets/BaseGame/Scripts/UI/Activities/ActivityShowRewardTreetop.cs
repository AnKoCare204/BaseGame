using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Activities;
using CoreData;
using Core.Manager;

namespace Core.UI.Activities
{
    public class ActivityShowRewardTreetop : Activity
    {
        [field: SerializeField] public ActivityShowRewardTreetopContext.UIPresenter UIPresenter { get; private set; }

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
    public class ActivityShowRewardTreetopContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }
            public static Action CloseActivity { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }
            [field: SerializeField]
            public List<GameResource> Rewards { get; private set; } = new();
            [field: SerializeField]
            public Vector3 TfRootPosition { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                Rewards = args.Span[0] as List<GameResource>;
                TfRootPosition = args.Span[1] as Vector3? ?? Vector3.zero;
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
            public GameObject GORewardClaimed { get; private set; }
            [field: SerializeField]
            public RectTransform RectGOClaimed { get; private set; }
            [field: SerializeField]
            public RectTransform ImgStartBubbleClaimed { get; private set; }

            [field: SerializeField]
            public GameObject GOReward3 {get; private set;}
            [field: SerializeField]
            public UIItemInfo[] UIItem3 { get; private set; }
            [field: SerializeField]
            public RectTransform RectGOReward3 { get; private set; }
            [field: SerializeField]
            public RectTransform ImgStartBubble3 { get; private set; }

            [field: SerializeField]
            public GameObject GOReward6 {get; private set;}
            [field: SerializeField]
            public UIItemInfo[] UIItem6 { get; private set; }
            [field: SerializeField]
            public RectTransform RectGOReward6 { get; private set; }
            [field: SerializeField]
            public RectTransform ImgStartBubble6 { get; private set; }
            
            [field: SerializeField]
            public GameObject GOReward8 {get; private set;}
            [field: SerializeField]
            public UIItemInfo[] UIItem8 { get; private set; }
            [field: SerializeField]
            public RectTransform RectGOReward8 { get; private set; }
            [field: SerializeField]
            public RectTransform ImgStartBubble8 { get; private set; }

            [field: SerializeField]
            public Transform Root {get; private set;}
            // Keep the bubble this far inside the screen edges when clamped
            [field: SerializeField]
            public float ScreenEdgePadding { get; private set; } = 24f;

            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IActivityLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            [field: SerializeField] public MMF_Player AnimGOClaimed { get; private set; }
            [field: SerializeField] public MMF_Player AnimGOReward3 { get; private set; }
            [field: SerializeField] public MMF_Player AnimGOReward6 { get; private set; }
            [field: SerializeField] public MMF_Player AnimGOReward8 { get; private set; }
            [field: SerializeField] public RectTransform CurRectTransform { get; private set; }
            private RectTransform CurBubbleTriangle { get; set; }
            private R3Input R3Input => InputManager.Instance.R3Input;
            
            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);

                View.GORewardClaimed.SetActive(false);
                View.GOReward3.SetActive(false);
                View.GOReward6.SetActive(false);
                View.GOReward8.SetActive(false);

                InitUI();
                
                Events.CloseActivity = CloseActivity;
                
                View.MainView.UpdateAsObservable()
                    .Where(R3Input.WasPressedThisFrame)
                    .Subscribe(OnPointerDown)
                    .AddTo(View.MainView);
            }

            private void OnPointerDown(Unit _)
            {
                Vector3 pointerPosition = R3Input.PointerPosition;
                if (CameraManager.Instance.RectangleContainsScreenPoint(CurRectTransform, pointerPosition)) return;
                CloseActivity();
            }

            public void DidEnter(Memory<object> args)
            {
                PlayAnimClaimed();
                PlayAnimReward3();
                PlayAnimReward6();
                PlayAbunReward8();
            }
            public UniTask Cleanup(Memory<object> args)
            {
                Events.CloseActivity = null;
                return UniTask.CompletedTask;
            }

            private void InitUI()
            {
                View.Root.transform.position = Model.TfRootPosition;
                SetClaimed();
                SetReward3();
                SetReward6();
                SetReward8();
                ClampBubbleToScreen();
            }

           

            /// <summary>
            /// The bubble Root is pinned to the reward source, so near a screen edge the bubble
            /// body overflows. Shift the active container back inside the screen, then counter-shift
            /// its ImgStartBubble triangle (a child of the container) so the tail still points at the source.
            /// </summary>
            private void ClampBubbleToScreen()
            {
                RectTransform body = CurRectTransform;
                if (body == null) return;

                Canvas canvas = body.GetComponentInParent<Canvas>();
                if (canvas == null) return;
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

                // The DidEnter scale anim may have pre-set the container to scale 0; measuring or doing
                // local-space math on it then collapses the rect to a point. Keep full scale through
                // the whole clamp, restore at the very end (anchoredPosition edits persist regardless).
                Vector3 originalScale = body.localScale;
                body.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();

                Vector3[] corners = new Vector3[4];
                body.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
                Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

                float pad = View.ScreenEdgePadding;
                float dxScreen = 0f;
                if (screenMin.x < pad) dxScreen = pad - screenMin.x;
                else if (screenMax.x > Screen.width - pad) dxScreen = (Screen.width - pad) - screenMax.x;
                float dyScreen = 0f;
                if (screenMin.y < pad) dyScreen = pad - screenMin.y;
                else if (screenMax.y > Screen.height - pad) dyScreen = (Screen.height - pad) - screenMax.y;

                if (Mathf.Approximately(dxScreen, 0f) && Mathf.Approximately(dyScreen, 0f))
                {
                    body.localScale = originalScale;
                    return;
                }

                // Move the body by the overflow (in the body's parent local space)
                Vector2 bodyDelta = ScreenDeltaToLocal(body.parent as RectTransform, cam, dxScreen, dyScreen);
                body.anchoredPosition += bodyDelta;

                // Counter-move the triangle (child of the body) so the tail still points at the source.
                // It is a fixed bottom/side tail: slide only horizontally along the bubble edge,
                // keep its authored Y so it stays attached. Factor its localScale into the width.
                RectTransform triangle = CurBubbleTriangle;
                if (triangle != null)
                {
                    Vector2 triDelta = ScreenDeltaToLocal(triangle.parent as RectTransform, cam, dxScreen, dyScreen);
                    float triHalfWidth = triangle.rect.width * Mathf.Abs(triangle.localScale.x) * 0.5f;
                    float limitX = Mathf.Max(0f, body.rect.width * 0.5f - triHalfWidth);
                    Vector2 triPos = triangle.anchoredPosition;
                    triPos.x = Mathf.Clamp(triPos.x - triDelta.x, -limitX, limitX);
                    triangle.anchoredPosition = triPos; // Y unchanged: tail stays on the bubble edge
                }

                body.localScale = originalScale;
            }

            private static Vector2 ScreenDeltaToLocal(RectTransform space, Camera cam, float dxScreen, float dyScreen)
            {
                if (space == null) return Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(space, Vector2.zero, cam, out Vector2 p0);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(space, new Vector2(dxScreen, dyScreen), cam, out Vector2 p1);
                return p1 - p0;
            }

            private void SetClaimed()
            {
                if (Model.Rewards.Count > 0) return;
                View.GORewardClaimed.SetActive(true);
                CurRectTransform = View.RectGOClaimed;
                CurBubbleTriangle = View.ImgStartBubbleClaimed;
            }

            private void PlayAnimClaimed()
            {
                if (Model.Rewards.Count > 0) return;
                AnimGOClaimed.PlayFeedbacks();
            }

            private void SetReward3()
            {
                if (Model.Rewards.Count is <= 0 or > 3) return;
                View.GOReward3.SetActive(true);
                for (var i = 0; i < View.UIItem3.Length; i++)
                {
                    if (i >= Model.Rewards.Count)
                    {
                        View.UIItem3[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        View.UIItem3[i].gameObject.SetActive(true);
                        View.UIItem3[i].Init(Model.Rewards[i]);
                    }
                }
                CurRectTransform = View.RectGOReward3;
                CurBubbleTriangle = View.ImgStartBubble3;
            }
            private void PlayAnimReward3()
            {
                if (Model.Rewards.Count is <= 0 or > 3) return;
                AnimGOReward3.PlayFeedbacks();
            }

            private void SetReward6()
            {
                if (Model.Rewards.Count is <= 3 or > 6) return;
                View.GOReward6.SetActive(true);
                for (var i = 0; i < View.UIItem6.Length; i++)
                {
                    if (i >= Model.Rewards.Count)
                    {
                        View.UIItem6[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        View.UIItem6[i].gameObject.SetActive(true);
                        View.UIItem6[i].Init(Model.Rewards[i]);
                    }
                }
                CurRectTransform = View.RectGOReward6;
                CurBubbleTriangle = View.ImgStartBubble6;
            }
            private void PlayAnimReward6()
            {
                if (Model.Rewards.Count is <= 3 or > 6) return;
                AnimGOReward6.PlayFeedbacks();
            }
  
            private void SetReward8()
            {
                if (Model.Rewards.Count is <= 6 or > 8) return;
                View.GOReward8.SetActive(true);
                for (var i = 0; i < View.UIItem8.Length; i++)
                {
                    if (i >= Model.Rewards.Count)
                    {
                        View.UIItem8[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        View.UIItem8[i].gameObject.SetActive(true);
                        View.UIItem8[i].Init(Model.Rewards[i]);
                    }
                }
                CurRectTransform = View.RectGOReward8;
                CurBubbleTriangle = View.ImgStartBubble8;
            }
            private void PlayAbunReward8()
            {
                if (Model.Rewards.Count is <= 6 or > 8) return;
                AnimGOReward8.PlayFeedbacks();
            } 

            private void CloseActivity()
            {
                ActivityContainer.Find(ContainerKey.Activities).Hide(nameof(ActivityShowRewardTreetop));
            }
        }
    }
}