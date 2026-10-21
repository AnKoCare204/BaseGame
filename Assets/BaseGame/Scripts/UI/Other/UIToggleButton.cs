using System;
using LitMotion;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Other
{
    public class UIToggleButton : MonoBehaviour
    {
        [field: SerializeField] public Button MainButton { get; private set; }
        [field: SerializeField] public GameObject[] ActiveGroup { get; private set; }
        [field: SerializeField] public GameObject[] DeActiveGroup { get; private set; }
        [field: SerializeField] public RectTransform Switch { get; private set; }
        [field: SerializeField] public SerializableReactiveProperty<bool> ReactiveValue { get; private set; }
        private Vector3[] SwitchPosition { get; set; }
        public Action<bool> OnClickButton { get; set; }
        private MotionHandle MotionHandle { get; set; }
        private float CurrentProgress { get; set; } 

        private void Awake()
        {
            SwitchPosition = new Vector3[2]
            {
                new Vector3(Switch.localPosition.x, Switch.localPosition.y, Switch.localPosition.z),
                new Vector3(-Switch.localPosition.x, Switch.localPosition.y, Switch.localPosition.z)
            };

            MainButton.SetOnClickDestination(ToggleSwitch);
        }
        public void InitValue(SerializableReactiveProperty<bool> reactiveValue)
        {
            ReactiveValue = reactiveValue;
            UpdateSwitchProgress(ReactiveValue.Value ? 1f : 0f);
            ReactiveValue.Subscribe(OnReactiveValueChanged).AddTo(this);
        }
        private void ToggleSwitch()
        {
            ReactiveValue.Value = !ReactiveValue.Value;
            // Một clip, bật thì cao hơn, tắt thì trầm hơn. Phát trước OnClickButton để lúc tắt Sound vẫn nghe được tiếng gạt cuối.
            AudioManager.Instance.PlaySoundFxPitched(AudioKey.SfxToggle, ReactiveValue.Value ? 1.12f : 0.9f);
            OnClickButton?.Invoke(ReactiveValue.Value);
        }
        private void OnReactiveValueChanged(bool value)
        {
            MotionHandle.TryCancel();
            MotionHandle = LMotion.Create(CurrentProgress, ReactiveValue.Value ? 1f : 0f, 0.1f)
                .Bind(UpdateSwitchProgress)
                .AddTo(this);
        }
        private void UpdateSwitchProgress(float progress)
        {
            CurrentProgress = Mathf.Clamp01(progress);
            Switch.localPosition = Vector3.Lerp(SwitchPosition[0], SwitchPosition[1], CurrentProgress);
            for (int i = 0; i < ActiveGroup.Length; i++)
            {
                ActiveGroup[i].SetActive(CurrentProgress > 0.5f);
            }
            for (int i = 0; i < DeActiveGroup.Length; i++)
            {
                DeActiveGroup[i].SetActive(CurrentProgress <= 0.5f);
            }
        }
    }
}