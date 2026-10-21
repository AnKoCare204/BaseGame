using System;
using System.Collections;
using System.Collections.Generic;
using CoreNotification;
using UnityEngine;
using UnityEngine.UI;


namespace UI.ModalProfile
{
    public class ProfileCellUI : MonoBehaviour
    {
        [SerializeField] public int profileID;
        public int ProfileID => profileID;
        [SerializeField] private Image imgProfile;
        [SerializeField] private RectTransform rectTransformProfile;
        [SerializeField] private Color colorProfileLocked;
        [SerializeField] public Button btnProfile;
        [SerializeField] private GameObject selectObj;
        [SerializeField] private GameObject lockObj;
        [SerializeField] private GameObject notificationObj;
        [field: SerializeReference] private IProfileNotificationCondition Condition { get; set; }
        
        private void OnDisable()
        {
            ShowNotification(false);
        }

        private void OnClickProfile()
        {
            ShowNotification(false);
            if (Condition is AvatarNotificationCondition)
            {
                ProfileData.Instance.OnChangeStatusNotification(new AvatarNotificationCondition(), -1);
            }
            else if (Condition is FrameNotificationCondition)
            {
                ProfileData.Instance.OnChangeStatusNotification(new FrameNotificationCondition(), -1);
            }
            else if (Condition is BadgeNotificationCondition)
            {
                ProfileData.Instance.OnChangeStatusNotification(new BadgeNotificationCondition(), -1);
            }
        }

        public void AddListenerOnClickProfile(Action onClick)
        {
            btnProfile.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                OnClickProfile();
            });
        }

        public void ShowNotification(bool show)
        {
            notificationObj.SetActive(show);
        }

        public void ShowSelect(bool show)
        {
            if (lockObj.activeSelf)
            {
                selectObj.SetActive(false);
                return;
            }
            selectObj.SetActive(show);
        }

        public void SetProfile(int id, Sprite sprProfile, bool isUnlocked, float sizeRectTransformProfile = 20f)
        {
            profileID = id;
            imgProfile.sprite = sprProfile;
            imgProfile.color = isUnlocked ? Color.white : colorProfileLocked;
            lockObj.SetActive(!isUnlocked);

            if (rectTransformProfile != null)
            {
                rectTransformProfile.offsetMin = new Vector2(sizeRectTransformProfile, sizeRectTransformProfile);
                rectTransformProfile.offsetMax = new Vector2(-sizeRectTransformProfile, -sizeRectTransformProfile);
            }
        }    
        
        public bool IsUnlocked => lockObj.activeSelf;
    }

}
