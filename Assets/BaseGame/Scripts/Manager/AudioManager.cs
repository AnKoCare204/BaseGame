using System;
using System.Collections.Generic;
using CoreData;
using Cysharp.Threading.Tasks;
using GameEnum;
using LitMotion;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using TW.Utility.Extension;
using UnityEngine;

/// <summary>
/// Khóa âm thanh. Giá trị số được serialize trong AudioManager.prefab và ButtonBehaviour.ClickSound nên không đổi số của key cũ.
/// Danh sách này là nguồn cho SoundRework/SoundList.tsv (bảng đặt hàng sound).
/// </summary>
public enum AudioKey
{
    // ===== Nhạc nền =====
    BgMainMenu = 0,
    BgGamePlay = 1,

    // ===== UI chung =====
    SfxButtonClick = 10,
    SfxWin = 13,
    SfxLose = 14,
    SfxComplete = 15,
    SfxCoin = 16,        // hạt xu chạm đích trong CoinEffectUI, nhiều biến thể random
    SfxCoinPick = 30,    // hạt xu chạm ví trong CoinChangeEffect
    SfxRewardClaim = 32,
    SfxCoinPop = 38,     // chùm xu bung ra lúc bắt đầu hiệu ứng xu bay

    // ===== FishSort gameplay =====
    SfxFishTap = 50,
    SfxTankComplete = 53,
    SfxBubblePop = 54,
    SfxBubbleSpawn = 55,
    SfxTankDrop = 56,
    SfxHoldingWarning = 57,
    SfxHoldingFull = 58,
    SfxBoardClear = 59,
    SfxTapDenied = 60,
    SfxFishLand = 61,   // cá đáp xuống: dùng chung cho ô chờ và bể
    SfxTankLeave = 62,

    // ===== FishSort UI =====
    SfxPopupOpen = 70,
    SfxPopupClose = 71,
    SfxToggle = 73,

    // ===== FishSort tutorial =====
    SfxTutorialStep = 94,
    SfxTutorialDone = 95,

    // ===== FishSort booster (code đã gọi, prefab chưa gán clip) =====
    SfxBoosterAddSlot = 80,
    SfxBoosterShuffle = 81,
    SfxBoosterAutoFill = 82,
    SfxBoosterPackedUp = 83,
    SfxBagSuck = 84,
    SfxBoosterUnlockTank = 85,
    SfxBoosterSwap = 86,
    SfxBoosterUnlock = 87,
    /// <summary>Bóng khói (isIce) chạm đỉnh đủ lâu, lớp khói tan ra (BoardManager.TickIce).</summary>
    SfxSmokeClear = 88,
}

[Serializable]
public class AudioConfig
{
    [field: SerializeField] public AudioKey AudioKey { get; private set; }
    [field: SerializeField] public AudioClip[] AudioClip { get; private set; }
}

public class AudioManager : Singleton<AudioManager>
{
#if UNITY_EDITOR
    /// <summary>Chỉ trong editor: báo mỗi lần một key được gọi phát (key, trễ giây). SoundCaptureProbe dùng để quay clip minh hoạ cho bên làm sound.</summary>
    public static event Action<AudioKey, float> OnKeyPlayed;
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void NotifyPlayed(AudioKey audioKey, float delay = 0f)
    {
#if UNITY_EDITOR
        OnKeyPlayed?.Invoke(audioKey, delay);
#endif
    }

    [ShowInInspector] private SettingData SettingData => SettingData.Instance;

    [field: SerializeField] public AudioConfig[] SoundFxArray { get; private set; }
    [field: SerializeField] public AudioConfig[] MusicArray { get; private set; }

    // Mỗi cụm sfx có enum + mảng config riêng (AudioSfxKeys.cs), không dùng chung AudioKey.
    [field: Title("Event Sfx")]
    [field: SerializeField] public SfxConfig<StarBlazeSfx>[] StarBlazeSfxArray { get; private set; }
    
    [field: SerializeField] public AudioSource SoundFxAudioSource { get; private set; }
    [field: SerializeField] public AudioSource MusicAudioSource { get; private set; }
    [field: SerializeField] public SoundEmitter SoundEmitter { get; private set; }

    /// <summary>Mixer và âm lượng cho tiếng hạt xu chạm đích (PlayCoinEffect); Clip được gán lúc phát từ SfxCoin.</summary>
    [field: Title("SoundData")]
    [field: SerializeField] public SoundData CoinEffectSoundData { get; private set; }

    private AudioClip CurrentMusicAudioClip { get; set; }
    private MotionHandle ScaleVolumeHandle { get; set; }
    public bool IsSoundActive { get; private set; } = true;
    public bool IsMusicActive { get; private set; } = true;
    private float MusicVolume { get; set; }

    private void Start()
    {
        SetSoundFx(SettingData.GetSettingSubData(SettingType.Sound).Value);
        SetMusic(SettingData.GetSettingSubData(SettingType.Music).Value);
        MusicVolume = MusicAudioSource.volume;
        PlayMusic(AudioKey.BgMainMenu);
    }

    private AudioConfig GetFxAudioConfig(AudioKey audioKey) => FindConfig(SoundFxArray, audioKey);
    private AudioConfig GetMusicAudioConfig(AudioKey audioKey) => FindConfig(MusicArray, audioKey);

    private static AudioConfig FindConfig(AudioConfig[] configs, AudioKey audioKey)
    {
        for (int index = 0; index < configs.Length; index++)
        {
            if (configs[index].AudioKey == audioKey) return configs[index];
        }

        return null;
    }

    /// <summary>Phát một clip ngẫu nhiên của key qua SoundFxAudioSource (PlayOneShot). Key chưa gán clip trong prefab thì bỏ qua.</summary>
    public void PlaySoundFx(AudioKey audioKey, float delay = 0)
    {
        NotifyPlayed(audioKey, delay);
        AudioConfig audioConfig = GetFxAudioConfig(audioKey);
        if (audioConfig == null) return;
        PlaySoundDelay(audioConfig.AudioClip.GetRandomElement(), delay).Forget();
    }
    
    public void PlaySoundFx(StarBlazeSfx key, float delay = 0) => PlayBankSfx(StarBlazeSfxArray, key, delay);
    private void PlayBankSfx<TKey>(SfxConfig<TKey>[] bank, TKey key, float delay) where TKey : struct, Enum
    {
        SfxConfig<TKey> config = FindSfxConfig(bank, key);
        // Chưa gán clip (vd. SkyJump CoinCountStart/JumpToWater) → im lặng, giống hành vi cũ khi thiếu config.
        if (config?.AudioClip == null || config.AudioClip.Length == 0) return;
        PlaySoundDelay(config.AudioClip.GetRandomElement(), delay).Forget();
    }
    
    private static SfxConfig<TKey> FindSfxConfig<TKey>(SfxConfig<TKey>[] bank, TKey key) where TKey : struct, Enum
    {
        if (bank == null) return null;
        EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
        for (int index = 0; index < bank.Length; index++)
        {
            if (comparer.Equals(bank[index].Key, key)) return bank[index];
        }
        return null;
    }

    /// <summary>
    /// Phát clip của key với pitch riêng (bậc thang cao độ từ một clip). PlayOneShot dùng chung pitch của SoundFxAudioSource nên phải
    /// đi qua SoundEmitter; âm lượng và mixer lấy theo SoundFxAudioSource để nghe đồng đều với PlaySoundFx. volumeScale &lt; 1 để phát nhỏ
    /// hơn bình thường (biến thể "nhẹ" của một clip có sẵn).
    /// </summary>
    public void PlaySoundFxPitched(AudioKey audioKey, float pitch, float volumeScale = 1f)
    {
        NotifyPlayed(audioKey);
        AudioConfig audioConfig = GetFxAudioConfig(audioKey);
        if (audioConfig == null) return;
        SoundData soundData = new SoundData
        {
            SoundDataType = SoundDataType.Active,
            Clip = audioConfig.AudioClip.GetRandomElement(),
            MixerGroup = SoundFxAudioSource.outputAudioMixerGroup,
            Volume = SoundFxAudioSource.volume * volumeScale,
        };
        PlaySound(soundData)?.ChangePitch(pitch);
    }

    /// <summary>Tiếng hạt xu chạm đích (CoinEffectUI gọi mỗi hạt): clip ngẫu nhiên của SfxCoin, mixer và âm lượng theo CoinEffectSoundData.</summary>
    public void PlayCoinEffect()
    {
        NotifyPlayed(AudioKey.SfxCoin);
        AudioConfig audioConfig = GetFxAudioConfig(AudioKey.SfxCoin);
        if (audioConfig == null) return;
        CoinEffectSoundData.Clip = audioConfig.AudioClip.GetRandomElement();
        PlaySound(CoinEffectSoundData);
    }

    private void PlayMusic(AudioKey audioKey)
    {
        NotifyPlayed(audioKey);
        AudioConfig audioConfig = GetMusicAudioConfig(audioKey);
        if (audioConfig == null) return;
        AudioClip newMusicAudioClip = audioConfig.AudioClip.GetRandomElement();
        if (CurrentMusicAudioClip == newMusicAudioClip) return;
        CurrentMusicAudioClip = newMusicAudioClip;
        MusicAudioSource.clip = CurrentMusicAudioClip;
        MusicAudioSource.Play();
    }

    /// <summary>Đổi nhạc nền: fade out nửa <paramref name="time"/>, đổi clip rồi fade in 0.2 s. Cùng clip đang phát thì không làm gì.</summary>
    public void ChangeMusic(AudioKey audioKey, float time)
    {
        NotifyPlayed(audioKey);
        ScaleVolumeHandle.TryCancel();
        AudioConfig audioConfig = GetMusicAudioConfig(audioKey);
        if (audioConfig == null) return;
        AudioClip newMusicAudioClip = audioConfig.AudioClip.GetRandomElement();
        if (CurrentMusicAudioClip == newMusicAudioClip) return;
        CurrentMusicAudioClip = newMusicAudioClip;
        ScaleVolumeHandle = LMotion.Create(MusicAudioSource.volume, 0f, time / 2f)
            .WithOnComplete(OnMusicScaleVolumeToAZero)
            .Bind(SetMusicAudioSourceVolume);
    }

    private SoundEmitter PlaySound(SoundData soundData)
    {
        if (!soundData.IsActive) return null;
        SoundEmitter soundEmitter = SoundEmitter.Get(Transform.position, Quaternion.identity, transform)
            .Initialize(soundData)
            .WithMute(!IsSoundActive);
        soundEmitter.Play().Forget();
        return soundEmitter;
    }

    private void OnMusicScaleVolumeToAZero()
    {
        ScaleVolumeHandle.TryCancel();
        MusicAudioSource.clip = CurrentMusicAudioClip;
        MusicAudioSource.Play();
        ScaleVolumeHandle = LMotion.Create(0f, MusicVolume, 0.2f)
            .Bind(SetMusicAudioSourceVolume);
    }

    private void SetMusicAudioSourceVolume(float x)
    {
        MusicAudioSource.volume = x;
    }

    private async UniTask PlaySoundDelay(AudioClip audioClip, float delay = 0)
    {
        if (delay < 0.01f)
        {
            SoundFxAudioSource.PlayOneShot(audioClip);
            return;
        }

        await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        SoundFxAudioSource.PlayOneShot(audioClip);
    }

    public void SetSoundFx(bool value)
    {
        if (IsSoundActive == value) return;
        IsSoundActive = value;
        SettingData.SetSettingValue(SettingType.Sound, value);
        SoundFxAudioSource.mute = !value;
    }

    public void SetMusic(bool value)
    {
        if (IsMusicActive == value) return;
        IsMusicActive = value;
        SettingData.SetSettingValue(SettingType.Music, value);
        MusicAudioSource.mute = !value;
    }
}
