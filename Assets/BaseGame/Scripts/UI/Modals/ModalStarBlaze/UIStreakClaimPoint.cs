using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using TMPro;
using TW.Utility.CustomComponent;
using UnityEngine;

public class UIStreakClaimPoint : ABetterMonoBehaviour
{
    public static Func<int, UniTask> PlayClaimAnimAsync { get; set; }
    [field: SerializeField] public TextMeshProUGUI ClaimValue { get; set; }
    [field: SerializeField] public TextMeshProUGUI BonusValue { get; set; }
    [field: SerializeField] public MMF_Player ClaimAnim { get; set; }
    [field: SerializeField] public List<FeelAnimationWithSkip> ClaimAnimList { get; private set; }
    [field: SerializeField] public GameObject ClaimAnimGroup { get; private set; }
    [field: SerializeField] public float ActiveDuration { get; private set; }
    [field: SerializeField] private float SkipDelay { get; set; } = 0.1f;
    private float LastSkipTime { get; set; } = float.MinValue;

    private void Awake()
    {
        PlayClaimAnimAsync = PlayClaimAnim;
    }

    private void OnDestroy()
    {
        PlayClaimAnimAsync = null;
    }

    [Button]
    public async UniTask PlayClaimAnim(int pointClaim)
    {
        ClaimValue.SetText($"+{pointClaim}");
        BonusValue.SetText($"x{pointClaim}");
        ClaimAnimGroup.SetActive(true);
        SelfInactive().Forget();
        ClaimAnim.PlayFeedbacks();
        MainMenuSkipButton.MainButton.onClick.AddListener(SkipFeedback);
        await IsFeedbackComplete();
        MainMenuSkipButton.MainButton.onClick.RemoveListener(SkipFeedback);
        Time.timeScale = 1;
    }
    public void SkipFeedback()
    {
        Time.timeScale = 1.2f;
    }

    private async UniTask IsFeedbackComplete()
    {
        await UniTask.WaitUntil(() => !ClaimAnim.IsPlaying, cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    [Button]
    public void SkipAnimation()
    {
        if (Time.unscaledTime - LastSkipTime < SkipDelay) return;
        for (int i = 0; i < ClaimAnimList.Count; i++)
        {
            if (ClaimAnimList[i].IsPlaying)
            {
                if (ClaimAnimList[i].Skip())
                {
                    LastSkipTime = Time.unscaledTime;
                    Debug.Log($"Skipped animation at index {i}");
                }
                return;
            }
        }
    }

    public async UniTask SelfInactive()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(ActiveDuration),
            cancellationToken: this.GetCancellationTokenOnDestroy());
        ClaimAnimGroup.SetActive(false);
    }

    public void PlaySoundAppear()
    {
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.ClaimAppear);
    }

    public void PlaySoundAdd()
    {
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StarAdd);
    }

    public void PlaySoundClaim()
    {
        AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StarClaim);
    }
}