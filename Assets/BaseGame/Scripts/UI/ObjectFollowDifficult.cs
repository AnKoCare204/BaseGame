using TW.Reactive.CustomComponent;
using Core.GamePlay;
using Core.Manager;
using R3;
using UnityEngine;

public class ObjectFollowDifficult : MonoBehaviour
{
    [field: SerializeField] private GameObject[] Normal {get; set;}
    [field: SerializeField] private GameObject[] Hard {get; set;}
    [field: SerializeField] private GameObject[] SuperHard {get; set;}
    
    private void Start()
    {
        Reactive<LevelDifficulty> currentLevelDifficulty = LevelManager.Instance.currentLevelDifficulty;
        currentLevelDifficulty.Subscribe(ChangeLevelDifficult).AddTo(this);
    }

    private void ChangeLevelDifficult(LevelDifficulty dChange)
    {
        switch (dChange)
        {
            case LevelDifficulty.Normal:
                SetActiveObject(Normal);
                break;
            case LevelDifficulty.Hard:
                SetActiveObject(Hard);
                break;
            case LevelDifficulty.SuperHard:
                SetActiveObject(SuperHard);
                break;
        }
    }

    private void SetActiveObject(GameObject[] objects)
    {
        foreach (var obj in Normal)
        {
            obj.SetActive(false);
        }
        foreach (var obj in Hard)
        {
            obj.SetActive(false);
        }
        foreach (var obj in SuperHard)
        {
            obj.SetActive(false);
        }
        foreach (var obj in objects)
        {
            obj.SetActive(true);
        }
    }
}