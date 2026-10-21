using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Một lớp hiệu ứng trong prefab effect (quét sáng, mây trôi, mưa, chớp...).
    /// Đây là điểm mở rộng chính: effect Epic/Legendary sau này = ghép thêm layer
    /// vào prefab, không phải sửa <see cref="NameEffectView"/> hay host.
    ///
    /// Layer KHÔNG tự Update — <see cref="NameEffectView"/> gọi <see cref="Tick"/>
    /// cho cả prefab trong một Update duy nhất, nên 20 dòng leaderboard vẫn chỉ
    /// tốn 20 lần Update chứ không phải 20 × số layer.
    ///
    /// Mỗi layer phải nằm trên một GameObject con riêng (khi Stop, layer tự tắt
    /// GameObject của mình).
    /// </summary>
    public abstract class NameEffectLayer : MonoBehaviour
    {
        [Tooltip("Chất lượng tối thiểu để layer này được chạy.")]
        [SerializeField] private NameEffectQuality minQuality = NameEffectQuality.Medium;

        [Tooltip("Layer có animation → cần slot trong ngân sách. Layer tĩnh (chỉ đổi sprite/màu) thì bỏ tick.")]
        [SerializeField] private bool requiresAnimationSlot = true;

        [Tooltip("Tắt GameObject khi layer ngừng chạy.")]
        [SerializeField] private bool hideWhenStopped = true;

        public NameEffectQuality MinQuality => minQuality;
        public bool RequiresAnimationSlot => requiresAnimationSlot;
        public bool IsPlaying { get; private set; }

        protected NameEffectContext Context { get; private set; }

        public bool ShouldPlay(in NameEffectContext context)
        {
            if (context.Quality < minQuality)
                return false;

            return !requiresAnimationSlot || context.Animated;
        }

        public void Play(in NameEffectContext context)
        {
            Context = context;

            if (hideWhenStopped && !gameObject.activeSelf)
                gameObject.SetActive(true);

            IsPlaying = true;
            OnPlay();
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                OnStop();
            }

            if (hideWhenStopped && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (IsPlaying)
                OnTick(deltaTime);
        }

        /// <summary>
        /// Text vừa đổi nội dung nhưng effect giữ nguyên (cell trong list ảo hoá
        /// đổi sang người chơi khác). Layer nào bám theo hình chữ phải đồng bộ lại.
        /// </summary>
        public void NotifyTextChanged()
        {
            if (IsPlaying)
                OnTextChanged();
        }

        protected abstract void OnPlay();

        protected abstract void OnStop();

        protected abstract void OnTick(float deltaTime);

        protected virtual void OnTextChanged()
        {
        }
    }
}
