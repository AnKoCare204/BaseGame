using System;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using Core.UI.Modals;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.Utility.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

public class UIMoneyResourceEffect : MonoBehaviour
{
    private static readonly List<UIMoneyResourceEffect> EffectMap = new List<UIMoneyResourceEffect>();
    public static int Tracking = 0;
    public static void TryPlayAnim(Vector3 position, int fromCoin, int toCoin)
    {
        if (Tracking != 0) return;
        float minDistance = float.MaxValue;
        UIMoneyResourceEffect closestEffect = null;
        for (int i = 0; i < EffectMap.Count; i++)
        {
            UIMoneyResourceEffect effect = EffectMap[i];
            if (!effect.isActiveAndEnabled) continue;
            float distance = Vector3.Distance(effect.transform.position, position);
            if (distance > minDistance) continue;
            minDistance = distance;
            closestEffect = EffectMap[i];
        }

        if (closestEffect == null) return;
        closestEffect.PlayAnim(position, fromCoin, toCoin);
    }

    [field: SerializeField] private GameObject OriginGroup { get; set; }
    [field: SerializeField] private GameObject FakeGroup { get; set; }
    [field: SerializeField] private TextMeshProUGUI TextMeshProUGUI { get; set; }
    [field: SerializeField] private Transform IconImage { get; set; }
    [field: SerializeField] private ParticleImage ParticleCoin { get; set; }
    [field: SerializeField] private AnimationCurve ScaleCurve { get; set; }
    private List<CoinChangeEffect> CoinChangeEffects { get; set; } = new List<CoinChangeEffect>();

    private void Awake()
    {
        EffectMap.Add(this);
    }

    private void OnDestroy()
    {
        EffectMap.Remove(this);
    }

    public void PlayAnim(Vector3 position, int fromCoin, int toCoin)
    {
        CoinChangeEffect newCoinChangeEffect = new CoinChangeEffect
        {
            From = fromCoin,
            To = toCoin,
            Position = position,
            TextMeshProUGUI = TextMeshProUGUI,
            ParticleCoin = Instantiate(ParticleCoin, ParticleCoin.transform.parent),
            OriginGroup = OriginGroup,
            IconImage = IconImage,
            ScaleCurve = ScaleCurve,
            OnEffectComplete = OnEffectComplete
        };
        newCoinChangeEffect.Init().Play();
        if (CoinChangeEffects.Count == 0)
        {
            TextMeshProUGUI.SetText(fromCoin.ToString());
            OriginGroup.SetActive(false);
            FakeGroup.SetActive(true);
        }

        CoinChangeEffects.Add(newCoinChangeEffect);
    }

    private void OnEffectComplete()
    {
        for (int i = 0; i < CoinChangeEffects.Count; i++)
        {
            if (CoinChangeEffects[i].IsPlaying) return;
        }

        foreach (CoinChangeEffect coinChangeEffect in CoinChangeEffects)
        {
            coinChangeEffect.Clean();
        }

        CoinChangeEffects.Clear();

        OriginGroup.SetActive(true);
        FakeGroup.SetActive(false);
    }
}

public class PopupMoneyResourceEffectLifecycle : IModalLifecycleEventSimple, IActivityLifecycleEventSimple
{
    public UniTask Initialize(Memory<object> args)
    {
        UIMoneyResourceEffect.Tracking++;
        return UniTask.CompletedTask;
    }

    public UniTask Cleanup(Memory<object> args)
    {
        UIMoneyResourceEffect.Tracking--;
        return UniTask.CompletedTask;
    }
}
public class CoinChangeEffect
{
    public int From { get; set; }
    public int To { get; set; }
    public Vector3 Position { get; set; }
    public TextMeshProUGUI TextMeshProUGUI { get; set; }
    public ParticleImage ParticleCoin { get; set; }
    public GameObject OriginGroup { get; set; }
    public Transform IconImage { get; set; }
    private MotionHandle TextHandle { get; set; }
    private DateTime LastPlaySound { get; set; }
    public AnimationCurve ScaleCurve { get; set; }
    public bool IsPlaying { get; set; }
    public MotionHandle ScaleHandle { get; set; }
    public Action OnEffectComplete { get; set; }

    public CoinChangeEffect Init()
    {
        ParticleCoin.onFirstParticleFinish.AddListener(OnFirstParticleFinish);
        ParticleCoin.onParticleFinish.AddListener(OnParticleFinish);
        ParticleCoin.onLastParticleFinish.AddListener(OnLastParticleFinish);
        return this;
    }

    public void Play()
    {
        AudioManager.Instance.PlaySoundFx(AudioKey.SfxCoinPop);
        ParticleCoin.transform.position = Position;
        ParticleCoin.SetActive(true);
        ParticleCoin.Stop();
        ParticleCoin.Play();
        IsPlaying = true;
    }

    private void OnFirstParticleFinish()
    {
        TextHandle.TryCancel();
        TextHandle = LMotion.Create(0f, 1f, 1f)
            .WithEase(Ease.Linear)
            .Bind(OnTextAnimUpdate)
            .AddTo(OriginGroup);
    }

    private void OnTextAnimUpdate(float progress)
    {
        int currentCoin = (int)Mathf.Lerp(From, To, progress);
        TextMeshProUGUI.SetText(currentCoin.ToString());
    }

    private void OnParticleFinish()
    {
        DateTime utcNow = DateTime.UtcNow;
        if (utcNow - LastPlaySound > TimeSpan.FromSeconds(0.02f))
        {
            LastPlaySound = utcNow;
            AudioManager.Instance.PlaySoundFx(AudioKey.SfxCoinPick);
        }

        ScaleHandle.TryCancel();
        ScaleHandle = LMotion.Create(0f, 1f, 0.2f)
            .WithEase(Ease.Linear)
            .Bind(OnImageUpdate)
            .AddTo(OriginGroup);
    }

    private void OnImageUpdate(float progress)
    {
        if (IconImage == null || TextMeshProUGUI == null) return;
        IconImage.localScale = Vector3.one * ScaleCurve.Evaluate(progress);
        TextMeshProUGUI.transform.localScale = Vector3.one * ScaleCurve.Evaluate(progress);
    }

    private void OnLastParticleFinish()
    {
        ParticleCoin.SetActive(false);
        IsPlaying = false;
        OnEffectComplete?.Invoke();
    }

    public void Clean()
    {
        ScaleHandle.TryCancel();
        TextHandle.TryCancel();
        Object.Destroy(ParticleCoin.gameObject);
    }
}