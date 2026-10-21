using System;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

public class FeelAnimationWithSkip : MonoBehaviour
{
	[field: SerializeField] private MMF_Player Main {get; set;}
	[field: SerializeField] private float AnimDuration {get; set;}
	[field: SerializeField] public bool IsPlaying {get; private set;}
	[field: SerializeField] public bool CanSkip {get; set;}
	[field: SerializeField] public float DurationCanSkip {get; set;}
	private float PlayStartTime { get; set; }
	[Button]
	public async UniTask PlayAsync()
	{
		Main.PlayFeedbacks();
		IsPlaying = true;
		PlayStartTime = Time.time;
		await WaitAnimationComplete();
		IsPlaying = false;
	}

	public async UniTask WaitAnimationComplete()
	{
		UniTask task1 = UniTask.Delay(TimeSpan.FromSeconds(AnimDuration),
			cancellationToken: this.GetCancellationTokenOnDestroy());
		UniTask task2 = UniTask.WaitUntil(IsAnimationStop, cancellationToken: this.GetCancellationTokenOnDestroy());
		await UniTask.WhenAll(task1, task2);
	}
	private bool IsAnimationStop()
	{
		return !Main.IsPlaying;
	}
	[Button]
	public bool Skip()
	{
		if (!CanSkip) return false;
		if (!IsPlaying) return false;
		if (Time.time - PlayStartTime < DurationCanSkip) return false;
		Main.SkipToTheEnd();
		IsPlaying = false;
		return true;
	}
}