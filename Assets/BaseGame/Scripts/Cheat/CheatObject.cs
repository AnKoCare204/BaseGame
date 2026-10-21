using Core.Manager;
using CoreData;
using CoreGame.GameGlobalConfig;
using Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bảng cheat trong ScreenInGame / ModalPause. Tự tắt ở bản Release (GameManager.BuildType).</summary>
public class CheatObject : MonoBehaviour
{
    [field: SerializeField] private Button LoadLevelButton {get; set;}
    [field: SerializeField] private Button HideUIButton {get; set;}
    [field: SerializeField] private Button WinButton {get; set;}
    [field: SerializeField] private Button LoseButton {get; set;}
    [field: SerializeField] private TMP_InputField LevelInput {get; set;}
    [field: SerializeField] private TextMeshProUGUI LevelSourceText {get; set;}
    [field: SerializeField] private TMP_InputField RetryInput {get; set;}
    [field: SerializeField] private Button RetryButton {get; set;}

    private void Start()
    {
        if (GameManager.Instance.BuildType == BuildType.Release)
        {
            gameObject.SetActive(false);
            return;
        }
        LoadLevelButton.SetOnClickDestination(OnLoadLevelButtonClicked);
        HideUIButton.onClick.AddListener(OnHideUIButtonClicked);
        WinButton.SetOnClickDestination(OnWinButtonClicked);
        LoseButton.SetOnClickDestination(OnLoseButtonClicked);
        if (RetryButton != null) RetryButton.SetOnClickDestination(OnRetryButtonClicked);

        LevelInput.text = GameManager.Instance.Level.Value.ToString();
        RefreshLevelSourceText();
    }

    private async UniTask OnLoadLevelButtonClicked()
    {
        if (int.TryParse(LevelInput.text, out int levelNumber))
        {
            GameManager.Instance.Level.Value = levelNumber;
            await GameManager.Instance.TryLoadNextLevel();
        }
    }

    /// <summary>Vào lại level hiện tại với số lần chơi lại = giá trị nhập (0 = lượt đầu). LoadCurrentLevel cộng 1 vào bộ đếm, nên lưu r - 1.</summary>
    private async UniTask OnRetryButtonClicked()
    {
        if (RetryInput == null || !int.TryParse(RetryInput.text, out int retry) || retry < 0) return;
        int level = GameManager.Instance.Level.Value;
        UserData.Instance.SetLevelRetry(retry - 1, level);
        await GameManager.Instance.TryLoadNextLevel();
    }

    private void RefreshLevelSourceText()
    {
        if (LevelSourceText == null) return;
        LevelConfig config = LevelManager.Instance != null ? LevelManager.Instance.LevelConfig : null;
        LevelSourceText.text = config != null && config.Level > 0
            ? $"L{config.Level} · {config.LevelType} · retry {config.RetryCount}"
            : "no level";
    }

    private void OnHideUIButtonClicked()
    {
        Launcher launcher = FindAnyObjectByType<Launcher>();
        float originalAlpha = launcher.GetComponent<CanvasGroup>().alpha;
        launcher.GetComponent<CanvasGroup>().alpha = originalAlpha > 0.5 ? 0 : 1;
    }

    private void OnWinButtonClicked()
    {
        GameManager.Instance.WinLevel().Forget();
    }

    private void OnLoseButtonClicked()
    {
        GameManager.Instance.LoseLevel().Forget();
    }
}
