using System.Collections;
using TMPro;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Gắn vào mọi chỗ hiện tên người chơi (dòng leaderboard, roster team, popup
    /// profile, bong bóng chat). Nhận một id effect và lo phần còn lại: đổi style
    /// chữ, thuê view từ pool, đặt đúng thứ tự vẽ, trả lại nguyên trạng khi rebind.
    ///
    /// Toàn bộ trạng thái gốc của TMP đều được chụp lại trước khi ghi đè — cell
    /// trong list ảo hoá bị tái sử dụng, không trả lại thì effect của người này
    /// sẽ dính sang người khác.
    /// </summary>
    [DisallowMultipleComponent]
    public class NameEffectHost : MonoBehaviour
    {
        /// <summary>Id "không có effect nào" — dùng khi chưa ai gọi Apply.</summary>
        private const int NoId = int.MinValue;

        [SerializeField] private TMP_Text text;

        [Tooltip("Nơi gắn view. Bỏ trống = cha của text.")]
        [SerializeField] private RectTransform container;

        [Tooltip("Chất lượng mà CHỖ NÀY cần: popup profile để Full, dòng leaderboard để Medium hoặc Static.")]
        [SerializeField] private NameEffectQuality quality = NameEffectQuality.Full;

        [Tooltip("Cho phép effect đổi material/gradient của TMP.")]
        [SerializeField] private bool applyTextStyle = true;

        private int requestedId = NoId;
        private int builtId = NoId;

        private NameEffectView currentView;
        private NameEffectTextState savedTextState;

        private Transform savedTextParent;
        private int savedTextSiblingIndex;
        private bool textReparented;

        private Coroutine pendingBuild;

        private static bool budgetInitialized;

        public TMP_Text Text => text;

        public NameEffectQuality Quality
        {
            get => quality;
            set
            {
                if (quality == value)
                    return;

                quality = value;
                Rebuild();
            }
        }

        /// <summary>Cho prefab dựng text lúc runtime trỏ host về đúng TMP của mình.</summary>
        public void SetText(TMP_Text target)
        {
            if (text == target)
                return;

            Teardown();
            text = target;
            Rebuild();
        }

        /// <summary>Áp effect theo id server trả về. Gọi lại cùng id là no-op.</summary>
        public void Apply(int id)
        {
            if (requestedId == id && builtId == id)
            {
                // Cùng effect nhưng có thể là người chơi khác: view đang sống nên
                // không dựng lại, chỉ báo cho layer đồng bộ theo text mới.
                if (currentView != null)
                    currentView.NotifyTextChanged();

                return;
            }

            requestedId = id;

            if (isActiveAndEnabled)
                Build(id);
        }

        /// <summary>Gỡ effect, trả text về nguyên trạng.</summary>
        public void Clear()
        {
            requestedId = NoId;
            Teardown();
        }

        private void OnEnable()
        {
            // Build có SetParent (thuê view từ pool, hoặc nhấc text vào slot của
            // prefab) mà Unity cấm đổi cha trong lúc GameObject đang được bật/tắt
            // — hoãn sang frame sau, khi pha kích hoạt đã xong.
            if (requestedId != NoId && requestedId != builtId)
                ScheduleBuild();
        }

        /// <summary>
        /// Cố tình KHÔNG gỡ view ở đây: gỡ là SetParent giữa lúc Unity đang tắt
        /// GameObject → lỗi "Cannot set the parent ... while activating or
        /// deactivating". View cứ nằm lại cùng cell và tắt theo cell, không tốn gì;
        /// slot animation đã được NameEffectView tự nhả trong OnDisable của nó.
        /// Chỉ dọn tay cầm coroutine, vì tắt GameObject là coroutine chết theo.
        /// </summary>
        private void OnDisable()
        {
            pendingBuild = null;
        }

        private void ScheduleBuild()
        {
            if (pendingBuild != null)
                return;

            pendingBuild = StartCoroutine(BuildAfterActivation());
        }

        private IEnumerator BuildAfterActivation()
        {
            yield return null;

            pendingBuild = null;

            if (isActiveAndEnabled && requestedId != NoId && requestedId != builtId)
                Build(requestedId);
        }

        private void Rebuild()
        {
            if (requestedId == NoId)
                return;

            Teardown();

            if (isActiveAndEnabled)
                Build(requestedId);
        }

        private void Build(int id)
        {
            Teardown();

            if (text == null)
                return;

            NameEffectDatabase database = NameEffectDatabase.Instance;
            if (database == null)
                return;

            if (!budgetInitialized)
            {
                NameEffectBudget.MaxConcurrent = database.MaxConcurrentAnimated;
                budgetInitialized = true;
            }

            NameEffectDefinition definition = database.Get(id);
            builtId = id;

            if (definition == null)
                return;

            if (applyTextStyle && definition.HasTextStyle)
            {
                savedTextState = NameEffectTextState.Capture(text);
                definition.TextStyle.Apply(text);
            }

            if (definition.ViewPrefab == null)
                return;

            NameEffectPool pool = NameEffectPool.Instance;
            if (pool == null)
                return;

            RectTransform parent = ResolveContainer();
            if (parent == null)
                return;

            currentView = pool.Rent(definition.ViewPrefab, parent);
            if (currentView == null)
                return;

            PlaceView(currentView, parent);
            currentView.Bind(text, definition.ResolveQuality(quality), definition.Rarity);
        }

        private void Teardown()
        {
            if (currentView != null)
            {
                RestoreText();

                // Teardown chạy cả lúc scene bị huỷ — dùng instance sẵn có, không tạo mới.
                NameEffectPool pool = NameEffectPool.ExistingInstance;
                if (pool == null || !pool.Release(currentView))
                {
                    currentView.ReleaseView();
                    Destroy(currentView.gameObject);
                }

                currentView = null;
            }

            savedTextState.Restore(text);
            savedTextState = default;
            builtId = NoId;
        }

        private RectTransform ResolveContainer()
        {
            if (container != null)
                return container;

            return text.rectTransform.parent as RectTransform;
        }

        private void PlaceView(NameEffectView view, RectTransform parent)
        {
            RectTransform viewRect = view.RectTransform;
            if (viewRect == null)
                return;

            viewRect.localScale = Vector3.one;
            viewRect.localRotation = Quaternion.identity;

            if (view.Placement == NameEffectPlacement.WrapText)
            {
                RectTransform slot = view.TextSlot != null ? view.TextSlot : viewRect;

                savedTextParent = text.rectTransform.parent;
                savedTextSiblingIndex = text.rectTransform.GetSiblingIndex();
                textReparented = true;

                // worldPositionStays = false: text giữ nguyên anchor/offset của nó,
                // nên slot trong prefab phải stretch đúng bằng rect gốc của text.
                text.rectTransform.SetParent(slot, false);
                return;
            }

            bool sameParent = text.rectTransform.parent == parent;
            int textIndex = sameParent ? text.rectTransform.GetSiblingIndex() : parent.childCount - 1;

            viewRect.SetSiblingIndex(view.Placement == NameEffectPlacement.Behind
                ? textIndex
                : textIndex + 1);

            if (view.MatchTextRect && sameParent)
                CopyRect(text.rectTransform, viewRect);
        }

        private void RestoreText()
        {
            if (!textReparented)
                return;

            textReparented = false;

            if (text == null)
                return;

            if (savedTextParent != null)
            {
                text.rectTransform.SetParent(savedTextParent, false);
                text.rectTransform.SetSiblingIndex(savedTextSiblingIndex);
            }

            savedTextParent = null;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
        }

        private void Reset()
        {
            text = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
