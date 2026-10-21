using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.Shared;
using UnityEngine;

public class TipBoosterHideAnim : SimpleTransitionAnimationBehaviour
{
    [field: SerializeField] public RectTransform ContainerView { get; private set; }
    [field: SerializeField] public AnimationCurve CurveScale { get; private set; }
    
    private MotionHandle motionScale;
    public override async UniTask PlayAsync(IProgress<float> progress = null)
    {
        motionScale.TryCancel();
        Vector3 startScale = ContainerView.localScale;
        Vector3 endScale = Vector3.zero;
        float duration = 0.2f;
        motionScale = LMotion.Create(startScale, endScale, duration).WithEase(CurveScale)
            .Bind(x => ContainerView.localScale = x)
            .AddTo(ContainerView);
        await motionScale.ToUniTask();
    }
}
