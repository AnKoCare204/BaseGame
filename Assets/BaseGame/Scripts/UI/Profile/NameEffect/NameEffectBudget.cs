using System.Collections.Generic;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Giới hạn số name effect được chạy animation cùng lúc. Bảng xếp hạng cuộn
    /// qua 200 dòng mà dòng nào cũng quét sáng thì máy yếu tụt frame ngay; ngân
    /// sách này giữ cho chỉ N cái đẹp nhất chạy, số còn lại rơi về dạng tĩnh.
    ///
    /// Khi hết slot, effect hiếm hơn được phép "giành" slot của effect thường hơn
    /// — người chơi Legendary luôn thấy hiệu ứng của mình chạy.
    /// </summary>
    public static class NameEffectBudget
    {
        public const int Unlimited = 0;

        private static int maxConcurrent = 8;
        private static readonly List<NameEffectView> holders = new List<NameEffectView>();

        public static int MaxConcurrent
        {
            get => maxConcurrent;
            set
            {
                maxConcurrent = Mathf.Max(0, value);
                TrimToBudget();
            }
        }

        public static int ActiveCount => holders.Count;

        public static bool TryAcquire(NameEffectView view)
        {
            if (view == null)
                return false;

            PruneDead();

            if (holders.Contains(view))
                return true;

            if (maxConcurrent == Unlimited || holders.Count < maxConcurrent)
            {
                holders.Add(view);
                return true;
            }

            int victimIndex = FindVictim(view.Rarity);
            if (victimIndex < 0)
                return false;

            NameEffectView victim = holders[victimIndex];
            holders.RemoveAt(victimIndex);
            holders.Add(view);

            // Gọi sau khi đã sửa xong list: view bị thu hồi sẽ gọi Release ngược lại.
            victim.OnAnimationSlotRevoked();
            return true;
        }

        public static void Release(NameEffectView view)
        {
            if (view == null)
            {
                PruneDead();
                return;
            }

            holders.Remove(view);
        }

        /// <summary>Chỉ số của effect thường nhất đang giữ slot, nếu nó thường hơn kẻ đang xin.</summary>
        private static int FindVictim(NameEffectRarity challenger)
        {
            int index = -1;
            NameEffectRarity lowest = challenger;

            for (int i = 0; i < holders.Count; i++)
            {
                NameEffectView holder = holders[i];
                if (holder == null)
                    continue;

                if (holder.Rarity < lowest)
                {
                    lowest = holder.Rarity;
                    index = i;
                }
            }

            return index;
        }

        private static void PruneDead()
        {
            for (int i = holders.Count - 1; i >= 0; i--)
            {
                if (holders[i] == null)
                    holders.RemoveAt(i);
            }
        }

        private static void TrimToBudget()
        {
            if (maxConcurrent == Unlimited)
                return;

            while (holders.Count > maxConcurrent)
            {
                int last = holders.Count - 1;
                NameEffectView view = holders[last];
                holders.RemoveAt(last);

                if (view != null)
                    view.OnAnimationSlotRevoked();
            }
        }
    }
}
