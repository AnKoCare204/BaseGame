using System;
using CoreData;
using GameEnum;
using Manager;
using R3;
using UnityEngine;

namespace Core.UI.Other
{
    public class UISettingsButton : MonoBehaviour
    {
        [field: SerializeField] private SettingType SettingType { get; set; }
        [field: SerializeField] private UIToggleButton UIToggleButton {get; set;}
        [field: SerializeField] private SerializableReactiveProperty<bool> CurrentValue { get; set; }
        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            UIToggleButton.InitValue(SettingData.Instance.GetSettingSubData(SettingType).Value);
            UIToggleButton.OnClickButton = OnClickButton;
        }
        private void OnClickButton(bool value)
        {
            switch (SettingType)
            {
                case SettingType.Sound:
                    AudioManager.Instance.SetSoundFx(value);
                    break;
                case SettingType.Music:
                    AudioManager.Instance.SetMusic(value);
                    break;
                case SettingType.Vibration:
                    VibrationManager.Instance.SetVibration(value);
                    break;
                case SettingType.LightMode:
                    SettingData.Instance.SetSettingValue(SettingType.LightMode, value);
                    break;
                default:
                    break;
            }
        }
    }
}