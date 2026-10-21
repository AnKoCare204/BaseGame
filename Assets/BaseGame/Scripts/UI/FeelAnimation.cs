using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using TW.UGUI.Shared;
using UnityEngine;

[RequireComponent(typeof(MMF_Player))]
public class FeelAnimation : TransitionAnimationBehaviour
{
    [field: SerializeField] public MMF_Player MmfPlayer {get; private set;}
    public override float TotalDuration => MmfPlayer.TotalDuration;
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
        await UniTask.Delay(TimeSpan.FromSeconds(TotalDuration), cancellationToken: destroyCancellationToken);
    }

    public override void Play(IProgress<float> progress = null)
    {
        PlayAsync(progress).Forget();
    }

    public override void Stop()
    {
        MmfPlayer.StopFeedbacks();
    }
    public bool IsPlaying()
    {
        return MmfPlayer.IsPlaying;
    }
}