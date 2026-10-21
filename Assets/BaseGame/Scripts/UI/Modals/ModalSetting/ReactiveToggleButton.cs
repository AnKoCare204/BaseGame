using System.Collections.Generic;
using GameEnum;
using R3;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ReactiveToggleButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] List<GameObject> toggleStates;
    [SerializeField] private SerializableReactiveProperty<bool> state;
    public UnityAction<SettingType, bool> OnSwitchState;
    SettingType settingType;

    private void Start()
    {
        button.onClick.AddListener(SwitchState);
    }

    public void AddListener(SerializableReactiveProperty<bool> state, UnityAction<SettingType, bool> OnSwitchState, SettingType settingType)
    {
        this.settingType = settingType;
        this.state = state;
        this.OnSwitchState = OnSwitchState;
        SetStateVisual();
    }
    
    void SwitchState()
    {
        OnSwitchState?.Invoke(settingType, !state.Value);
        SetStateVisual();
    }

    void SetStateVisual()
    {
        toggleStates[0].SetActive(state.Value);
        toggleStates[1].SetActive(!state.Value);
    }
}
