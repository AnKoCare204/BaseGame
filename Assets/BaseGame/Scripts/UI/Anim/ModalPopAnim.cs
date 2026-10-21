using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.Shared;
using UnityEngine;

namespace BaseGame.Scripts.UI.Anim
{
    public class ModalPopAnim : SimpleTransitionAnimationBehaviour
    {
        public CanvasGroup mainCanvasGroup;
        public Transform targetTransform;
        public bool playSound = true;

        public override async UniTask PlayAsync(IProgress<float> progress = null)
        {
            if (playSound && AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxPopupClose);
            if (targetTransform != null)
            {
                LMotion.Create(1f, 0f, 0.25f).WithEase(Ease.InBack).Bind(x => targetTransform.localScale = Vector3.one * x).AddTo(targetTransform);
                
            }
            
            if (mainCanvasGroup == null)
                mainCanvasGroup = GetComponent<CanvasGroup>();

            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.interactable = false;
                await LMotion.Create(1f, 0f, 0.25f)
                    .Bind(x => mainCanvasGroup.alpha = x)
                    .AddTo(RectTransform);
            }
        }
    }
}
