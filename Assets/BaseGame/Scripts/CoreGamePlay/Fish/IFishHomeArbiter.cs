/// Trọng tài cho cá Home: FishHomeRenderManager cài đặt để quyết định những việc liên quan tới nhiều cá cùng lúc.
/// Cá không có trọng tài (đặt tay trong scene) thì không rời khung, không tò mò, chỉ bơi và nghỉ.
public interface IFishHomeArbiter
{
    /// Còn được phép có thêm một cá ra ngoài khung không.
    bool CanLeave();

    /// Còn được phép có thêm một cá tò mò (kể cả đang chờ tới lượt) không.
    bool CanBeCurious();

    /// Làn Z mới cho cá vừa ở ngoài khung vào lại (không trùng làn cá khác).
    float PickLane(FishHomeSwimmer who);
}
