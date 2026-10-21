using Unity.Mathematics;

/// Dạng sóng dùng chung cho job animation (Burst) và code main thread. Sóng trả về -1..+1 theo pha chuẩn hoá u = pha / 2π (0..1).
/// Jet (nhịp phụt (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed)): [0, open) chụm → xòe (+1 → −1, smoothstep); [open, open + snap) giật xòe → chụm (−1 → +1); còn lại giữ chụm (+1).
/// JetSpeedMultiplier: hệ số tốc độ di chuyển cùng nhịp: mở tay = mức sàn (trượt chậm), giật = tăng lên 1, sau đó giảm theo hàm mũ về sàn.
public static class FishWaveProfiles
{
    private static float SmoothStep(float x)
    {
        x = math.saturate(x);
        return x * x * (3f - 2f * x);
    }

    public static float JetWave(float u, float open, float snap)
    {
        open = math.max(open, 1e-3f);
        snap = math.max(snap, 1e-3f);
        if (u < open) return 1f - 2f * SmoothStep(u / open);
        if (u < open + snap) return -1f + 2f * SmoothStep((u - open) / snap);
        return 1f;
    }

    public static float JetSpeedMultiplier(float u, float open, float snap, float floor, float decay)
    {
        open = math.max(open, 1e-3f);
        snap = math.max(snap, 1e-3f);
        float snapEnd = math.min(open + snap, 0.999f);
        if (u < open) return floor;
        if (u < snapEnd) return math.lerp(floor, 1f, SmoothStep((u - open) / snap));
        float g = (u - snapEnd) / math.max(1f - snapEnd, 1e-3f);
        return floor + (1f - floor) * math.exp(-decay * g);
    }
}
