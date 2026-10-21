using System;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using LitMotion;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using TMPro;
using TW.Utility.Extension;
using UnityEngine;

public class UIStreakBar : MonoBehaviour
{
    public static Func<int, int, UniTask> PlayStreakAsync { get; private set; }
    public static Action<int> SetStreakAction { get; private set; }
    [field: SerializeField] private TextMeshProUGUI TextBonus { get; set; }
    [field: SerializeField] private Transform RootMaskGroup {get; set;}
    [field: SerializeField] private Transform[] RootMaskGroupPosition {get; set;}
    [field: SerializeField] private Transform[] StreakTransform {get; set;}
    [field: SerializeField] private Transform StreakCurrent {get; set;}
    [field: SerializeField] private CanvasGroup[] SelectGroup {get; set;}
    [field: SerializeField] private AnimationCurve[] SelectCurve {get; set;}
    [field: SerializeField] private int FromStreak {get; set;}
    [field: SerializeField] private int ToStreak {get; set;}
    [field: SerializeField] private AnimationCurve MaskGroupCurve {get; set;}
    [field: SerializeField] private AnimationCurve MoveCurve {get; set;}
    [field: SerializeField] private MMF_Player FireAnim {get; set;}
    [field: SerializeField] private ParticleImage FireParticle {get; set;}
    private MotionHandle MotionHandle { get; set; }
    private void Start()
    {
        PlayStreakAsync = PlayStreak;
        SetStreakAction = SetStreak;
        // StarBlazeManager.OnInitializeComplete += RefreshStreak;
        // RefreshStreak();
        RefreshStreak();
    }
    
    private void RefreshStreak()
    {
        int streak = Mathf.Min(StarBlazeManager.Instance.LastStreak, StarBlazeManager.Instance.CurrentStreak);
        Debug.Log(("Streak:" + streak));
        Debug.Log("LastStreak:" + StarBlazeManager.Instance.LastStreak);
        Debug.Log("CurrentStreak:" + StarBlazeManager.Instance.CurrentStreak);
        SetStreak(streak);
    }

    private void OnDestroy()
    {
        PlayStreakAsync = null;
        SetStreakAction = null;
        // StarBlazeManager.OnInitializeComplete -= RefreshStreak;
    }

    [Button]
    public void SetStreak(int streakCount)
    {
        int fixedStreak = Mathf.Clamp(streakCount, 0, StreakTransform.Length);
        int bonus = GetBonus(fixedStreak);
        TextBonus.text = $"x{bonus}";
        StreakCurrent.transform.position = StreakTransform[fixedStreak].position;
        UpdateSelectGroup(fixedStreak);
    }
    [Button]
    public async UniTask PlayStreak(int fromStreak, int toStreak)
    {
        ToStreak = Mathf.Clamp(fromStreak, 0, StreakTransform.Length);
        FromStreak = Mathf.Clamp(toStreak, 0, StreakTransform.Length);
        SetStreak(ToStreak);

        if (RootMaskGroupPosition.Length == 2)
        {
            MotionHandle.TryCancel();
            MotionHandle = LMotion.Create(0f, 1f, 0.5f)
                .Bind(OnMaskGroupUpdate)
                .AddTo(this);
            await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        
        MotionHandle.TryCancel();
        MotionHandle = LMotion.Create((float)ToStreak, FromStreak, 0.5f)
            .Bind(OnCurrentStreakUpdate)
            .AddTo(this);
        await UniTask.Delay(700, cancellationToken: this.GetCancellationTokenOnDestroy());
        FireAnim.PlayFeedbacks();
        await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
        FireParticle.SetActive(true);
        if (FireParticle.gameObject.activeInHierarchy)
        {
            FireParticle.Play();
            AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StreakIncrease);
        }


        SetStreak(FromStreak);
        await UniTask.Delay(700, cancellationToken: this.GetCancellationTokenOnDestroy());
        FireParticle.SetActive(false);
        if (RootMaskGroupPosition.Length == 2)
        {
            MotionHandle.TryCancel();
            MotionHandle = LMotion.Create(1f, 0f, 0.5f)
                .Bind(OnMaskGroupUpdate)
                .AddTo(this);
            await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        
    }
    private void OnMaskGroupUpdate(float progress)
    {
        RootMaskGroup.transform.position = Vector3.Lerp(RootMaskGroupPosition[0].position, RootMaskGroupPosition[1].position, MaskGroupCurve.Evaluate(progress));
    }
    private void OnCurrentStreakUpdate(float streak)
    {
        UpdateSelectGroup(streak);
        float progress = Mathf.InverseLerp(ToStreak, FromStreak, streak);
        StreakCurrent.transform.position = Vector3.Lerp(StreakTransform[ToStreak].position, StreakTransform[FromStreak].position, MoveCurve.Evaluate(progress));
    }
    
    private int GetBonus(int streakCount)
    {
        return StarBlazeManager.BonusArray[streakCount];
    }
    private void UpdateSelectGroup(float progress)
    {
        for (int i = 0; i < SelectGroup.Length; i++)
        {
            SelectGroup[i].alpha = SelectCurve[i].Evaluate(progress);
        }
    }
}