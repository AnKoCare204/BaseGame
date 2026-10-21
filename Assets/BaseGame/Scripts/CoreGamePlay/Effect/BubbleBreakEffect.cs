/// <summary>
/// Hiệu ứng bóng vỡ (prefab BubbleBreakFx): cùng kiểu với TapEffect nhưng dựng cho bóng bán kính 1 (glow phủ cả bóng, vòng nở từ mép
/// bóng ra ngoài, nhiều bọt bắn ra). BubbleView.PlayBreakVfx phát tại tâm bóng với scale = bán kính lúc bóng hết cá nổ, vỡ băng,
/// vỡ vỏ HP. Pool và cách phát: xem PooledParticleEffect. Lớp riêng để có pool riêng, không lẫn instance với TapEffect.
/// </summary>
public class BubbleBreakEffect : PooledParticleEffect<BubbleBreakEffect>
{
}
