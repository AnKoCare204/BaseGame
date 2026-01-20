using System;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using TW.Utility.CustomComponent;
using TW.Utility.DesignPattern;
using UnityEngine;

public class UICoinEffect : ACachedMonoBehaviour
{
    [field: SerializeField] public ParticleImage ParticleImage { get; private set; }
    [field: SerializeField] public TextMeshProUGUI CoinText { get; private set; }
    [field: SerializeField] public AnimationCurve FadeAnimationCurve { get; private set; }
    [field: SerializeField] public AnimationCurve MoveAnimationCurve { get; private set; }
    private Vector3 StartPosition { get; set; }
    private Vector3 EndPosition { get; set; }
    private MotionHandle MotionHandle { get; set; }
    public void Play(int coin, Vector3 position)
    {
        Transform.position = position;
        ParticleImage.Play();
        CoinText.text = $"+{coin}";
        StartPosition = position;
        EndPosition = position + new Vector3(0, 200, 0);
        OnMotionUpdate(0);
        MotionHandle = LMotion.Create(0f, 1f, 3f)
            .WithEase(Ease.Linear)
            .WithOnComplete(OnMotionComplete)
            .Bind(OnMotionUpdate);
        // DelayAndDespawn(5).Forget();
    }
    private void OnMotionUpdate(float value)
    {
        float fadeValue = FadeAnimationCurve.Evaluate(value);
        float moveValue = MoveAnimationCurve.Evaluate(value);
        CoinText.alpha = fadeValue;
        Transform.position = Vector3.LerpUnclamped(StartPosition, EndPosition, moveValue);
    }
    private void OnMotionComplete()
    {
        SimplePool.DeSpawn(gameObject);
    }

    private async UniTask DelayAndDespawn(float delay)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        SimplePool.DeSpawn(gameObject);
    }
        
}