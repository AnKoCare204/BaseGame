using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class SoundData
{
    public SoundDataType SoundDataType;
    [ShowIf("IsActive")] public AudioClip Clip;
    [ShowIf("IsActive")] public AudioMixerGroup MixerGroup;
    [ShowIf("IsActive")] public bool IsLoop;
    [ShowIf("IsActive")] public float Volume = 1f;
    public bool IsActive => SoundDataType == SoundDataType.Active;
}
public enum SoundDataType
{
    Inactive = 0,
    Active = 1,
}