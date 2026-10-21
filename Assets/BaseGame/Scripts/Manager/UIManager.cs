using Core.UI.Activities;
using Core.UI.Modals;
using Core.UI.Screens;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Facade điều hướng UI (khung AntFood/FishSort). Mọi screen/modal/activity chính đi qua đây.
/// Quy ước: address Addressables = tên class (nameof).
/// </summary>
public class UIManager : Singleton<UIManager>
{
    private bool isClosingAllModal;
    private bool isClosingAllActivities;
    private readonly List<AsyncOperationHandle<GameObject>> preloadedViews = new();

    /// <summary>
    /// Nạp sẵn prefab các màn dùng ngay sau boot (address = tên class) để lần Push của TW.UGUI không phải đọc bundle giữa gameplay.
    /// Giữ handle suốt phiên chơi; lỗi key chỉ log, boot vẫn tiếp tục. Không bật pooling (instantiate vẫn như cũ).
    /// </summary>
    public async UniTask PreloadCoreViewsAsync()
    {
        List<string> keys = new List<string> { nameof(ScreenInGame), nameof(ActivityLoading), nameof(ModalWin), nameof(ModalLose) };
        foreach (string key in keys)
        {
            try
            {
                AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (handle.Status == AsyncOperationStatus.Succeeded) preloadedViews.Add(handle);
                else Debug.LogError($"[UIManager] preload {key} thất bại: {handle.OperationException}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIManager] preload {key} lỗi: {e.Message}");
            }
        }
        Debug.Log($"[UIManager] preload {preloadedViews.Count}/{keys.Count} view");
    }
    
    public async UniTask InitOverlayActivities()
    {
        ActivityContainer container = ActivityContainer.Find(ContainerKey.OverlayActivities);
        ViewOptions activityNotificationOverlay = new ViewOptions(nameof(ActivityNotificationOverlay));
        await container.ShowAsync(activityNotificationOverlay);
    }

    public async UniTask ShowScreenMainMenu()
    {
        await CloseAllModal();
        ScreenContainer container = ScreenContainer.Find(ContainerKey.Screens);
        if (container == null) return;
        ScreenOptions viewOptions = new ScreenOptions(nameof(ScreenMainMenu), stack: false);
        await container.PushAsync(viewOptions);
    }

    public async UniTask ShowScreenInGame()
    {
        await CloseAllModal();
        ScreenContainer container = ScreenContainer.Find(ContainerKey.Screens);
        if (container == null) return;
        ScreenOptions viewOptions1 = new ScreenOptions(nameof(ScreenEmpty), stack: false);
        await container.PushAsync(viewOptions1);
        await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
        ScreenOptions viewOptions2 = new ScreenOptions(nameof(ScreenInGame), stack: false);
        await container.PushAsync(viewOptions2);
    }

    public async UniTask CloseAllModal()
    {
        ModalContainer container = ModalContainer.Find(ContainerKey.Modals);
        if (container == null) return;
        while (isClosingAllModal)
        {
            await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        }

        isClosingAllModal = true;
        try
        {
            while (container.Modals.Count > 0)
            {
                await UniTask.WaitUntil(IsNotInTransition, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (container.Modals.Count == 0) break;

                int beforeCount = container.Modals.Count;
                try
                {
                    await container.PopAsync(true);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    UnityEngine.Debug.LogError($"[UIManager] CloseAllModal: container lệch trạng thái khi pop, dừng an toàn. {e}");
                    break;
                }

                if (container.Modals.Count >= beforeCount)
                {
                    await UniTask.Yield(this.GetCancellationTokenOnDestroy());
                }
            }
        }
        finally
        {
            isClosingAllModal = false;
        }

        return;

        bool IsNotInTransition() => !container.IsInTransition;
    }

    public async UniTask CloseAllActivities()
    {
        ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
        if (container == null) return;
        while (isClosingAllActivities)
        {
            await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        }

        isClosingAllActivities = true;
        try
        {
            while (container.Activities.Count > 0)
            {
                await UniTask.WaitUntil(IsNotInTransition, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (container.Activities.Count == 0) break;

                int beforeCount = container.Activities.Count;
                try
                {
                    string activityName = container.Activities.Keys.First();
                    await container.HideAsync(activityName);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    UnityEngine.Debug.LogError($"[UIManager] CloseAllActivities: container lệch trạng thái khi hide all, dừng an toàn. {e}");
                    break;
                }

                if (container.Activities.Count >= beforeCount)
                {
                    await UniTask.Yield(this.GetCancellationTokenOnDestroy());
                }
            }
        }
        finally
        {
            isClosingAllActivities = false;
        }

        return;

        bool IsNotInTransition() => !container.IsInTransition;
    }

    // ===== Win / Lose =====
    public async UniTask ShowModalWin()
    {
        await CloseAllModal();
        ViewOptions viewOptions = new ViewOptions(nameof(ModalWin));
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
    }

    /// <summary>Kẹt: popup Play On (coin / ads) hoặc bỏ cuộc.</summary>
    public async UniTask ShowModalLose()
    {
        await CloseAllModal();
        ViewOptions viewOptions = new ViewOptions(nameof(ModalLose));
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
    }

    // ===== Modal chung =====
    public async UniTask ShowModalFillHeart()
    {
        ViewOptions viewOptions = new ViewOptions(nameof(ModalFillHeart));
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
    }

    public async UniTask ShowModalRating()
    {
        ModalContainer container = ModalContainer.Find(ContainerKey.Modals);
        await UniTask.WaitUntil(IsContainerDoneTransition, cancellationToken: this.GetCancellationTokenOnDestroy());
        ViewOptions viewOptions = new ViewOptions(nameof(ModalRate));
        await container.PushAsync(viewOptions);
        return;

        bool IsContainerDoneTransition()
        {
            if (container.IsInTransition) return false;
            if (container.Modals.Count == 0) return true;
            return container.Current.View is not ModalRate;
        }
    }

    public async UniTask ShowModalPause()
    {
        ViewOptions viewOptions = new ViewOptions(nameof(ModalPause));
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
    }

    // ===== Loading =====
    public async UniTask ShowLoadingWithTask(Func<UniTask> onLoadingTask, Func<UniTask> onLoadingCompleteTask)
    {
        ViewOptions activityLoading = new ViewOptions(nameof(ActivityLoading));
        Memory<object> args = new Memory<object>(new object[] { onLoadingTask, onLoadingCompleteTask });
        await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(activityLoading, args);
    }

    public async UniTask WaitLoadingClose()
    {
        ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
        await UniTask.WaitUntil(IsCloseActivityLoading, cancellationToken: this.GetCancellationTokenOnDestroy());
        return;

        bool IsCloseActivityLoading()
        {
            if (container.Activities.Count == 0) return true;
            return !container.Activities.ContainsKey(nameof(ActivityLoading));
        }
    }
    
    // ===== Event: reward claim =====
    /// <summary>
    /// Đếm post-close flow (vd. CheckOpenCardPackAsync) sau khi ActivityClaimReward hide.
    /// ClaimRewardComplete chỉ return khi activity đã đóng VÀ flow này = 0.
    /// </summary>
    private int _claimRewardPostCloseFlowCount;

    public void BeginClaimRewardPostCloseFlow() => _claimRewardPostCloseFlowCount++;

    public void EndClaimRewardPostCloseFlow()
    {
        if (_claimRewardPostCloseFlowCount > 0)
            _claimRewardPostCloseFlowCount--;
    }

    /// <summary>
    /// Đợi ActivityClaimReward đóng xong, rồi đợi luôn post-close flow
    /// (open card pack / complete set-collection) nếu có.
    /// </summary>
    public async UniTask ClaimRewardComplete()
    {
        ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
        await UniTask.WaitUntil(IsClaimRewardFullyComplete, cancellationToken: this.GetCancellationTokenOnDestroy());

        return;

        bool IsClaimRewardFullyComplete()
        {
            if (_claimRewardPostCloseFlowCount > 0) return false;
            if (container.Activities.Count == 0) return true;
            return !container.Activities.ContainsKey(nameof(ActivityClaimReward));
        }
    }
    
    public void ShowInfo(string message)
    {
        ActivityNotificationOverlayContext.Events.ShowInfo?.Invoke(message);
    }

    [Button]
    public void ShowError(string message)
    {
        ActivityNotificationOverlayContext.Events.ShowError?.Invoke(message);
    }

    public async UniTask ShowModalNoAdsCompleted()
    {
        ViewOptions viewOptions = new ViewOptions(nameof(ModalNoAds));
        ModalContainer container = ModalContainer.Find(ContainerKey.Modals);
        await container.PushAsync(viewOptions);
        await UniTask.WaitUntil(IsShowModalNoAdsCompleted, cancellationToken: this.GetCancellationTokenOnDestroy());
        return;

        bool IsShowModalNoAdsCompleted()
        {
            if (container.IsInTransition) return false;
            for (int i = 0; i < container.Modals.Count; i++)
            {
                if (container.Modals[i].View is ModalNoAds) return false;
            }

            return true;
        }
    }
    
    
    // ===== Notification overlay =====
    [Button]
    public void ShowLoadingBlock()
    {
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock?.Invoke();
    }

    [Button]
    public void HideLoadingBlock()
    {
        ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
    }

    [Button]
    public void ShowSuccess(string message)
    {
        ActivityNotificationOverlayContext.Events.ShowSuccess?.Invoke(message);
    }


    [Button]
    public void ShowWarning(string message)
    {
        ActivityNotificationOverlayContext.Events.ShowWarning?.Invoke(message);
    }
    
    public bool IsHaveActivity(string name)
    {
        ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
        if (container.Activities.Count == 0) return false;
        return container.Activities.ContainsKey(name);
    }
}
