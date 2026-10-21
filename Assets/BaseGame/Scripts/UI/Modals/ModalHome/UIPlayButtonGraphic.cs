using TW.Reactive.CustomComponent;
using Core;
using Core.GamePlay;
using CoreGame.GameGlobalConfig;
using R3;
using TMPro;
using UnityEngine;

public class UIPlayButtonGraphic : MonoBehaviour
{
    [field: SerializeField] private Reactive<int> Level {get; set;}
    [field: SerializeField] private TextMeshProUGUI[] TextLevel {get; set;}
    [field: SerializeField] private GameObject[] NormalLevel {get; set;}
    [field: SerializeField] private GameObject[] HardLevel {get; set;}
    [field: SerializeField] private GameObject[] SuperHardLevel {get; set;}
    private void Start()
    {
        Level = GameManager.Instance.Level;
        Level.Subscribe(OnLevelChange).AddTo(this);
    }

    private void OnLevelChange(int level)
    {
        ApplyDifficulty(LevelGlobalConfig.Instance.GetDifficulty(level));

        foreach (TextMeshProUGUI textMeshProUGUI in TextLevel)
        {
            textMeshProUGUI.SetText("Level {0}", level);
        }
    }

    private void ApplyDifficulty(LevelDifficulty difficulty)
    {
        for (var i = 0; i < NormalLevel.Length; i++)
        {
            NormalLevel[i].SetActive(difficulty == LevelDifficulty.Normal);
        }

        for (var i = 0; i < HardLevel.Length; i++)
        {
            HardLevel[i].SetActive(difficulty == LevelDifficulty.Hard);
        }

        for (var i = 0; i < SuperHardLevel.Length; i++)
        {
            SuperHardLevel[i].SetActive(difficulty == LevelDifficulty.SuperHard);
        }
    }
}
