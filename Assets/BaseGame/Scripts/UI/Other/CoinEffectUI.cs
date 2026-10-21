using System;
using AssetKits.ParticleImage;
using Manager;
using UnityEngine;
using UnityEngine.Events;

public class CoinEffectUI : MonoBehaviour
{
    [field: SerializeField] public ParticleImage ParticleImage { get; set; }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    public CoinEffectUI OnInit(Vector3 posStart, Transform target, UnityAction callbackOnFirst, UnityAction callbackOnLast)
    {
        transform.position = posStart;
        ParticleImage.attractorTarget = target;
        ParticleImage.onFirstParticleFinish.AddListener(callbackOnFirst);
        ParticleImage.onLastParticleFinish.AddListener(callbackOnLast);
        ParticleImage.onParticleFinish.AddListener(OnParticleFinish);
        gameObject.SetActive(true);
        PlayAnimation();
        return this;
    }
    
    private void PlayAnimation()
    {
        AudioManager.Instance.PlaySoundFx(AudioKey.SfxCoinPop);
        ParticleImage.Play();
    }

    private void OnParticleFinish()
    {
        AudioManager.Instance.PlayCoinEffect();
    }
}
