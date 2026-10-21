using System;
using R3;
using TW.Reactive.CustomComponent;

namespace CoreNotification
{
    public class ProfileNotificationCondition : IProfileNotificationCondition
    {
        public Reactive<int> IsShowNotification { get; set; } = new(-1);

        // ProfileData.Instance sống suốt phiên chơi, còn condition này chết theo prefab chứa nó.
        // Phải giữ lại subscription để gỡ trong Dispose, nếu không mỗi lần mở lại UI là cộng thêm
        // 4 subscriber vĩnh viễn vào ProfileData và giữ condition cũ không cho GC thu.
        private IDisposable[] subscriptions;

        public void Initialize()
        {
            Dispose(); // gọi Initialize lần nữa thì không nhân đôi subscription

            ProfileData profileData = ProfileData.Instance;
            subscriptions = new IDisposable[]
            {
                profileData.IsShowAvatarNotification.Subscribe(_ => UpdateNotificationState()),
                profileData.IsShowBadgeNotification.Subscribe(_ => UpdateNotificationState()),
                profileData.IsShowFrameNotification.Subscribe(_ => UpdateNotificationState()),
                profileData.IsShowNameNotification.Subscribe(_ => UpdateNotificationState()),
            };

            UpdateNotificationState();
        }

        public void Dispose()
        {
            if (subscriptions == null) return;
            for (int i = 0; i < subscriptions.Length; i++)
            {
                subscriptions[i]?.Dispose();
            }

            subscriptions = null;
        }

        private void UpdateNotificationState()
        {
            if (ProfileData.Instance.IsShowAvatarNotification.Value >= 0 || ProfileData.Instance.IsShowBadgeNotification.Value >= 0 || ProfileData.Instance.IsShowFrameNotification.Value >= 0 || ProfileData.Instance.IsShowNameNotification.Value >= 0)
            {
                IsShowNotification.Value = 1;
            }
            else
            {
                IsShowNotification.Value = -1;
            }
        }
    }
}