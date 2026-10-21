using UnityEngine;

/// Toán thuần cho cá Home (FishHomeSwimmer, FishHomeRenderManager, HomeRenderTexture), tách riêng để test EditMode không cần scene.
public static class FishHomeSwimMath
{
    /// Cường độ giật mình 0..1 theo khoảng cách tới điểm chạm: strength * sqrt(1 - dist / radius). Ngoài bán kính (hoặc bán kính <= 0) = 0.
    public static float StartleIntensity(float dist, float radius, float strength)
    {
        if (radius <= 0f || dist >= radius) return 0f;
        return Mathf.Clamp01(strength) * Mathf.Sqrt(1f - dist / radius);
    }

    /// Điểm có nằm phía trước cá không (dirX = +1 bơi sang phải, -1 sang trái). Cùng X không tính là phía trước.
    public static bool IsAhead(float fishX, float pointX, float dirX) => (pointX - fishX) * dirX > 0f;

    /// Độ cao đích (so với tâm vùng) khi bỏ chạy: dạt khỏi điểm chạm theo chiều dọc, kẹp trong ±range. Cùng độ cao thì dạt lên.
    public static float FleeTargetY(float fishOffY, float pointOffY, float fleeVertical, float intensity, float range)
    {
        float sign = fishOffY >= pointOffY ? 1f : -1f;
        return Mathf.Clamp(fishOffY + sign * fleeVertical * intensity, -range, range);
    }

    /// Cả thân cá đã ra khỏi khung ngang chưa (offX = tâm cá so với tâm vùng, halfLength = nửa dài thân).
    public static bool IsFullyOutside(float offX, float halfWidth, float halfLength) => Mathf.Abs(offX) >= halfWidth + halfLength;

    /// X (so với tâm vùng) khi vào lại từ phía side (+1 mép phải, -1 mép trái): mũi cá cách mép pad đơn vị.
    public static float EntryOffsetX(float side, float halfWidth, float halfLength, float pad) => side * (halfWidth + halfLength + pad);

    /// baseYaw khi vào lại từ phía side: mép phải thì bơi sang trái (270), mép trái thì bơi sang phải (90).
    public static float EntryBaseYaw(float side) => side > 0f ? 270f : 90f;

    /// Z của slot trong dải slotCount làn cách nhau spacing, đối xứng quanh 0.
    public static float LaneZ(int slot, int slotCount, float spacing) => (slot - (slotCount - 1) * 0.5f) * spacing;

    /// Slot ban đầu của cá thứ fishIndex khi có fishCount cá và slotCount slot: nhóm cá ở giữa, slot dư chia đều hai đầu.
    public static int InitialSlot(int fishIndex, int fishCount, int slotCount) => fishIndex + Mathf.Max(0, slotCount - fishCount) / 2;

    /// Chọn slot mới: ngẫu nhiên trong các slot không bị chiếm và khác current (rand 0..1 = Random.value, truyền vào để test được).
    /// Không còn slot nào thì trả về current.
    public static int PickFreeSlot(bool[] occupied, int current, float rand)
    {
        int free = 0;
        for (int i = 0; i < occupied.Length; i++)
        {
            if (!occupied[i] && i != current) free++;
        }
        if (free == 0) return current;

        int pick = Mathf.Clamp((int)(rand * free), 0, free - 1);
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i] || i == current) continue;
            if (pick == 0) return i;
            pick--;
        }
        return current;
    }

    /// Điểm local trong rect của RawImage -> toạ độ 0..1 của ảnh (áp uvRect). Không kẹp, để ngoài rect ra ngoài 0..1. Rect rỗng trả về giữa.
    public static Vector2 LocalToViewport(Vector2 local, Rect rect, Rect uvRect)
    {
        float nx = rect.width > 0f ? (local.x - rect.xMin) / rect.width : 0.5f;
        float ny = rect.height > 0f ? (local.y - rect.yMin) / rect.height : 0.5f;
        return new Vector2(uvRect.xMin + nx * uvRect.width, uvRect.yMin + ny * uvRect.height);
    }
}
