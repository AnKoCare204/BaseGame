using System.Collections.Generic;
using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>Chỗ đặt cả nhóm khung chữ (linh vật + bóng thoại) và linh vật nằm bên phải hay trái.</summary>
    public readonly struct GuidePlacementResult
    {
        public GuidePlacementResult(Rect rect, bool mascotOnRight)
        {
            Rect = rect;
            MascotOnRight = mascotOnRight;
        }

        public Rect Rect { get; }
        public bool MascotOnRight { get; }
    }

    /// <summary>
    /// Chọn chỗ đặt khung chữ tutorial (spec FTUE bản sửa mục 3). Hàm thuần trên hình chữ nhật cùng một hệ toạ độ (y hướng lên), test ở
    /// FtueGuidePlacementTests. Thử lần lượt: ngay dưới hợp các phần đang sáng, ngay trên, giữa khoảng trống dọc lớn nhất không chạm phần
    /// sáng / phần cần tránh. Lấy chỗ đầu tiên nằm trọn trong safe và không giao phần sáng / phần cần tránh; không có thì lấy chỗ (đã kẹp
    /// dọc vào safe) giao ít diện tích nhất. Ngang: căn giữa theo phần đang sáng rồi kẹp vào safe; bị kẹp vào mép phải thì linh vật sang phải.
    /// </summary>
    public static class GuidePlacement
    {
        public static GuidePlacementResult Choose(Rect safe, Vector2 size, IReadOnlyList<Rect> lit, IReadOnlyList<Rect> avoid, float gap)
        {
            bool hasLit = TryUnion(lit, out Rect litUnion);
            float desiredCenterX = hasLit ? litUnion.center.x : safe.center.x;
            float x = ClampX(desiredCenterX - size.x * 0.5f, safe, size.x, out bool clampedRight);

            var candidates = new List<float>(3);   // yMin của từng chỗ thử
            if (hasLit)
            {
                candidates.Add(litUnion.yMin - gap - size.y);
                candidates.Add(litUnion.yMax + gap);
            }

            if (TryLargestFreeBand(safe, size.y, lit, avoid, out float bandMin, out float bandMax))
                candidates.Add((bandMin + bandMax - size.y) * 0.5f);

            Rect best = new Rect(x, ClampY(safe.center.y - size.y * 0.5f, safe, size.y), size.x, size.y);
            float bestOverlap = float.MaxValue;
            foreach (float yMin in candidates)
            {
                var rect = new Rect(x, yMin, size.x, size.y);
                bool inside = rect.yMin >= safe.yMin && rect.yMax <= safe.yMax;
                if (inside && OverlapArea(rect, lit) + OverlapArea(rect, avoid) <= 0f) return new GuidePlacementResult(rect, clampedRight);

                var clamped = new Rect(x, ClampY(yMin, safe, size.y), size.x, size.y);
                float overlap = OverlapArea(clamped, lit) + OverlapArea(clamped, avoid);
                if (overlap < bestOverlap)
                {
                    bestOverlap = overlap;
                    best = clamped;
                }
            }

            return new GuidePlacementResult(best, clampedRight);
        }

        private static float ClampX(float xMin, Rect safe, float width, out bool clampedRight)
        {
            clampedRight = false;
            if (width >= safe.width) return safe.center.x - width * 0.5f;
            if (xMin + width > safe.xMax)
            {
                clampedRight = true;
                return safe.xMax - width;
            }

            return xMin < safe.xMin ? safe.xMin : xMin;
        }

        private static float ClampY(float yMin, Rect safe, float height) =>
            height >= safe.height ? safe.center.y - height * 0.5f : Mathf.Clamp(yMin, safe.yMin, safe.yMax - height);

        private static bool TryUnion(IReadOnlyList<Rect> rects, out Rect union)
        {
            union = default;
            bool any = false;
            if (rects == null) return false;
            foreach (Rect r in rects)
            {
                if (r.width <= 0f || r.height <= 0f) continue;
                union = any ? Rect.MinMaxRect(Mathf.Min(union.xMin, r.xMin), Mathf.Min(union.yMin, r.yMin),
                    Mathf.Max(union.xMax, r.xMax), Mathf.Max(union.yMax, r.yMax)) : r;
                any = true;
            }

            return any;
        }

        private static float OverlapArea(Rect rect, IReadOnlyList<Rect> others)
        {
            if (others == null) return 0f;
            float sum = 0f;
            foreach (Rect o in others)
            {
                float w = Mathf.Min(rect.xMax, o.xMax) - Mathf.Max(rect.xMin, o.xMin);
                float h = Mathf.Min(rect.yMax, o.yMax) - Mathf.Max(rect.yMin, o.yMin);
                if (w > 0f && h > 0f) sum += w * h;
            }

            return sum;
        }

        /// <summary>
        /// Khoảng dọc lớn nhất trong safe không có phần sáng / cần tránh nào (chỉ xét y: khung chữ gần rộng hết màn hình), cao ít nhất
        /// minHeight. false nếu không khoảng nào đủ cao.
        /// </summary>
        private static bool TryLargestFreeBand(Rect safe, float minHeight, IReadOnlyList<Rect> lit, IReadOnlyList<Rect> avoid,
            out float min, out float max)
        {
            var blocks = new List<Vector2>();
            AddBlocks(blocks, lit, safe);
            AddBlocks(blocks, avoid, safe);
            blocks.Sort((a, b) => a.x.CompareTo(b.x));

            min = max = 0f;
            float bestHeight = -1f;
            float cursor = safe.yMin;
            foreach (Vector2 b in blocks)
            {
                if (b.x > cursor && b.x - cursor > bestHeight)
                {
                    bestHeight = b.x - cursor;
                    min = cursor;
                    max = b.x;
                }

                cursor = Mathf.Max(cursor, b.y);
            }

            if (safe.yMax - cursor > bestHeight)
            {
                bestHeight = safe.yMax - cursor;
                min = cursor;
                max = safe.yMax;
            }

            return bestHeight >= minHeight;
        }

        private static void AddBlocks(List<Vector2> blocks, IReadOnlyList<Rect> rects, Rect safe)
        {
            if (rects == null) return;
            foreach (Rect r in rects)
            {
                float lo = Mathf.Max(r.yMin, safe.yMin), hi = Mathf.Min(r.yMax, safe.yMax);
                if (hi > lo) blocks.Add(new Vector2(lo, hi));
            }
        }
    }
}
