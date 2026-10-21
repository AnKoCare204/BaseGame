using System;
using Core.UI.Activities;
using CoreData;
using Cysharp.Threading.Tasks;
using Game.Helper;
using Sirenix.OdinInspector;
using TMPro;
using TW.Utility.CustomComponent;
using TW.Utility.Extension;
using UnityEngine;
using UnityEngine.UI;

public class UIItemGroupInfo : ABetterMonoBehaviour
{
    [field: SerializeField] private ItemGroup ItemGroup { get; set; }
    [field: SerializeField] private Image Icon { get; set; }
    [field: SerializeField] private TextMeshProUGUI Value { get; set; }
    [field: SerializeField] private string ValueFormat { get; set; }
    [field: SerializeField] private Button ShowInfoButton { get; set; }
    [field: SerializeField] private Image CardDetail { get; set; }

    private void Awake()
    {
        ShowInfoButton.SetOnClickDestination(OnClickButton);
    }

    private void OnClickButton()
    {
        if (IsCardPack())
        {
            // CardPack cardPack = ItemGroup.ItemList[0].Type.ToCardPack();
            // CardCollectionUI.ShowModal(nameof(ModalCardInfo), (int)cardPack).Forget();
        }
        else
        {
            ActivityNotificationOverlayContext.Events.ShowResourceGroup?.Invoke(ItemGroup.ItemList, transform.position);
        }
    }

    [Button]
    public void Init(ItemGroup itemGroup)
    {
        ItemGroup = itemGroup;
        if (Icon != null)
        {
            Icon.sprite = itemGroup.Sprite;
        }

        string text = "";
        CardDetail.SetActive(false);
        if (ItemGroup.ItemList == null || ItemGroup.ItemList.Count > 1 || ItemGroup.ItemList.Count == 0)
        {
            text = "";
            ShowInfoButton.interactable = true;
        }
        else if (IsCardPack())
        {
            // CardPack cardPack = ItemGroup.ItemList[0].Type.ToCardPack();
            // text = "";
            // ShowInfoButton.interactable = true;
            // CardDetail.SetActive(true);
            // CardDetail.sprite = CardCollectionManager.Config
            //     .GetIconDesPack(cardPack);
        }
        else if (itemGroup.ItemList[0].Type == ResourceType.InfiniteLife)
        {
            text = Game.Helper.DataTimeHelper.TimeSpanToStringUI(
                TimeSpan.FromSeconds(itemGroup.ItemList[0].Amount.Value));
            ShowInfoButton.interactable = false;
        }
        else
        {
            text = string.Format(ValueFormat, itemGroup.ItemList[0].Amount.Value.ToString());
            ShowInfoButton.interactable = false;
        }

        Value.text = string.Format(text);
    }

    public bool IsCardPack()
    {
        // return ItemGroup.ItemList[0].Type is
        //     ResourceType.CardPack_0 or
        //     ResourceType.CardPack_1 or
        //     ResourceType.CardPack_2 or
        //     ResourceType.CardPack_3 or
        //     ResourceType.CardPack_4;
        return false;
    }
}