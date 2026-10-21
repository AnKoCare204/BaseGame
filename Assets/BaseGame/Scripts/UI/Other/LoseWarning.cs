using System;

/// <summary>
/// Hook cảnh báo trước khi người chơi bỏ cuộc (ModalLose / ModalLeaveGame / ModalRetryGame): bấm bỏ lần đầu mà còn tiến độ event
/// sẽ mất thì hiện dòng cảnh báo (GoEventDes) thay vì đóng ngay. Event nào có tiến độ mất khi thua thì cộng handler vào đây
/// lúc khởi tạo và gỡ khi kết thúc.
/// </summary>
public static class LoseWarning
{
    public static Func<bool> IsAnyProgressAtRisk { get; set; }

    public static bool ShouldWarn()
    {
        if (IsAnyProgressAtRisk == null) return false;
        foreach (Delegate handler in IsAnyProgressAtRisk.GetInvocationList())
        {
            if (((Func<bool>)handler).Invoke()) return true;
        }

        return false;
    }
}
