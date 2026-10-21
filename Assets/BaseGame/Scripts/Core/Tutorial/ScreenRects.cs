using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>Đổi bounds world / RectTransform UI sang hình chữ nhật pixel màn hình (y hướng lên) cho GuidePlacement. Test ở FtueScreenRectsTests.</summary>
    public static class ScreenRects
    {
        public static Rect FromBounds(Bounds bounds, Camera cam)
        {
            Vector3 min = bounds.min, max = bounds.max;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 s = cam.WorldToScreenPoint(corner);
                xMin = Mathf.Min(xMin, s.x);
                yMin = Mathf.Min(yMin, s.y);
                xMax = Mathf.Max(xMax, s.x);
                yMax = Mathf.Max(yMax, s.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Rect FromRectTransform(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector3 c in corners)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, c);
                xMin = Mathf.Min(xMin, s.x);
                yMin = Mathf.Min(yMin, s.y);
                xMax = Mathf.Max(xMax, s.x);
                yMax = Mathf.Max(yMax, s.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
