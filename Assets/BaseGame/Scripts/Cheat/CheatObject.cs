using System.Collections.Generic;
using Core.Manager;
using CoreData;
using CoreGame.GameGlobalConfig;
using Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CheatObject : MonoBehaviour
{
    [field: SerializeField] private Button LoadLevelButton {get; set;}
    [field: SerializeField] private Button HideUIButton {get; set;}
    [field: SerializeField] private Button WinButton {get; set;}
    [field: SerializeField] private Button LoseButton {get; set;}
    [field: SerializeField] private TMP_InputField LevelInput {get; set;}
    [field: SerializeField] private TMP_Dropdown TestSetDropdown {get; set;}
    [field: SerializeField] private Button RefreshLevelsButton {get; set;}
    [field: SerializeField] private TextMeshProUGUI LevelSourceText {get; set;}
    [field: SerializeField] private Button FishPriorityButton {get; set;}
    [field: SerializeField] private TextMeshProUGUI FishPriorityText {get; set;}
    [field: SerializeField] private CheatFishPicker FishPicker {get; set;}
    [field: SerializeField] private TMP_InputField StarBlazePointInput {get; set;}
    [field: SerializeField] private Button StarBlazeAddPointButton {get; set;}
    [field: SerializeField] private TextMeshProUGUI StarBlazeText {get; set;}
    [field: SerializeField] private TMP_InputField RetryInput {get; set;}
    [field: SerializeField] private Button RetryButton {get; set;}
    [field: SerializeField] private Button ChangeBgButton {get; set;}
    /// <summary>Giá trị ghi đè ứng với từng option của TestSetDropdown, cùng thứ tự.</summary>
    private List<string> testSetChoices = new();
    private void Start()
    {
        if (GameManager.Instance.BuildType == BuildType.Release)
        {
            gameObject.SetActive(false);
            return;
        }
        ChangeBgButton.SetOnClickDestination(OnChangeBg);
        LoadLevelButton.SetOnClickDestination(OnLoadLevelButtonClicked);
        HideUIButton.onClick.AddListener(OnHideUIButtonClicked);
        WinButton.SetOnClickDestination(OnWinButtonClicked);
        LoseButton.SetOnClickDestination(OnLoseButtonClicked);
        
        LevelInput.text = GameManager.Instance.Level.Value.ToString();

        if (RefreshLevelsButton != null) RefreshLevelsButton.SetOnClickDestination(OnRefreshLevelsClicked);
        if (TestSetDropdown != null)
        {
            RebuildTestSetOptions();
            TestSetDropdown.onValueChanged.AddListener(OnTestSetChanged);
        }

        RefreshLevelSourceText();

        if (FishPicker != null)
        {
            FishPicker.OnChanged += RefreshFishPriorityText;
            FishPicker.Close();
        }
        if (FishPriorityButton != null) FishPriorityButton.onClick.AddListener(OnFishPriorityButtonClicked);
        RefreshFishPriorityText();

        if (StarBlazeAddPointButton != null) StarBlazeAddPointButton.onClick.AddListener(OnStarBlazeAddPointClicked);
        if (RetryButton != null) RetryButton.SetOnClickDestination(OnRetryButtonClicked);
        RefreshStarBlazeText();
    }

    private void OnEnable()
    {
        RefreshStarBlazeText();
    }

    private async UniTask OnLoadLevelButtonClicked()
    {
        if (int.TryParse(LevelInput.text, out int levelNumber))
        {
            GameManager.Instance.Level.Value = levelNumber;
            await GameManager.Instance.TryLoadNextLevel();
        }

    }

    /// <summary>
    /// Vào lại level hiện tại với số lần chơi lại = giá trị nhập (0 = lượt đầu), để thử biến thể LevelData_{n}_{x} mà không phải thua
    /// thật x lần. LoadCurrentLevel cộng 1 vào bộ đếm, nên lưu r - 1.
    /// </summary>
    private async UniTask OnRetryButtonClicked()
    {
        if (RetryInput == null || !int.TryParse(RetryInput.text, out int retry) || retry < 0) return;
        int level = GameManager.Instance.Level.Value;
        UserData.Instance.SetLevelRetry(retry - 1, level);
        await GameManager.Instance.TryLoadNextLevel();
    }
    /// <summary>Dựng lại option theo các bộ test đang có. Lựa chọn đã lưu mà bộ đó không còn thì về "Remote config".</summary>
    private void RebuildTestSetOptions()
    {
        if (TestSetDropdown == null) return;

        List<string> available = LevelAddressableProvider.GetAvailableTestVersions();
        // Catalog chưa nạp được thì danh sách chưa đáng tin: giữ nguyên bộ đã chọn, không coi là đã bị xoá.
        bool authoritative = LevelAddressableProvider.AreTestVersionsAuthoritative;

        string saved = LevelTestSelector.CheatOverride;
        string normalized = LevelTestSelector.NormalizeOverride(saved, available, authoritative);
        if (normalized != saved) LevelTestSelector.CheatOverride = normalized;
        testSetChoices = LevelTestSelector.BuildCheatChoices(available, normalized);

        List<string> labels = new List<string>(testSetChoices.Count);
        foreach (string choice in testSetChoices) labels.Add(LevelTestSelector.DescribeChoice(choice));

        TestSetDropdown.ClearOptions();
        TestSetDropdown.AddOptions(labels);
        TestSetDropdown.SetValueWithoutNotify(Mathf.Max(0, testSetChoices.IndexOf(normalized)));
        TestSetDropdown.RefreshShownValue();
    }

    private void OnTestSetChanged(int index)
    {
        if (index < 0 || index >= testSetChoices.Count) return;
        LevelTestSelector.CheatOverride = testSetChoices[index];
        // Bộ vừa chọn có thể chưa tải: tải nền ngay, gate sẽ tải chặn nếu người dùng vào level trước khi xong.
        LevelAddressableProvider.RequestSyncAllChunks();
        RefreshLevelSourceText();
    }

    /// <summary>Kiểm tra catalog trên CDN ngay, bỏ qua giãn cách 5 phút, rồi cập nhật danh sách bộ test.</summary>
    private async UniTask OnRefreshLevelsClicked()
    {
        if (LevelSourceText != null) LevelSourceText.text = "refreshing...";
        await LevelCatalog.CheckForUpdateAsync(true);
        RebuildTestSetOptions();
        RefreshLevelSourceText();
    }

    private void RefreshLevelSourceText()
    {
        if (LevelSourceText == null) return;
        LevelConfig config = LevelManager.Instance != null ? LevelManager.Instance.LevelConfig : null;
        string level = config != null && config.Level > 0 ? $"L{config.Level} · {config.Source} · retry {config.RetryCount}" : "no level";
        string test = LevelTestSelector.DescribeChoice(LevelTestSelector.CheatOverride);
        string catalog = LevelCatalog.IsLoaded ? "catalog ok" : "catalog off";
        LevelSourceText.text = $"{level}\ntest: {test} · {catalog}";
    }

    /// <summary>Mở / đóng popup chọn cá ưu tiên cho các level load sau.</summary>
    private void OnFishPriorityButtonClicked()
    {
        if (FishPicker == null) return;
        if (FishPicker.gameObject.activeSelf) FishPicker.Close();
        else FishPicker.Open();
    }

    private void RefreshFishPriorityText()
    {
        if (FishPriorityText == null) return;
        int count = CheatFishPriority.ResolveModels(FishModelGlobalConfig.Instance.GetPickableFishModels()).Count;
        FishPriorityText.text = count > 0 ? $"Fish ({count})" : "Fish";
    }

    /// <summary>Cộng điểm StarBlaze chờ claim (giữ streak); về Home sẽ chạy animation claim và nhận thưởng.</summary>
    private void OnStarBlazeAddPointClicked()
    {
#if CHEAT_ONLY || UNITY_EDITOR
        if (StarBlazePointInput != null && int.TryParse(StarBlazePointInput.text, out int point))
        {
            StarBlazeManager.Instance.CheatAddPoint(point);
        }
#endif
        RefreshStarBlazeText();
    }

    private void RefreshStarBlazeText()
    {
        if (StarBlazeText == null) return;
#if CHEAT_ONLY || UNITY_EDITOR
        StarBlazeManager manager = StarBlazeManager.Instance;
        StarBlazeText.text = manager != null && manager.IsCheatEventRunning
            ? $"Star {manager.CurrentPoint} +{manager.WillAddPoint}"
            : "Star off";
#else
        StarBlazeText.text = "Star off";
#endif
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
    private void OnChangeBg()
    {
        GamePlayBackground.OnChangeBg?.Invoke();
    }
}