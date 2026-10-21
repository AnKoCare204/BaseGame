using System.Threading;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Profile.ProfileItem
{
    /// <summary>
    /// Controller quản lý animation cho profile item (Avatar/Frame/Badge).
    /// StartAndLoop: start → loop random (min..max) → lặp lại.
    /// Off-screen (ScrollRect viewport / màn hình): freeze Spine, dừng cycle để tiết kiệm CPU trên leaderboard.
    /// </summary>
    [RequireComponent(typeof(SkeletonGraphic))]
    public class ProfileAnimationController : MonoBehaviour
    {
        private const float VisibilityCheckInterval = 0.1f;

        [SerializeField] private SkeletonGraphic skeletonGraphic;
        [SerializeField] private AnimationConfig animationConfig;

        [Tooltip("Mở rộng vùng nhìn thấy (px screen). Anim chỉ tắt sau khi đã ra khỏi viewport hoàn toàn + padding này.")]
        [SerializeField] private float visibilityEdgePadding = 40f;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private ScrollRect _scrollRect;
        private CancellationTokenSource _cycleCts;
        private bool _isPlaybackActive;
        private bool _hasInitialized;
        private float _nextVisibilityCheckTime;
        private readonly Vector3[] _itemCorners = new Vector3[4];
        private readonly Vector3[] _viewportCorners = new Vector3[4];

        private void Awake()
        {
            if (skeletonGraphic == null)
            {
                skeletonGraphic = GetComponent<SkeletonGraphic>();
            }

            _rectTransform = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (_hasInitialized)
            {
                ScheduleVisibilityCheck(true);
                ApplyVisibility(IsVisible());
            }
        }

        private void OnDisable()
        {
            SetPlaybackActive(false);
        }

        private void OnDestroy()
        {
            CancelCycle();
        }

        private void LateUpdate()
        {
            if (!_hasInitialized || animationConfig == null || !animationConfig.HasAnimation())
            {
                return;
            }

            if (Time.unscaledTime < _nextVisibilityCheckTime)
            {
                return;
            }

            _nextVisibilityCheckTime = Time.unscaledTime + VisibilityCheckInterval;
            ApplyVisibility(IsVisible());
        }

        /// <summary>
        /// Khởi tạo animation với config
        /// </summary>
        public void Initialize(AnimationConfig config)
        {
            animationConfig = config;
            CancelCycle();
            _isPlaybackActive = false;
            _hasInitialized = false;

            if (animationConfig == null || !animationConfig.HasAnimation())
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (skeletonGraphic == null)
            {
                skeletonGraphic = GetComponent<SkeletonGraphic>();
            }

            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            CacheVisibilityRefs();
            animationConfig.InitializeAnimation(skeletonGraphic);
            _hasInitialized = true;

            // Stagger checks across many leaderboard cells.
            ScheduleVisibilityCheck(true);
            ApplyVisibility(IsVisible());
        }

        /// <summary>
        /// Reset animation
        /// </summary>
        public void Reset()
        {
            _hasInitialized = false;
            SetPlaybackActive(false);
            CancelCycle();

            if (skeletonGraphic != null)
            {
                skeletonGraphic.freeze = false;
                skeletonGraphic.enabled = true;
                skeletonGraphic.skeletonDataAsset = null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Set active state của animation
        /// </summary>
        public void SetActive(bool active)
        {
            if (gameObject == null)
            {
                return;
            }

            if (!active)
            {
                SetPlaybackActive(false);
            }

            gameObject.SetActive(active);

            if (active && _hasInitialized)
            {
                ScheduleVisibilityCheck(true);
                ApplyVisibility(IsVisible());
            }
        }

        /// <summary>
        /// Kiểm tra xem animation có đang active không
        /// </summary>
        public bool IsActive()
        {
            return gameObject != null && gameObject.activeSelf;
        }

        private void CacheVisibilityRefs()
        {
            _canvas = GetComponentInParent<Canvas>();
            _scrollRect = GetComponentInParent<ScrollRect>();
        }

        private void ScheduleVisibilityCheck(bool immediate)
        {
            // Random offset để tránh mọi cell check cùng frame.
            float offset = immediate ? Random.Range(0f, VisibilityCheckInterval) : VisibilityCheckInterval;
            _nextVisibilityCheckTime = Time.unscaledTime + offset;
        }

        private void ApplyVisibility(bool visible)
        {
            SetPlaybackActive(visible);
        }

        private void SetPlaybackActive(bool active)
        {
            if (_isPlaybackActive == active)
            {
                return;
            }

            _isPlaybackActive = active;

            if (skeletonGraphic == null)
            {
                return;
            }

            if (active)
            {
                skeletonGraphic.enabled = true;
                skeletonGraphic.freeze = false;
                StartCycle();
            }
            else
            {
                CancelCycle();
                skeletonGraphic.freeze = true;
                skeletonGraphic.enabled = false;
            }
        }

        private void StartCycle()
        {
            CancelCycle();

            if (animationConfig == null || skeletonGraphic == null)
            {
                return;
            }

            switch (animationConfig.animationType)
            {
                case AnimationType.LoopOnly:
                    if (!string.IsNullOrEmpty(animationConfig.loopAnimationName))
                    {
                        skeletonGraphic.AnimationState.SetAnimation(0, animationConfig.loopAnimationName, true);
                    }
                    break;

                case AnimationType.StartAndLoop:
                    _cycleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                    RunStartAndLoopCycle(_cycleCts.Token).Forget();
                    break;
            }
        }

        private void CancelCycle()
        {
            if (_cycleCts == null)
            {
                return;
            }

            _cycleCts.Cancel();
            _cycleCts.Dispose();
            _cycleCts = null;
        }

        private async UniTaskVoid RunStartAndLoopCycle(CancellationToken ct)
        {
            string startName = animationConfig.startAnimationName;
            string loopName = animationConfig.loopAnimationNameAfterStart;

            if (string.IsNullOrEmpty(startName) || string.IsNullOrEmpty(loopName))
            {
                return;
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var startEntry = skeletonGraphic.AnimationState.SetAnimation(0, startName, false);
                    float startDuration = startEntry != null
                        ? Mathf.Max(0.01f, startEntry.AnimationEnd / Mathf.Max(0.01f, startEntry.TimeScale))
                        : 0.5f;

                    bool canceled = await UniTask
                        .WaitForSeconds(startDuration, cancellationToken: ct)
                        .SuppressCancellationThrow();
                    if (canceled || ct.IsCancellationRequested)
                    {
                        return;
                    }

                    skeletonGraphic.AnimationState.SetAnimation(0, loopName, true);
                    float loopDuration = Mathf.Max(0.01f, animationConfig.GetRandomLoopDuration());

                    canceled = await UniTask
                        .WaitForSeconds(loopDuration, cancellationToken: ct)
                        .SuppressCancellationThrow();
                    if (canceled || ct.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when off-screen / destroy.
            }
        }

        private bool IsVisible()
        {
            if (_rectTransform == null || !isActiveAndEnabled)
            {
                return false;
            }

            Camera cam = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = _canvas.worldCamera;
            }

            _rectTransform.GetWorldCorners(_itemCorners);
            if (!TryGetScreenRect(_itemCorners, cam, out Rect itemScreen))
            {
                return false;
            }

            // Clip theo viewport ScrollRect nếu có (leaderboard list).
            if (_scrollRect != null && _scrollRect.viewport != null)
            {
                _scrollRect.viewport.GetWorldCorners(_viewportCorners);
                if (!TryGetScreenRect(_viewportCorners, cam, out Rect viewportScreen))
                {
                    return false;
                }

                return itemScreen.Overlaps(ExpandRect(viewportScreen, visibilityEdgePadding));
            }

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            return itemScreen.Overlaps(ExpandRect(screen, visibilityEdgePadding));
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            if (padding <= 0f)
            {
                return rect;
            }

            return Rect.MinMaxRect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.xMax + padding,
                rect.yMax + padding);
        }

        private static bool TryGetScreenRect(Vector3[] worldCorners, Camera cam, out Rect screenRect)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
                minX = Mathf.Min(minX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxX = Mathf.Max(maxX, screenPoint.x);
                maxY = Mathf.Max(maxY, screenPoint.y);
            }

            if (maxX < minX || maxY < minY)
            {
                screenRect = default;
                return false;
            }

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }
    }
}
