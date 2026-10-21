using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

namespace Profile.ProfileItem
{
    /// <summary>
    /// Loại animation của profile item (Avatar/Frame/Badge)
    /// </summary>
    public enum AnimationType
    {
        None,           // Không có animation, chỉ là ảnh tĩnh
        LoopOnly,       // Chỉ có 1 animation loop
        StartAndLoop    // Có 1 animation start và 1 animation loop
    }

    /// <summary>
    /// Config cho animation của profile item
    /// </summary>
    [System.Serializable]
    public class AnimationConfig
    {
        [Title("Animation Settings")]
        [Tooltip("Loại animation của item này")]
        public AnimationType animationType = AnimationType.None;

        [ShowIf("animationType", AnimationType.LoopOnly)]
        [Tooltip("Tên animation loop")]
        public string loopAnimationName = "";

        [ShowIf("animationType", AnimationType.StartAndLoop)]
        [Tooltip("Tên animation start (chạy 1 lần rồi chuyển sang loop)")]
        public string startAnimationName = "";

        [ShowIf("animationType", AnimationType.StartAndLoop)]
        [Tooltip("Tên animation loop (chạy sau khi start animation kết thúc)")]
        public string loopAnimationNameAfterStart = "";

        [ShowIf("animationType", AnimationType.StartAndLoop)]
        [MinValue(0f)]
        [Tooltip("Thời gian loop tối thiểu (giây) trước khi chạy lại start")]
        public float loopDurationMin = 3f;

        [ShowIf("animationType", AnimationType.StartAndLoop)]
        [MinValue(0f)]
        [Tooltip("Thời gian loop tối đa (giây) trước khi chạy lại start")]
        public float loopDurationMax = 6f;

        [ShowIf("HasSkeletonDataAsset")]
        [Tooltip("Skeleton Data Asset cho animation")]
        public SkeletonDataAsset skeletonDataAsset;

        [ShowIf("HasSkeletonDataAsset")]
        [Tooltip("Material cho animation")]
        public Material material;

        [ShowIf("animationType", AnimationType.StartAndLoop)]
        [Tooltip("Mix duration cho animation")]
        public float mixDuration = 0.2f;

        private bool HasSkeletonDataAsset => animationType == AnimationType.LoopOnly || animationType == AnimationType.StartAndLoop;

        /// <summary>
        /// Setup SkeletonGraphic + play LoopOnly. StartAndLoop chỉ setup mix; playback do ProfileAnimationController điều khiển.
        /// </summary>
        public void InitializeAnimation(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null || skeletonDataAsset == null)
            {
                return;
            }

            skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
            skeletonGraphic.material = material;
            skeletonGraphic.Initialize(true);

            switch (animationType)
            {
                case AnimationType.None:
                    break;

                case AnimationType.LoopOnly:
                    if (!string.IsNullOrEmpty(loopAnimationName))
                    {
                        skeletonGraphic.AnimationState.SetAnimation(0, loopAnimationName, true);
                    }
                    break;

                case AnimationType.StartAndLoop:
                    if (!string.IsNullOrEmpty(startAnimationName) && !string.IsNullOrEmpty(loopAnimationNameAfterStart))
                    {
                        skeletonGraphic.AnimationState.Data.SetMix(startAnimationName, loopAnimationNameAfterStart, mixDuration);
                        skeletonGraphic.AnimationState.Data.SetMix(loopAnimationNameAfterStart, startAnimationName, mixDuration);
                    }
                    break;
            }
        }

        public float GetRandomLoopDuration()
        {
            float min = Mathf.Min(loopDurationMin, loopDurationMax);
            float max = Mathf.Max(loopDurationMin, loopDurationMax);
            return Random.Range(min, max);
        }

        /// <summary>
        /// Kiểm tra xem có animation hay không
        /// </summary>
        public bool HasAnimation()
        {
            return animationType != AnimationType.None && skeletonDataAsset != null;
        }
    }
}
