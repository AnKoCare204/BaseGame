using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

public class SkeletonAutoAnim : MonoBehaviour
{
    [field: SerializeField] public SkeletonGraphic SkeletonGraphic { get; set; }
    [field: SerializeField] public string StartAnimationName { get; set; } = "start";
    [field: SerializeField] public string IdleAnimationName { get; set; } = "idle";

    [field: SerializeField, Tooltip("Chi chay start dung 1 lan luc khoi tao, sau do idle loop mai mai.")]
    public bool PlayStartOnce { get; set; }

    private void Start()
    {
        SkeletonGraphic.AnimationState.ClearTrack(0);
        SkeletonGraphic.AnimationState.SetAnimation(0, StartAnimationName, false);
        SkeletonGraphic.AnimationState.AddAnimation(0, IdleAnimationName, true, 0f);
        if (PlayStartOnce) return;
        RandomAnimation().Forget();
    }

    private async UniTask RandomAnimation()
    {
        int random = Random.Range(2, 3);
        await UniTask.WaitForSeconds(random, cancellationToken: this.GetCancellationTokenOnDestroy());
        await WaitForCurrentLoopEnd();
        SkeletonGraphic.AnimationState.ClearTrack(0);
        SkeletonGraphic.AnimationState.SetAnimation(0, StartAnimationName, false);
        SkeletonGraphic.AnimationState.AddAnimation(0, IdleAnimationName, true,0f);
        await UniTask.WaitForSeconds(10f, cancellationToken: this.GetCancellationTokenOnDestroy());
        RandomAnimation().Forget();
    }

    /// <summary>
    /// ClearTrack cuts whatever pose "idle" happens to be in mid-loop, which pops visibly when "start"
    /// begins from its own frame 0. Idle's own loop already returns to that same frame 0 pose once per
    /// cycle - waiting until then makes the cut land on a seam instead of an arbitrary frame.
    /// </summary>
    private async UniTask WaitForCurrentLoopEnd()
    {
        Spine.TrackEntry current = SkeletonGraphic.AnimationState.GetCurrent(0);
        if (current == null || current.Animation == null) return;

        float remaining = current.Animation.Duration - current.AnimationTime;
        if (remaining <= 0f) return;
        await UniTask.WaitForSeconds(remaining, cancellationToken: this.GetCancellationTokenOnDestroy());
    }
}
