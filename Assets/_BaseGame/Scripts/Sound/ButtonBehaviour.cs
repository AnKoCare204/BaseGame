using System;
using LitMotion;
using Lofelt.NiceVibrations;
using Manager;
using TW.ACacheEverything;
using TW.Utility.CustomComponent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI
{
    [RequireComponent(typeof(Button))]
    public partial class ButtonBehaviour : ACachedMonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [field: SerializeField] private bool IsDisableAnim { get; set; }
        [field: SerializeField] private Button MainButton { get; set; }
        [field: SerializeField] private SoundType ButtonSound = SoundType.ButtonClick;
        private MotionHandle ButtonMotion { get; set; }
        private float CurrentProgress { get; set; }
        private void Reset()
        {
            MainButton = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (MainButton.interactable == false) return;
            if (!IsDisableAnim)
            {
                ButtonMotion.TryCancel();
                ButtonMotion = LMotion.Create(CurrentProgress, 0f, 0.15f)
                    .Bind(OnProgressUpdateCache)
                    .AddTo(this);
            }

            VibrationManager.Instance.CallHaptic(HapticPatterns.PresetType.MediumImpact);
            SoundManager.Instance.PlaySFX(ButtonSound);
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (MainButton.interactable == false) return;
            if (!IsDisableAnim)
            {
                ButtonMotion.TryCancel();
                ButtonMotion = LMotion.Create(CurrentProgress, 1f, 0.15f)
                    .WithEase(Ease.InOutSine)
                    .Bind(OnProgressUpdateCache)
                    .AddTo(this);
            }
        }
        
        [ACacheMethod]
        private void OnProgressUpdate(float progress)
        {
            CurrentProgress = progress;
            MainButton.transform.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, progress);
        }
    }
}