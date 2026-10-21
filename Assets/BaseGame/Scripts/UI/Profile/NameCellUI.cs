using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using CoreNotification;
using Profile.ProfileItem;

public class NameCellUI : MonoBehaviour
{
    [SerializeField] public UserNameEffect userNameEffect;
    [SerializeField] public int nameID;
    public int NameID => nameID;
    [SerializeField] public Button btnName;
    [SerializeField] private GameObject selectObj;
    [SerializeField] private GameObject lockObj;
    [SerializeField] private GameObject notificationObj;
    [field: SerializeReference] private IProfileNotificationCondition Condition { get; set; }

    private bool clickListenerBound;

    private void Start()
    {
        if (btnName != null)
        {
            btnName.onClick.AddListener(OnClickName);
        }
    }

    private void OnDisable()
    {
        ShowNotification(false);
    }

    /// <summary>
    /// Đăng ký handler chọn ô. Popup có thể Initialize lại nhiều lần nên chốt cờ
    /// để không cộng dồn listener — một cú bấm chạy handler nhiều lần.
    /// </summary>
    public void AddListenerOnClickName(Action onClick)
    {
        if (clickListenerBound)
        {
            return;
        }

        clickListenerBound = true;
        btnName.onClick.AddListener(() => onClick?.Invoke());
    }

    private void OnClickName()
    {
        ShowNotification(false);
        if (Condition is NameNotificationCondition)
        {
            ProfileData.Instance.OnChangeStatusNotification(new NameNotificationCondition(), -1);
        }
    }

    // selectObj / lockObj / notificationObj chỉ có ở ô trong popup Edit Profile.
    // Cell dùng lại ở chỗ khác (ví dụ NameCellReward) để trống hết, nên phải
    // chống null — OnDisable gọi ShowNotification là nổ ngay.

    public void ShowNotification(bool show)
    {
        if (notificationObj != null)
        {
            notificationObj.SetActive(show);
        }
    }

    public void ShowSelect(bool show)
    {
        if (selectObj != null)
        {
            selectObj.SetActive(show);
        }
    }

    public void SetUserNameEffect(int id, bool isUnlocked, string name)
    {
        nameID = id;
        userNameEffect.InitUserNameEffect(name, id);

        if (lockObj != null)
        {
            lockObj.SetActive(!isUnlocked);
        }
    }    

    public void SetIsCurrentUser(bool isCurrentUser, bool isOnlyChangeName = false)
    {
        userNameEffect.SetIsCurrentUser(isCurrentUser, isOnlyChangeName);
    }

    public bool IsUnlocked => lockObj != null && lockObj.activeSelf;
}
