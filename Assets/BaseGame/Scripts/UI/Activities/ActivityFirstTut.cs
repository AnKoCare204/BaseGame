using System;
using System.Collections.Generic;
using Core.Manager;
using Core.Tutorial;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Activities
{
    /// <summary>Khung chữ (mascot + bóng thoại) của tutorial, đặt theo vị trí world; TutorialStage điều khiển qua Events.</summary>
    public class ActivityFirstTut : Activity
    {
        [field: SerializeField] public ActivityFirstTutContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }
    }

    [Serializable]
    public class ActivityFirstTutContext
    {
        public static class Events
        {
            public static Action CloseActivity { get; set; }
            public static Action HideGuide { get; set; }

            /// <summary>(chữ, phần đang sáng, phần cần tránh — pixel màn hình, chặn bấm + hiện "Tap to continue"). Chỗ đặt: GuidePlacement.</summary>
            public static Action<string, IReadOnlyList<Rect>, IReadOnlyList<Rect>, bool> AppearGuide { get; set; }

            /// <summary>Người chơi chạm vùng "Tap to continue" khi khung đang chặn bấm.</summary>
            public static event Action TapContinued;

            internal static void RaiseTapContinued() => TapContinued?.Invoke();
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            public UniTask Initialize(Memory<object> args) => UniTask.CompletedTask;
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField] public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TextHint { get; private set; }
            [field: SerializeField] public RectTransform GuideRoot { get; private set; }
            [field: SerializeField] public RectTransform GuideBox { get; private set; }
            [field: SerializeField] public float GuideBelowPadding { get; private set; } = 48f;
            [field: SerializeField] public float GuideScreenPadding { get; private set; } = 24f;
            [field: SerializeField] public GameObject TapToContinue { get; private set; }
            [field: SerializeField, Tooltip("Linh vật trong GuideRoot (tut_ava); lật sang phải khi khung kẹp mép phải")]
            public RectTransform Mascot { get; private set; }

            public UniTask Initialize(Memory<object> args) => UniTask.CompletedTask;
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IActivityLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();

            [field: SerializeField] private Button closeButton;
            private Vector2 initialGuidePos;
            private bool hasInitialGuidePos;
            private float mascotX, bubbleX;
            private bool mascotLayoutCached;
            private float guideShownAt;   // lúc khung chặn bấm hiện (Time.unscaledTime), cho GuideTapGate

            private RectTransform GuideRoot => View.GuideRoot != null ? View.GuideRoot : View.GuideBox;

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                CacheGuidePos();
                Events.CloseActivity = OnClose;
                Events.HideGuide = HideGuide;
                Events.AppearGuide = AppearGuide;
                if (closeButton != null)
                {
                    closeButton.gameObject.SetActive(true);
                    closeButton.SetOnClickDestination(OnCloseButtonClicked);
                }

                HideGuide();
            }

            public UniTask Cleanup(Memory<object> args)
            {
                RestoreGuideVisuals();
                SetTapToContinueVisible(false);
                SetCloseButtonInteractable(false);
                Events.CloseActivity = null;
                Events.HideGuide = null;
                Events.AppearGuide = null;
                SetMascotOnRight(false);
                return UniTask.CompletedTask;
            }

            private void HideGuide()
            {
                SetCloseButtonInteractable(false);
                SetBlocksRaycasts(false);
                SetTapToContinueVisible(false);
                RectTransform root = GuideRoot;
                if (root != null) root.gameObject.SetActive(false);
            }

            private void AppearGuide(string text, IReadOnlyList<Rect> lit, IReadOnlyList<Rect> avoid, bool blocking)
            {
                RectTransform box = View.GuideBox;
                RectTransform root = GuideRoot;
                if (box == null) return;
                CacheGuidePos();
                if (root != null) root.gameObject.SetActive(true);
                if (View.TextHint != null && !string.IsNullOrEmpty(text)) View.TextHint.text = text;
                box.localScale = Vector3.one;
                PlaceGuide(lit, avoid);
                guideShownAt = Time.unscaledTime;
                SetTapToContinueVisible(blocking);
                SetBlocksRaycasts(blocking);
                SetCloseButtonInteractable(blocking);
            }

            /// <summary>Đổi phần sáng / cần tránh sang toạ độ của cha GuideRoot, GuidePlacement chọn chỗ, dời GuideRoot tới đó, lật linh vật nếu cần.</summary>
            private void PlaceGuide(IReadOnlyList<Rect> litScreen, IReadOnlyList<Rect> avoidScreen)
            {
                RectTransform root = GuideRoot;
                RectTransform parent = root != null ? root.parent as RectTransform : null;
                if (root == null || parent == null) return;

                Canvas.ForceUpdateCanvases();
                List<Rect> lit = ToLocal(litScreen, parent);
                List<Rect> avoid = ToLocal(avoidScreen, parent);
                Rect area = parent.rect;
                float pad = View.GuideScreenPadding;
                var safe = new Rect(area.xMin + pad, area.yMin + pad, Mathf.Max(0f, area.width - pad * 2f), Mathf.Max(0f, area.height - pad * 2f));
                GetLocalMinMax(root, parent, out Vector2 min, out Vector2 max);
                GuidePlacementResult placement = GuidePlacement.Choose(safe, max - min, lit, avoid, View.GuideBelowPadding);
                root.anchoredPosition += placement.Rect.center - (min + max) * 0.5f;
                SetMascotOnRight(placement.MascotOnRight);
            }

            /// <summary>Pixel màn hình → toạ độ local của parent (Launcher là ScreenSpaceOverlay nên camera null).</summary>
            private static List<Rect> ToLocal(IReadOnlyList<Rect> screenRects, RectTransform parent)
            {
                var list = new List<Rect>();
                if (screenRects == null) return list;
                foreach (Rect r in screenRects)
                {
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, r.min, null, out Vector2 a)) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, r.max, null, out Vector2 b)) continue;
                    list.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
                }

                return list;
            }

            /// <summary>Linh vật bên trái (mặc định) hay bên phải bóng thoại: đổi bên quanh tâm GuideRoot và soi gương (GuideFlip).</summary>
            private void SetMascotOnRight(bool right)
            {
                RectTransform mascot = View.Mascot;
                RectTransform bubble = View.GuideBox;
                if (mascot == null || bubble == null || bubble == GuideRoot) return;
                if (!mascotLayoutCached)
                {
                    mascotX = mascot.localPosition.x;
                    bubbleX = bubble.localPosition.x;
                    mascotLayoutCached = true;
                }

                GuideFlip.Apply(mascot, bubble, View.TextHint != null ? View.TextHint.transform : null, mascotX, bubbleX, right);
            }

            private static void GetLocalMinMax(RectTransform rect, RectTransform parent, out Vector2 min, out Vector2 max)
            {
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                min = new Vector2(float.MaxValue, float.MaxValue);
                max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = parent.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }

            private void SetTapToContinueVisible(bool visible)
            {
                if (View.TapToContinue != null) View.TapToContinue.SetActive(visible);
            }

            private void SetCloseButtonInteractable(bool interactable)
            {
                if (closeButton != null) closeButton.interactable = interactable;
            }

            private void OnCloseButtonClicked()
            {
                if (!GuideTapGate.Accepts(guideShownAt, Time.unscaledTime)) return;   // chạm thứ hai của cú chạm đúp đóng câu trước
                SetCloseButtonInteractable(false);
                SetBlocksRaycasts(false);
                SetTapToContinueVisible(false);
                Events.RaiseTapContinued();
            }

            private void CacheGuidePos()
            {
                RectTransform root = GuideRoot;
                if (hasInitialGuidePos || root == null) return;
                initialGuidePos = root.anchoredPosition;
                hasInitialGuidePos = true;
            }

            private void RestoreGuideVisuals()
            {
                RectTransform root = GuideRoot;
                if (root != null)
                {
                    if (hasInitialGuidePos) root.anchoredPosition = initialGuidePos;
                    root.localScale = Vector3.one;
                }

                if (View.GuideBox != null) View.GuideBox.localScale = Vector3.one;
            }

            private void SetBlocksRaycasts(bool blocks)
            {
                if (View.MainView == null) return;
                View.MainView.blocksRaycasts = blocks;
                View.MainView.interactable = blocks;
            }

            private void OnClose()
            {
                ActivityContainer.Find(ContainerKey.Activities).Hide(nameof(ActivityFirstTut));
            }
        }
    }
}
