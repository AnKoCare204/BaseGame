using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TW.Utility.CustomComponent;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(AudioSource))]
public class SoundEmitter : ALinkedPoolMonoBehaviour<SoundEmitter>
{
    [field: SerializeField]
    public SoundData Data { get; private set; }

    public AudioSource AudioSource;
    private CancellationTokenSource cancellationTokenSource;
    public float Volume => AudioSource.volume;
    private void Reset()
    {
        AudioSource = GetComponent<AudioSource>();
    }

    public SoundEmitter Initialize(SoundData data)
    {
        Data = data;
        AudioSource.clip = data.Clip;
        AudioSource.outputAudioMixerGroup = data.MixerGroup;
        AudioSource.loop = data.IsLoop;
        AudioSource.volume = data.Volume;
        AudioSource.pitch = 1;
        return this;
    }

    public async UniTask Play()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
        AudioSource.Play();
        // Debug.Break();
        await UniTask.WaitWhile(IsPlaying, cancellationToken: cancellationTokenSource.Token);
        Stop();
    }
    public bool IsPlaying()
    {
        if (AudioSource == null) return false;
        return AudioSource.isPlaying;
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        
        if (AudioSource == null) return;
        AudioSource.Stop();
        Release();
    }

    public SoundEmitter WithMute(bool mute)
    {
        AudioSource.mute = mute;
        return this;
    }

    public SoundEmitter WithRandomPitch(float min = -0.1f, float max = 0.1f)
    {
        AudioSource.pitch += Random.Range(min, max);
        return this;
    }
    public SoundEmitter ChangeVolume(float volume)
    {
        AudioSource.volume = volume;
        return this;
    }

    public SoundEmitter ChangePitch(float pitch)
    {
        AudioSource.pitch = pitch;
        return this;
    }
    
}