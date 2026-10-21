using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Một ô trong CheatFishPicker: ảnh chụp từ prefab, tên prefab, viền khi đang nằm trong list ưu tiên.</summary>
public class CheatFishPickerItem : MonoBehaviour
{
    [field: SerializeField] private Button Button { get; set; }
    [field: SerializeField] private RawImage Icon { get; set; }
    [field: SerializeField] private TextMeshProUGUI NameText { get; set; }
    [field: SerializeField] private GameObject SelectedMark { get; set; }

    public FishModel Model { get; private set; }

    /// <summary>Đã có ảnh còn dùng được (RenderTexture có thể mất nội dung khi app vào nền).</summary>
    public bool HasIcon => Icon.texture is RenderTexture texture && texture.IsCreated();

    public void Setup(FishModel model, Action<CheatFishPickerItem> onClick)
    {
        Model = model;
        name = model.name;
        NameText.text = model.name;
        SetIcon(null);
        Button.onClick.RemoveAllListeners();
        Button.onClick.AddListener(() => onClick?.Invoke(this));
    }

    public void SetIcon(Texture texture)
    {
        Icon.texture = texture;
        Icon.enabled = texture != null;
    }

    public void SetSelected(bool selected) => SelectedMark.SetActive(selected);
}
