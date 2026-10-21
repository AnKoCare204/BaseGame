using System;
using Core.UI.Activities;
using CoreData;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIItemInfo : MonoBehaviour
{
    [field: SerializeField] public bool InitIcon { get; set; } = true;
    [field: SerializeField] public bool PlayAnim { get; set; }
    [field: SerializeField] public Image ImgItemIcon { get; set; }
    [field: SerializeField] public Image ImgDesPack { get; set; }
    [field: SerializeField] public TextMeshProUGUI TxtItemAmount1 { get; set; }
    [field: SerializeField] public TextMeshProUGUI TxtItemAmount2 { get; set; }
    [field: SerializeField] public Button ButtonShowInfo { get; set; }

    [field: Header("Reward dạng prefab")]
    [field: Tooltip("Prefab NameCellReward — name effect không xem được qua icon tĩnh, phải gắn vào tên thật.")]
    [field: SerializeField] public NameCellUI NameRewardPrefab { get; set; }

    [field: Tooltip("Nơi gắn prefab. Bỏ trống thì gắn thẳng vào item này.")]
    [field: SerializeField] public Transform CustomViewParent { get; set; }

    private NameCellUI nameRewardInstance;

    public void Init(GameResource gameResource)
    {
        ButtonShowInfo?.gameObject.SetActive(false);

        // Init có thể gọi lại với loại reward khác — trả icon/prefab về mặc định.
        // if (ImgItemIcon != null) ImgItemIcon.gameObject.SetActive(true);
        if (nameRewardInstance != null) nameRewardInstance.gameObject.SetActive(false);
        if (PlayAnim)
        {
            AnimateItem();
        }

        string valueStr = gameResource.Value.Value.ToString();
        switch (gameResource.Type)
        {
            case ResourceType.None:
                break;
            case ResourceType.Money:
                // if((CurrencyType)gameResource.SpecificType == CurrencyType.Money && ImgItemIcon != null && InitIcon)
                //     ImgItemIcon.sprite = ItemGlobalConfig.Instance.GoldRewardIcon;

                valueStr = FormatNumberWithSpaces(gameResource.Value.Value);
                break;
            case ResourceType.InfiniteLife:
                valueStr = FormatTime(gameResource.Value.Value);
                break;
            case ResourceType.NoAds:
                valueStr = "";
                break;
            case ResourceType.Hint:
            case ResourceType.Undo:
            case ResourceType.Shuffle:
                valueStr = $"x{gameResource.Value.Value}";
                break;
            case ResourceType.Badge:
                valueStr = "";
                if (ImgItemIcon != null && InitIcon)
                    ImgItemIcon.sprite = GetProfileRewardSprite(gameResource.Type, gameResource.Value.Value);
                break;
            case ResourceType.Frame:
                valueStr = "";
                if (ImgItemIcon != null && InitIcon)
                    ImgItemIcon.sprite = GetProfileRewardSprite(gameResource.Type, gameResource.Value.Value);
                break;
            case ResourceType.Avatar:
                // Profile cosmetics: Value = index trong ProfileGlobalConfig reward lists.
                valueStr = "";
                if (ImgItemIcon != null && InitIcon)
                    ImgItemIcon.sprite = GetProfileRewardSprite(gameResource.Type, gameResource.Value.Value);
                break;
            case ResourceType.NameEffect:
                // Name effect chỉ có nghĩa khi gắn vào tên thật, nên vẽ nguyên cell
                // tên của người chơi thay vì một icon tĩnh.
                valueStr = "";
                ShowNameReward(gameResource.Value.Value);
                break;
        }

        if (ImgItemIcon != null && InitIcon
            && gameResource.Type is not (ResourceType.Badge or ResourceType.Frame or ResourceType.Avatar or ResourceType.NameEffect))
            ImgItemIcon.sprite =
                ItemGlobalConfig.Instance.GetItemSprite(gameResource.Type);
        if (TxtItemAmount1 != null)
        {
            TxtItemAmount1.text = valueStr;
        }

        if (TxtItemAmount2 != null)
        {
            TxtItemAmount2.text = valueStr;
        }
    }

    /// <summary>
    /// Dựng <see cref="NameRewardPrefab"/> với TÊN HIỆN TẠI của người chơi và effect
    /// đang xem, để họ thấy trước tên mình sẽ trông thế nào.
    ///
    /// Cố tình để <c>isCurrentUser = false</c>: bật lên thì cell bám theo
    /// <c>PlayerData.EffectNameID</c> và nhảy sang effect ĐANG đeo, thay vì effect
    /// đang được xem trong popup.
    /// </summary>
    private void ShowNameReward(int nameEffectIndex)
    {
        if (NameRewardPrefab == null)
        {
            // Prefab chưa được kéo vào (UIItemInfo nằm trong hơn chục prefab khác
            // nhau): quay về icon tĩnh như trước, đừng để icon của reward cũ dính lại.
            if (ImgItemIcon != null && InitIcon)
                ImgItemIcon.sprite = GetProfileRewardSprite(ResourceType.NameEffect, nameEffectIndex);

            return;
        }

        if (ImgItemIcon != null) ImgItemIcon.gameObject.SetActive(false);

        if (nameRewardInstance == null)
        {
            Transform parent = CustomViewParent != null ? CustomViewParent : transform;
            // worldPositionStays = false: giữ nguyên transform local của prefab thay
            // vì bắt Unity tính lại để bảo toàn vị trí world.
            nameRewardInstance = Instantiate(NameRewardPrefab, parent, false);
        }

        ResetRewardTransform(nameRewardInstance.transform);
        nameRewardInstance.gameObject.SetActive(true);
        nameRewardInstance.SetIsCurrentUser(false);
        nameRewardInstance.SetUserNameEffect(nameEffectIndex, true, PlayerData.Instance.UserName.Value);
    }


    /// <summary>
    /// Đưa prefab về đúng giữa ô. Phải dùng <c>anchoredPosition</c> chứ không phải
    /// <c>localPosition</c>: localPosition = 0 chỉ trùng tâm khi pivot của parent
    /// nằm đúng chỗ anchor — parent pivot lệch (0.5, 0) hay (0.5, 1) là ra ngay
    /// offset bằng nửa chiều cao parent.
    /// </summary>
    private static void ResetRewardTransform(Transform target)
    {
        if (target is RectTransform rect)
        {
            rect.anchoredPosition3D = Vector3.zero;
        }
        else
        {
            target.localPosition = Vector3.zero;
        }

        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
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

    public void SetOnClickShowInfo(Action onClickShowInfo)
    {
        ButtonShowInfo?.onClick.RemoveAllListeners();
        ButtonShowInfo?.SetOnClickDestination(onClickShowInfo);
    }

    static Sprite GetProfileRewardSprite(ResourceType type, int index)
    {
        return type switch
        {
            ResourceType.Badge => ProfileGlobalConfig.Instance.GetBadgeRewardSprite(index),
            ResourceType.Frame => ProfileGlobalConfig.Instance.GetFrameRewardSprite(index),
            ResourceType.Avatar => ProfileGlobalConfig.Instance.GetAvatarRewardSprite(index),
            ResourceType.NameEffect => ProfileGlobalConfig.Instance.GetNameRewardSprite(index),
            _ => null,
        };
    }
}