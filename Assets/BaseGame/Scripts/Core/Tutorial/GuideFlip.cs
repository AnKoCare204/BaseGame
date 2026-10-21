using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>Linh vật bên trái (mặc định) hay bên phải bóng thoại (spec FTUE bản sửa mục 3, "khung lật"). Test ở FtueGuideFlipTests.</summary>
    public static class GuideFlip
    {
        /// <summary>
        /// mascotX / bubbleX: localPosition.x gốc (bố cục linh vật bên trái). right = đổi dấu x quanh tâm khung và soi gương linh vật + bóng
        /// thoại (đuôi bóng thoại vẫn chỉ vào linh vật, linh vật quay mặt về bóng thoại); text là con của bubble nên soi gương ngược lại
        /// để chữ vẫn đọc xuôi.
        /// </summary>
        public static void Apply(Transform mascot, Transform bubble, Transform text, float mascotX, float bubbleX, bool right)
        {
            float sign = right ? -1f : 1f;
            SetX(mascot, mascotX * sign);
            SetX(bubble, bubbleX * sign);
            Mirror(mascot, sign);
            Mirror(bubble, sign);
            if (text != null) Mirror(text, sign);
        }

        private static void SetX(Transform t, float x) => t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);

        private static void Mirror(Transform t, float sign)
        {
            Vector3 s = t.localScale;
            t.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
        }
    }
}
