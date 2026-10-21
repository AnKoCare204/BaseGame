using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Lofelt.NiceVibrations;
using Manager;
using R3;
using UnityEngine;
using UnityEngine.UI;

public static class ButtonExtensions
{
    public static IDisposable SetOnClickDestination(this Button self, Action onClick)
    {
        Action onClickWrapper = () =>
        {
            onClick?.Invoke();
            // VibrationManager.Instance.CallHaptic(HapticPatterns.PresetType.LightImpact);
            // AudioManager.Instance.PlaySoundFx(AudioKey.SfxUIClickBtn);
        };
        return self.onClick
            .AsObservable()
            .Subscribe(_ => ActionExecuteRunner.AddExecuteAble(new PlayerAction(0, onClickWrapper)), null!)
            .AddTo(self);
    }
        
    public static IDisposable SetOnClickDestination(this Button self, Func<UniTask> onClick)
    {
        Func<UniTask> onClickWrapper = async () =>
        {
            // VibrationManager.Instance.CallHaptic(HapticPatterns.PresetType.LightImpact);
            // AudioManager.Instance.PlaySoundFx(AudioKey.SfxUIClickBtn);
            await onClick();
        };
        return self.onClick
            .AsObservable()
            .Subscribe(_ => ActionExecuteRunner.AddExecuteAble(new PlayerTask(0, onClickWrapper)), null!)
            .AddTo(self);
    }
}