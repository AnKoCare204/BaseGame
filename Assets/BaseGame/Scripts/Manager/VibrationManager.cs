using CoreData;
using GameEnum;
using Lofelt.NiceVibrations;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;

public class VibrationManager : Singleton<VibrationManager>
{
    [ShowInInspector] private SettingData GameSettingData => SettingData.Instance;
    public bool IsVibrationActive { get; private set; } = true;

    private void Start()
    {
        SetVibration(GameSettingData.GetSettingSubData(SettingType.Vibration).Value);
    }
    
    public void SetVibration(bool value)
    {
        if (IsVibrationActive == value) return;
        IsVibrationActive = value;
        GameSettingData.SetSettingValue(SettingType.Vibration, value);

    }

    public void CallHaptic(HapticType presetType)
    {
        if (!IsVibrationActive) return;
        HapticPatterns.PlayPreset((HapticPatterns.PresetType)presetType);
    }
}

public enum HapticType
{
    Selection = HapticPatterns.PresetType.Selection,
    Success = HapticPatterns.PresetType.Success,
    Warning = HapticPatterns.PresetType.Warning,
    Failure = HapticPatterns.PresetType.Failure,
    LightImpact = HapticPatterns.PresetType.LightImpact,
    MediumImpact = HapticPatterns.PresetType.MediumImpact,
    HeavyImpact = HapticPatterns.PresetType.HeavyImpact,
    RigidImpact = HapticPatterns.PresetType.RigidImpact,
    SoftImpact = HapticPatterns.PresetType.SoftImpact,
}