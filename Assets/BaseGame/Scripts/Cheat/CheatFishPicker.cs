using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup của panel cheat: lưới mọi FishModel trong RegularFishModelConfigs + SpecialFishModelConfigs, chạm để thêm / bỏ khỏi
/// list ưu tiên (CheatFishPriority, lưu ngay). List có hiệu lực từ lần load level kế tiếp (Level.StartGame).
/// Ảnh chụp từ prefab lúc mở lần đầu (CheatFishIconRenderer), mỗi frame một con để không giật.
/// </summary>
public class CheatFishPicker : MonoBehaviour
{
    [field: SerializeField] private CheatFishPickerItem ItemTemplate { get; set; }
    [field: SerializeField] private RectTransform Content { get; set; }
    [field: SerializeField] private TextMeshProUGUI CountText { get; set; }
    [field: SerializeField] private Button ClearButton { get; set; }
    [field: SerializeField] private Button CloseButton { get; set; }
    [field: SerializeField, Tooltip("Nền ảnh chụp, trùng màu ô để ảnh liền với ô")]
    private Color IconBackground { get; set; } = new Color(0.17f, 0.33f, 0.47f, 1f);

    /// <summary>List ưu tiên vừa đổi (chạm một ô hoặc bỏ chọn hết).</summary>
    public event Action OnChanged;

    private readonly List<CheatFishPickerItem> items = new List<CheatFishPickerItem>();
    private Coroutine iconRoutine;

    private void Awake()
    {
        ItemTemplate.gameObject.SetActive(false);
        ClearButton.onClick.AddListener(OnClearClicked);
        CloseButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        iconRoutine = null;   // coroutine dừng theo object; mở lại thì chụp tiếp các ô còn thiếu
    }

    public void Open()
    {
        gameObject.SetActive(true);
        BuildItems();
        Refresh();
        if (iconRoutine == null) iconRoutine = StartCoroutine(FillIcons());
    }

    public void Close() => gameObject.SetActive(false);

    private void BuildItems()
    {
        if (items.Count > 0) return;
        foreach (FishModel model in FishModelGlobalConfig.Instance.GetPickableFishModels())
        {
            CheatFishPickerItem item = Instantiate(ItemTemplate, Content);
            item.gameObject.SetActive(true);
            item.Setup(model, OnItemClicked);
            items.Add(item);
        }
    }

    private IEnumerator FillIcons()
    {
        foreach (CheatFishPickerItem item in items)
        {
            if (item.HasIcon) continue;
            item.SetIcon(CheatFishIconRenderer.GetIcon(item.Model, IconBackground));
            yield return null;
        }

        iconRoutine = null;
    }

    private void OnItemClicked(CheatFishPickerItem item)
    {
        CheatFishPriority.Toggle(item.Model.name);
        Refresh();
        OnChanged?.Invoke();
    }

    private void OnClearClicked()
    {
        CheatFishPriority.Clear();
        Refresh();
        OnChanged?.Invoke();
    }

    private void Refresh()
    {
        List<string> names = CheatFishPriority.Names;
        int selected = 0;
        foreach (CheatFishPickerItem item in items)
        {
            bool isSelected = names.Contains(item.Model.name);
            item.SetSelected(isSelected);
            if (isSelected) selected++;
        }

        CountText.text = $"Selected: {selected}  (applies on next level load)";
    }
}
