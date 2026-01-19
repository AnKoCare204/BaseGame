using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using MoreMountains.Feedbacks;
using TW.UGUI.Shared;
using UnityEngine;

[RequireComponent(typeof(MMF_Player))]
public class FeelAnimation : TransitionAnimationBehaviour
{
    [field: SerializeField] public MMF_Player MmfPlayer {get; private set;}
    public override float TotalDuration => MmfPlayer.TotalDuration;
    private CancellationTokenSource UpdateCancellationTokenSource { get; set; }
    private void Reset()
    {
        MmfPlayer = GetComponent<MMF_Player>();
    }

    public override void Setup()
    {
        
    }

    public override async UniTask PlayAsync(IProgress<float> progress = null)
    {
        MmfPlayer.PlayFeedbacks();
        OnUpdateFeedbacks(progress).Forget();
        await UniTask.Delay(TimeSpan.FromSeconds(TotalDuration), cancellationToken: destroyCancellationToken);
    }

    public override void Play(IProgress<float> progress = null)
    {
        PlayAsync(progress).Forget();
    }

    public override void Stop()
    {
        MmfPlayer.StopFeedbacks();
        UpdateCancellationTokenSource?.Cancel();
        UpdateCancellationTokenSource?.Dispose();
    }
    private async UniTask OnUpdateFeedbacks(IProgress<float> progress = null)
    {
        if (progress == null) return;
        UpdateCancellationTokenSource = new CancellationTokenSource();
        progress.Report(0);
        DateTime startTime = DateTime.Now;
        await foreach (AsyncUnit _ in UniTaskAsyncEnumerable.EveryUpdate().WithCancellation(UpdateCancellationTokenSource.Token))
        {
            progress.Report((float)((DateTime.Now - startTime).TotalSeconds / TotalDuration));
        }
    }

    private void OnDestroy()
    {
        UpdateCancellationTokenSource?.Cancel();
        UpdateCancellationTokenSource?.Dispose();
    }
}