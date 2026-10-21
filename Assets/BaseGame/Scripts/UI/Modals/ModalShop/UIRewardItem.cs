using System;
using CoreData;
using Cysharp.Threading.Tasks;
using Game.Helper;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRewardItem : MonoBehaviour
{
    [field: SerializeField] public bool InitIcon { get; set; } = true;
    [field: SerializeField] public bool PlayAnim { get; set; }
    [field: SerializeField] public Image ImgItemIcon { get; set; }
    [field: SerializeField] public TextMeshProUGUI TxtItemAmount1 { get; set; }
    [field: SerializeField] public Image ImgDesPack { get; set; }
    public void Init(GameResource gameResource)
    {
        if (PlayAnim)
        {
            AnimateItem();
        }
        string valueStr = gameResource.Value.Value.ToString();
        ImgDesPack.gameObject.SetActive(false);
        switch (gameResource.Type)
        {
            case ResourceType.None:
                break;
            case ResourceType.Money:
                valueStr = FormatNumberWithSpaces(gameResource.Value.Value);
                break;
            case ResourceType.InfiniteLife:
                valueStr = TimeSpan.FromSeconds(gameResource.Value.Value).TimeSpanToStringUI();
                break;
            case ResourceType.NoAds:
                valueStr = "";
                break;
            case ResourceType.Hint:
            case ResourceType.Undo:
            case ResourceType.Shuffle:
                valueStr = $"x{gameResource.Value.Value}";
                break;
        }

        if (ImgItemIcon != null && InitIcon)
            ImgItemIcon.sprite =
                ItemGlobalConfig.Instance.GetItemSprite(gameResource.Type);
        if (TxtItemAmount1 != null)
        {
            TxtItemAmount1.text = valueStr;
        }
    }

    public void AnimateItem()
    {
        LMotion.Create(ImgItemIcon.transform.position, ImgItemIcon.transform.position, 0.5f)
            .Bind(x => ImgItemIcon.transform.position = x).AddTo(this);
    }

    public async UniTask ShowItemAnim(float delay = 0f)
    {
        transform.localScale = Vector3.zero;
        await UniTask.Delay((int)(delay * 1000), cancellationToken: this.GetCancellationTokenOnDestroy());
        LMotion.Create(0f, 1f, 0.15f).WithEase(Ease.OutBack)
            .Bind(x => transform.localScale = Vector3.one * x)
            .AddTo(this);
    }

    public string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}s";
        }
        else if (totalSeconds < 3600)
        {
            int minutes = totalSeconds / 60;
            return $"{minutes}m";
        }
        else
        {
            int hours = totalSeconds / 3600;
            return $"{hours}h";
        }
    }

    public string FormatNumberWithSpaces(int number)
    {
        return number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(",", ".");
    }
}