using System;
using TMPro;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Gốc của một prefab effect. Nó chỉ làm ba việc: quyết định layer nào được
    /// chạy ở chất lượng hiện tại, xin/nhả slot trong <see cref="NameEffectBudget"/>,
    /// và bơm deltaTime xuống các layer bằng MỘT Update duy nhất.
    ///
    /// Không đụng tới style chữ — phần đó do <see cref="NameEffectHost"/> lo, để
    /// effect chỉ-đổi-style vẫn chạy được mà không cần prefab nào.
    /// </summary>
    [DisallowMultipleComponent]
    public class NameEffectView : MonoBehaviour
    {
        [SerializeField] private NameEffectPlacement placement = NameEffectPlacement.InFront;

        [Tooltip("Chỉ dùng khi Placement = WrapText: nơi host nhấc TMP text vào, để prefab tự xếp layer trước/sau chữ.")]
        [SerializeField] private RectTransform textSlot;

        [Tooltip("Kéo rect của view khớp rect của text khi cùng cha. Tắt nếu prefab tự neo.")]
        [SerializeField] private bool matchTextRect = true;

        [Tooltip("Chạy theo unscaledDeltaTime để effect không đứng hình khi game pause timeScale.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Tooltip("Tự điền từ các con khi Reset/OnValidate.")]
        [SerializeField] private NameEffectLayer[] layers = Array.Empty<NameEffectLayer>();

        private RectTransform cachedRect;
        private TMP_Text boundText;
        private NameEffectQuality quality = NameEffectQuality.Static;
        private bool bound;
        private bool holdsSlot;
        private bool hasPlayingLayer;

        public NameEffectPlacement Placement => placement;
        public RectTransform TextSlot => textSlot;
        public bool MatchTextRect => matchTextRect;
        public NameEffectRarity Rarity { get; private set; }

        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null)
                    cachedRect = transform as RectTransform;
                return cachedRect;
            }
        }

        public void Bind(TMP_Text text, NameEffectQuality effectQuality, NameEffectRarity rarity)
        {
            boundText = text;
            quality = effectQuality;
            Rarity = rarity;
            bound = true;

            bool wantsSlot = effectQuality > NameEffectQuality.Static && HasAnimatedLayer();
            holdsSlot = wantsSlot && NameEffectBudget.TryAcquire(this);

            ApplyLayers(holdsSlot);
        }

        /// <summary>Ngừng mọi layer và nhả slot — gọi trước khi trả view về pool.</summary>
        public void ReleaseView()
        {
            if (holdsSlot)
            {
                NameEffectBudget.Release(this);
                holdsSlot = false;
            }

            StopAllLayers();

            bound = false;
            boundText = null;
            hasPlayingLayer = false;
        }

        /// <summary>Báo cho các layer biết nội dung text vừa đổi.</summary>
        public void NotifyTextChanged()
        {
            if (!bound)
                return;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                    layers[i].NotifyTextChanged();
            }
        }

        /// <summary>Ngân sách thu hồi slot vì có effect hiếm hơn cần chạy.</summary>
        internal void OnAnimationSlotRevoked()
        {
            if (!holdsSlot)
                return;

            holdsSlot = false;

            if (bound)
                ApplyLayers(false);
        }

        private void ApplyLayers(bool animated)
        {
            var context = new NameEffectContext(boundText, quality, animated);
            hasPlayingLayer = false;

            for (int i = 0; i < layers.Length; i++)
            {
                NameEffectLayer layer = layers[i];
                if (layer == null)
                    continue;

                if (layer.ShouldPlay(context))
                {
                    layer.Play(context);
                    hasPlayingLayer = true;
                }
                else
                {
                    layer.Stop();
                }
            }
        }

        private void StopAllLayers()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                    layers[i].Stop();
            }
        }

        private bool HasAnimatedLayer()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                NameEffectLayer layer = layers[i];
                if (layer != null && layer.RequiresAnimationSlot)
                    return true;
            }

            return false;
        }

        private void Update()
        {
            if (!hasPlayingLayer)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            for (int i = 0; i < layers.Length; i++)
            {
                NameEffectLayer layer = layers[i];
                if (layer != null && layer.IsPlaying)
                    layer.Tick(deltaTime);
            }
        }

        private void OnEnable()
        {
            // Pool bật lại instance: xin lại slot đã nhả lúc bị tắt.
            if (!bound || holdsSlot || quality <= NameEffectQuality.Static || !HasAnimatedLayer())
                return;

            holdsSlot = NameEffectBudget.TryAcquire(this);
            ApplyLayers(holdsSlot);
        }

        private void OnDisable()
        {
            if (!holdsSlot)
                return;

            NameEffectBudget.Release(this);
            holdsSlot = false;
        }

        private void Reset()
        {
            CollectLayers();
        }

        private void OnValidate()
        {
            if (layers == null || layers.Length == 0)
                CollectLayers();
        }

        private void CollectLayers()
        {
            layers = GetComponentsInChildren<NameEffectLayer>(true);
        }

#if UNITY_EDITOR
        /// <summary>Chỉ dùng cho script editor dựng sẵn effect mẫu.</summary>
        public void EditorSetup(NameEffectPlacement newPlacement, RectTransform newTextSlot)
        {
            placement = newPlacement;
            textSlot = newTextSlot;
            CollectLayers();
        }
#endif
    }
}
