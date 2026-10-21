using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

public class UIEndGameStarBlazeProgress : MonoBehaviour
{
    [field: SerializeField] public MMF_Player Anim {get; private set;}
    [field: SerializeField] public GameObject Root {get; private set;}
    private void Start()
    {
        Root.SetActive(false);
        if (!StarBlazeManager.Instance.IsInitialize) return;
        if (!StarBlazeManager.Instance.IsStartEvent) return;
        if (!StarBlazeManager.Instance.IsEventUnlock) return;
        if (StarBlazeManager.Instance.IsCompleted()) return;
        PlayStreakAsync().Forget();
    }

    private async UniTask PlayStreakAsync()
    {
        Root.SetActive(true);
        Anim.PlayFeedbacks();
        await UniTask.DelayFrame(2, cancellationToken: this.GetCancellationTokenOnDestroy());
        UIStreakBar.SetStreakAction?.Invoke(StarBlazeData.Instance.LastWinStreak);
        
        await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
        int lastStreak = StarBlazeData.Instance.LastWinStreak;
        int currentStreak = StarBlazeData.Instance.CurrentWinStreak;
        bool isStreakChanged = lastStreak != currentStreak;
        

        await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (isStreakChanged)
        {
            await (UIStreakBar.PlayStreakAsync?.Invoke(lastStreak, currentStreak) ?? UniTask.CompletedTask);
        }
    }
}