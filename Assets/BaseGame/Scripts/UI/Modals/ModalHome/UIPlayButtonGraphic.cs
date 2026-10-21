using TW.Reactive.CustomComponent;
using Core;
using Core.GamePlay;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
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
        // Lượt sắp chơi có thể nạp biến thể (độ khó khác bản gốc): peek theo đúng số lần chơi lại của lượt đó.
        LevelConfig levelConfig = LevelGlobalConfig.Instance.GetLevelConfig(level, UserData.Instance.GetNextRetry(level));
        // Hiện tạm theo luật; độ khó thật nằm trong file level (có thể vừa được cập nhật từ CDN).
        ApplyDifficulty(levelConfig.LevelType);

        foreach (TextMeshProUGUI textMeshProUGUI in TextLevel)
        {
            textMeshProUGUI.SetText("Level {0}", level);
        }

        RefreshDifficultyAsync(level, levelConfig.ToRequest()).Forget();
    }

    private async UniTask RefreshDifficultyAsync(int level, LevelRequest request)
    {
        LevelDifficulty? difficulty = await LevelAddressableProvider.PeekLevelDifficultyAsync(request);
        if (difficulty == null) return;
        if (this == null || Level == null || Level.Value != level) return; // level đã đổi trong lúc chờ
        ApplyDifficulty(difficulty.Value);
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
