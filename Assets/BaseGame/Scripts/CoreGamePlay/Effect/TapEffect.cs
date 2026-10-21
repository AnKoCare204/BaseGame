/// <summary>
/// Hiệu ứng nổ bong bóng tại điểm chạm trong gameplay (prefab TapBubbleFx): chấm sáng lóe lên, một vòng tròn mỏng nở ra rồi mờ dần,
/// kèm vài bọt nhỏ bắn ra và trôi lên. InputManager.PlayTapEffect phát mỗi lần nhấn xuống trong vùng chơi.
/// Pool và cách phát: xem PooledParticleEffect. Lớp riêng để có pool riêng với BubbleBreakEffect.
/// </summary>
public class TapEffect : PooledParticleEffect<TapEffect>
{
    
}
