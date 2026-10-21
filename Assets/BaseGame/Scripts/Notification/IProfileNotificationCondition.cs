using System;
using R3;
using TW.Reactive.CustomComponent;

namespace CoreNotification
{
    // Kế thừa IDisposable: owner (ProfileNotification) BẮT BUỘC gọi Dispose khi bị Destroy.
    // Condition nào subscribe vào Reactive global của ProfileData mà không gỡ thì subscription
    // sẽ sống mãi, cộng dồn mỗi lần prefab được Instantiate lại và giữ luôn condition cũ không cho GC thu.
    public interface IProfileNotificationCondition : IDisposable
    {
        Reactive<int> IsShowNotification { get; set; }
        void Initialize();
    }
}