using System;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

public class UIButtonAnim : MonoBehaviour
{
    [field: SerializeField] private SkeletonGraphic SkeletonGraphic {get; set;}

    private void Start()
    {
        if (SkeletonGraphic == null) return;
        SkeletonGraphic.AnimationState.SetAnimation(0, "idle", true);
        DelayAction().Forget();
    }

    private async UniTask DelayAction()
    {
        for (int i = 0; i < 1000; i++)
        {
            int random = UnityEngine.Random.Range(5000, 10000);
            await UniTask.Delay(random, cancellationToken: this.GetCancellationTokenOnDestroy());
            SkeletonGraphic.AnimationState.SetAnimation(0, "start", false);
            SkeletonGraphic.AnimationState.AddAnimation(0, "idle", true, 0f);
        }
    }
    
}