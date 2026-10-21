using System;
using UnityEngine;

namespace CoreData
{
    /// <summary>
    /// Giải mã nội dung file level (Base64 của AES của JSON, do DataSerializer.Serialize ghi).
    /// Không bao giờ ném lỗi: file hỏng trả false để nơi gọi huỷ vào level thay vì dựng bàn rỗng.
    /// </summary>
    public static class LevelDataCodec
    {
        public static bool TryDecode(string text, out LevelData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                data = DataSerializer.Deserialize<LevelData>(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelDataCodec] Giải mã level lỗi: {e.Message}");
                data = null;
            }

            if (data != null && !IsPlayable(data))
            {
                Debug.LogWarning("[LevelDataCodec] Level giải mã được nhưng không có bóng hoặc không có đơn, coi như hỏng.");
                data = null;
            }

            return data != null;
        }

        /// <summary>Level dựng được một bàn chơi: có ít nhất một bóng và một đơn. Level rỗng cho ra bàn trống không thắng được.</summary>
        public static bool IsPlayable(LevelData data)
        {
            return data != null && data.BubbleCount > 0 && data.OrderQueue != null && data.OrderQueue.Length > 0;
        }
    }
}
