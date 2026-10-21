using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Sirenix.OdinInspector;
using TW.Utility.CustomComponent;
using UnityEngine;

/// <summary>
/// Code-driven version of the old MMF_Player claim animation.
/// The animation is split into sequential steps; calling <see cref="Skip"/> instantly
/// completes the tweens of the current step and jumps to the next one,
/// so the user can fast-forward through the animation step by step.
/// All curves are extracted from the original MMF_Player feedbacks.
/// </summary>
public class UIStreakClaimPointAnim : ABetterMonoBehaviour
{
    [field: Header("Targets")]
    [field: SerializeField] public Transform Root { get; private set; }
    [field: SerializeField] public Transform Star { get; private set; }
    [field: SerializeField] public Transform Fire { get; private set; }
    [field: SerializeField] public Transform HitPoint { get; private set; }
    [field: SerializeField] public Transform FireDestination { get; private set; }
    [field: SerializeField] public GameObject StartParticle { get; private set; }
    [field: SerializeField] public GameObject TextParticle { get; private set; }
    [field: SerializeField] public GameObject HitParticle { get; private set; }
    [field: SerializeField] public GameObject TextBase { get; private set; }
    [field: SerializeField] public GameObject TextCurrent { get; private set; }

    [field: Header("Step Durations")]
    [field: SerializeField] public float PopStepDuration { get; private set; } = 0.1f;
    [field: SerializeField] public float ShowUpStepDuration { get; private set; } = 0.2f;
    [field: SerializeField] public float FireChargeStepDuration { get; private set; } = 0.5f;
    [field: SerializeField] public float FireFlyStepDuration { get; private set; } = 0.5f;
    [field: SerializeField] public float AddPointStepDuration { get; private set; } = 0.15f;
    [field: SerializeField] public float SwapTextStepDuration { get; private set; } = 0.4f;
    [field: SerializeField] public float ExitStepDuration { get; private set; } = 0.4f;

    [field: Header("Star Show (scale 0 -> 2, bounce)")]
    [field: SerializeField] public float StarShowDuration { get; private set; } = 0.7f;
    [field: SerializeField] public AnimationCurve StarShowCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.4607921f, 2.2662182f, 1.962666f, 1.962666f),
        new Keyframe(0.75256026f, 1.9198645f, -1.2149023f, -1.2149023f),
        new Keyframe(0.874503f, 2.0996044f, 1.5390646f, 1.5390646f),
        new Keyframe(1f, 2f, 0f, 0f));

    [field: Header("Fire Show (scale 0 -> 1, overshoot)")]
    [field: SerializeField] public float FireShowDuration { get; private set; } = 0.5f;
    [field: SerializeField] public AnimationCurve FireShowCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.50171614f, 1.2983837f, 1.962666f, 1.962666f),
        new Keyframe(1f, 1f, 0f, 0f));

    [field: Header("Fire Fly (arc to destination, scale holds then vanishes)")]
    [field: SerializeField] public AnimationCurve FireFlyProgressCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));
    [field: SerializeField] public AnimationCurve FireFlyArcCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f, 1.3310287f, 1.3310287f),
        new Keyframe(0.4990738f, 0.2714259f, 0f, 0f),
        new Keyframe(1f, -0.3f, -2.6564875f, -2.6564875f));
    [field: SerializeField] public float FireFlyArcHeight { get; private set; } = 310f;
    [field: SerializeField] public AnimationCurve FireFlyScaleCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.95f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -19.999996f, -19.999996f));

    [field: Header("Star Punch (squash & stretch around scale 2)")]
    [field: SerializeField] public float StarPunchDuration { get; private set; } = 0.3f;
    [field: SerializeField] public float StarPunchScale { get; private set; } = 2f;
    [field: SerializeField] public AnimationCurve StarPunchCurveX { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 1f, 0.4000001f, 0.4000001f),
        new Keyframe(0.5f, 1.2f, 0f, 0f),
        new Keyframe(1f, 1f, -0.4000001f, -0.4000001f));
    [field: SerializeField] public AnimationCurve StarPunchCurveY { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 1f, -0.39999998f, -0.39999998f),
        new Keyframe(0.5f, 0.8f, 0f, 0f),
        new Keyframe(1f, 1f, 0.39999998f, 0.39999998f));

    [field: Header("Exit (root sways, star shrinks to 0)")]
    [field: SerializeField] public AnimationCurve RootSwayCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f, -2.693056f, -2.693056f),
        new Keyframe(0.50022584f, -0.5001755f, 0f, 0f),
        new Keyframe(1f, 0f, 2.0014746f, 2.0014746f));
    [field: SerializeField] public float RootSwayAmplitude { get; private set; } = 100f;
    [field: SerializeField] public float StarExitDuration { get; private set; } = 0.6f;
    [field: SerializeField] public AnimationCurve StarExitCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 2f, 0f, 0f),
        new Keyframe(0.7f, 2f, 0f, 0f),
        new Keyframe(1f, 0f, -12.99049f, -12.99049f));

    public bool IsPlaying { get; private set; }
    private bool SkipRequested { get; set; }
    private int RunId { get; set; }
    private List<MotionHandle> Handles { get; } = new List<MotionHandle>();
    private Vector3 RootStartLocalPosition { get; set; }
    private Vector3 FireStartLocalPosition { get; set; }

    private void Awake()
    {
        RootStartLocalPosition = Root.localPosition;
        FireStartLocalPosition = Fire.localPosition;
    }

    /// <summary>Skips the current step: jumps every running tween to its end state and moves on.</summary>
    [Button]
    public void Skip()
    {
        if (!IsPlaying) return;
        SkipRequested = true;
    }

    [Button]
    public async UniTask PlayAsync()
    {
        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        int runId = ++RunId;
        CompleteAllHandles();
        SkipRequested = false;
        IsPlaying = true;
        ResetState();

        // Step 1: particles on, swap real text -> anim text, star + fire hidden (scale 0)
        StepAppear();
        await WaitStep(PopStepDuration, ct);
        if (RunId != runId) return;

        // Step 2: appear sound + star pops in with bounce (0 -> 2)
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.ClaimAppear);
        ScaleAlongCurve(Star, StarShowCurve, 1f, StarShowDuration);
        await WaitStep(ShowUpStepDuration, ct);
        if (RunId != runId) return;

        // Step 3: fire pops in with overshoot (0 -> 1)
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StarAdd);
        StartParticle.SetActive(false);
        ScaleAlongCurve(Fire, FireShowCurve, 1f, FireChargeStepDuration);
        await WaitStep(FireChargeStepDuration, ct);
        if (RunId != runId) return;

        // Step 4: fire flies along an arc to the text and vanishes on arrival
        StepFireFly();
        await WaitStep(FireFlyStepDuration, ct);
        if (RunId != runId) return;

        // Step 5: point added, star squash & stretch punch
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StarAdd);
        StepStarPunch();
        await WaitStep(AddPointStepDuration, ct);
        if (RunId != runId) return;

        // Step 6: swap anim text back to the real (updated) text
        TextBase.SetActive(true);
        TextCurrent.SetActive(false);
        TextParticle.SetActive(false);
        await WaitStep(SwapTextStepDuration, ct);
        if (RunId != runId) return;

        // Step 7: exit (hit effect at hit point, root sways, star shrinks to 0)
        StepExit();
        await WaitStep(ExitStepDuration, ct);
        if (RunId != runId) return;

        // Step 8: cleanup
        HitParticle.SetActive(false);
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StarClaim);
        CompleteAllHandles();
        IsPlaying = false;
    }

    private void ResetState()
    {
        Root.localPosition = RootStartLocalPosition;
        Fire.localPosition = FireStartLocalPosition;
        Star.localScale = Vector3.zero;
        Fire.localScale = Vector3.zero;
    }

    private void StepAppear()
    {
        StartParticle.SetActive(true);
        TextParticle.SetActive(true);
        HitParticle.SetActive(true);
        TextCurrent.SetActive(true);
        TextBase.SetActive(false);
    }

    private void StepFireFly()
    {
        Vector3 from = Fire.position;
        Vector3 to = FireDestination.position;
        float arcEnd = FireFlyArcCurve.Evaluate(1f);
        Track(LMotion.Create(0f, 1f, FireFlyStepDuration)
            .WithEase(Ease.Linear)
            .Bind(time =>
            {
                float progress = FireFlyProgressCurve.Evaluate(time);
                // Subtract the linear component so the arc offset is 0 at both ends
                // and the fire always lands exactly on the destination.
                float arc = (FireFlyArcCurve.Evaluate(time) - time * arcEnd) * FireFlyArcHeight;
                Vector3 position = Vector3.LerpUnclamped(from, to, progress);
                position.y += arc;
                Fire.position = position;
            })
            .AddTo(gameObject));
        ScaleAlongCurve(Fire, FireFlyScaleCurve, 1f, FireFlyStepDuration);
    }

    private void StepStarPunch()
    {
        Track(LMotion.Create(0f, 1f, StarPunchDuration)
            .WithEase(Ease.Linear)
            .Bind(time => Star.localScale = new Vector3(
                StarPunchCurveX.Evaluate(time) * StarPunchScale,
                StarPunchCurveY.Evaluate(time) * StarPunchScale,
                StarPunchScale))
            .AddTo(gameObject));
    }

    private void StepExit()
    {
        HitParticle.transform.position = HitPoint.position;
        float baseX = Root.localPosition.x;
        Track(LMotion.Create(0f, 1f, ExitStepDuration)
            .WithEase(Ease.Linear)
            .Bind(time =>
            {
                Vector3 position = Root.localPosition;
                position.x = baseX + RootSwayCurve.Evaluate(time) * RootSwayAmplitude;
                Root.localPosition = position;
            })
            .AddTo(gameObject));
        ScaleAlongCurve(Star, StarExitCurve, 1f, StarExitDuration);
    }

    private void ScaleAlongCurve(Transform target, AnimationCurve curve, float multiplier, float duration)
    {
        Track(LMotion.Create(0f, 1f, duration)
            .WithEase(Ease.Linear)
            .Bind(time => target.localScale = Vector3.one * (curve.Evaluate(time) * multiplier))
            .AddTo(gameObject));
    }

    private void Track(MotionHandle handle)
    {
        Handles.Add(handle);
    }

    private async UniTask WaitStep(float duration, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (SkipRequested) break;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            elapsed += Time.deltaTime;
        }
        if (SkipRequested)
        {
            SkipRequested = false;
            CompleteAllHandles();
        }
        Handles.RemoveAll(handle => !handle.IsActive());
    }

    private void CompleteAllHandles()
    {
        for (int i = 0; i < Handles.Count; i++)
        {
            Handles[i].TryComplete();
        }
        Handles.Clear();
    }
}
