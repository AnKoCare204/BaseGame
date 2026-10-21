using R3;
using TW.Reactive.CustomComponent;

namespace CoreNotification
{
    public class FrameNotificationCondition : IProfileNotificationCondition
    {
        public Reactive<int> IsShowNotification { get; set; } = new(-1);
        public void Initialize()
        {
            IsShowNotification = ProfileData.Instance.IsShowFrameNotification;
        }

        // Không tạo subscription nào: IsShowNotification trỏ thẳng vào Reactive global,
        // subscriber duy nhất nằm ở ProfileNotification và đã được .AddTo(this).
        public void Dispose()
        {
        }
    }
}