using System;
using Core.GamePlay;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AlertLevelDifficult : MonoBehaviour
{
    [field: SerializeField] public GameObject HardLevel { get; set; }
    [field: SerializeField] public GameObject SuperHardLevel { get; set; }
    [field: SerializeField] public FeelAnimation HardAnim { get; set; }
    [field: SerializeField] public FeelAnimation SuperHardAnim { get; set; }
    private LevelDifficulty currentLevelDifficulty;
    public AlertLevelDifficult OnInit(LevelDifficulty levelDifficulty)
    {
        currentLevelDifficulty = levelDifficulty;
        HardLevel.SetActive(currentLevelDifficulty == LevelDifficulty.Hard);
        SuperHardLevel.SetActive(currentLevelDifficulty == LevelDifficulty.SuperHard);
        SetActive(true);
        return this;
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    public async UniTask TryStartMoveAnimation()
    {
        try
        {
            switch (currentLevelDifficulty)
            {
                case LevelDifficulty.Normal:
                    break;
                case LevelDifficulty.Hard:
                    await HardAnim.PlayAsync();
                    await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
                    break;
                case LevelDifficulty.SuperHard:
                    await SuperHardAnim.PlayAsync();
                    await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
                    break;
            }
        }
        catch (Exception e)
        {


        }

        try
        {
            SetActive(false);
        }
        catch (Exception e)
        {


        }
    }
}