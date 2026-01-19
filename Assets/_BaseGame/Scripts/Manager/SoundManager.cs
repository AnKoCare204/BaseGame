using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundType
{
    None,
    GamePlay1,
    ButtonClick,
    CoinClaim,
    GemClaim,
    TabClick,
    BgMainMenu,
}

public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    public static SoundManager Instance => _instance;
    [SerializeField] private bool isPlayMusicAwake = true;
    public SoundList[] musicSounds, sfxSound;
    public AudioSource musicSource, sfxSource;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Destroy(this.gameObject);
        }

    }

    private void Start()
    {
        musicSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        sfxSource.volume = PlayerPrefs.GetFloat("SoundVolume", 1f);

        if(isPlayMusicAwake)
        {
            PlayMusic(SoundType.GamePlay1);
        }
    }


    public void PlayMusic(SoundType type)
    {
        SoundList sound = Array.Find(musicSounds, x => x.type == type);
        if (sound == null)
        {
            Debug.LogError("Sound Error: " + type.ToString());
        }
        else
        {
            musicSource.clip = sound.clip;
            musicSource.Play();
        }
    }

    public void PlaySFX(SoundType type)
    {
        SoundList sound = Array.Find(sfxSound, x => x.type == type);
        if (sound == null)
        {
            Debug.LogError("Sound Error: " + type.ToString());
        }
        else
        {
            sfxSource.PlayOneShot(sound.clip);
            sfxSource.Play();
        }
    }
}
