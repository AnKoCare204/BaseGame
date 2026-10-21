using System;
using Core;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using R3;
using TW.Reactive.CustomComponent;
using Unity.VisualScripting;
using UnityEngine;

public class UIReactiveDifficulty : MonoBehaviour
{
    [field: SerializeField] private bool UpdateFistTimeOnly {get; set;}
    [field: SerializeField] private GameObject[] Normal { get; set; }
    [field: SerializeField] private GameObject[] Hard { get; set; }
    [field: SerializeField] private GameObject[] SuperHard { get; set; }
    private Reactive<int> Level { get; set; }

    private void Start()
    {
        Level = GameManager.Instance.Level;
        OnLevelChange(Level);
        Level.Subscribe(OnLevelChange).AddTo(this);
    }

    private void OnLevelChange(int level)
    {
        LevelConfig levelConfig = LevelGlobalConfig.Instance.GetLevelConfig(level);
        ApplyDifficulty(levelConfig.LevelType);
    }

    private void ApplyDifficulty(LevelDifficulty difficulty)
    {
        for (var i = 0; i < Normal.Length; i++)
        {
            Normal[i].SetActive(difficulty == LevelDifficulty.Normal);
        }

        for (var i = 0; i < Hard.Length; i++)
        {
            Hard[i].SetActive(difficulty == LevelDifficulty.Hard);
        }

        for (var i = 0; i < SuperHard.Length; i++)
        {
            SuperHard[i].SetActive(difficulty == LevelDifficulty.SuperHard);
        }
    }
}
