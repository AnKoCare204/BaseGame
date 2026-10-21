using System;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BaseGame.Scripts.UI
{
    public class SkeletonGraphicAuto: MonoBehaviour
    {
        [field: SerializeField] public SkeletonGraphic SkeletonGraphic { get; set; }

        private void Start()
        {
            SkeletonGraphic.AnimationState.ClearTrack(0);
            SkeletonGraphic.AnimationState.SetAnimation(0, "idle", true);
            RandomAnimation().Forget();
        }

        private async UniTask RandomAnimation()
        {
            int random = Random.Range(5, 10);
            await UniTask.WaitForSeconds(random, cancellationToken: this.GetCancellationTokenOnDestroy());
            SkeletonGraphic.AnimationState.ClearTrack(0);
            SkeletonGraphic.AnimationState.SetAnimation(0, "action", false);
            SkeletonGraphic.AnimationState.AddAnimation(0, "idle", true,0f);
            await UniTask.WaitForSeconds(10f, cancellationToken: this.GetCancellationTokenOnDestroy());
            RandomAnimation().Forget();
        }
    }
}