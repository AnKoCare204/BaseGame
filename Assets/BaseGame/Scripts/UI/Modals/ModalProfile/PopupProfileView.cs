using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;
using Profile.ProfileItem;
using CoreData;

namespace UI.ModalProfile
{
    public class PopupProfileView : MonoBehaviour
    {
        [FoldoutGroup("User Info")][field: SerializeField] public TextMeshProUGUI txtUserName { get; private set; }
        [FoldoutGroup("User Info")][field: SerializeField] public TextMeshProUGUI lblInstallDate { get; private set; }
        [FoldoutGroup("User Info")][field: SerializeField] public TextMeshProUGUI lblLevel { get; private set; }
        [FoldoutGroup("User Info")][field: SerializeField] public TextMeshProUGUI lblUID { get; private set; }

        [FoldoutGroup("User Info")][field: SerializeField] public UserAvatarFrame userAvatarFrame;

        [FoldoutGroup("User Info")]
        [Tooltip("Kéo UserNameEffect chứa txtUserName vào đây, nếu không tên sẽ hiện trơn không effect.")]
        [field: SerializeField] public UserNameEffect userNameEffect;
        [FoldoutGroup("Team Info")][field: SerializeField] public UserTeamCell userTeamCell; 

        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtCurrentStreak;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtFirstTryWin;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtCollectionsDone;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtSweetMealChampion;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtCompleteTreetopMission;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtCompleteNestQuest;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtTotalLeaguePoint;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtHighestLeaguePoint;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtTotalCoinEarned;
        [FoldoutGroup("General Info")][field: SerializeField] public TextMeshProUGUI txtBoosterUsed;


        private void OnEnable()
        {
            SetupUserInfo();
            SetupUserAvatarFrame();
            SetupGeneralInfo();
        }

        private void SetupUserInfo()
        {
            SetupUserName();
            lblLevel.text = $"{PlayerData.Instance.CurrentLevel.Value}";
            SetupInstallDate();
        }

        /// <summary>
        /// txtUserName là TMP NẰM TRONG prefab UserNameEffect, nên gán thẳng
        /// <c>.text</c> chỉ đổi chữ và bỏ qua toàn bộ phần effect — tên luôn hiện
        /// trơn dù người chơi đã đeo effect khác. Phải đi qua InitUserNameEffect,
        /// đúng kiểu <see cref="SetupUserAvatarFrame"/> làm với avatar/frame/badge.
        /// </summary>
        private void SetupUserName()
        {
            string userName = PlayerData.Instance.UserName.Value;

            if (userNameEffect == null)
            {
                // Chưa kéo reference: ít nhất vẫn hiện đúng tên, chỉ mất effect.
                if (txtUserName != null) txtUserName.text = userName;
                return;
            }

            // Đánh dấu là người chơi hiện tại để đổi tên / đổi effect lúc popup
            // đang mở cũng cập nhật theo, không phải đóng mở lại.
            userNameEffect.SetIsCurrentUser(true);
            userNameEffect.InitUserNameEffect(userName, PlayerData.Instance.EffectNameID.Value);
        }

        private void SetupInstallDate()
        {
            lblInstallDate.text = FormatInstallDate(PlayerData.Instance.InstallDate);
        }

        private static string FormatInstallDate(string installDate)
        {
            if (string.IsNullOrEmpty(installDate))
                return string.Empty;

            // InstallDate is stored with InvariantCulture, so parse with it to stay locale-independent.
            if (DateTime.TryParse(installDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return parsedDate.ToString("MM/yyyy", CultureInfo.InvariantCulture);
            }

            // Fallback: drop the time component (hh:mm:ss) and keep only the date part.
            int spaceIndex = installDate.IndexOf(' ');
            return spaceIndex > 0 ? installDate.Substring(0, spaceIndex) : installDate;
        }

        private void SetupUserAvatarFrame()
        {
            userAvatarFrame.SetUpProfile(PlayerData.Instance.AvatarIndex.Value, PlayerData.Instance.AvatarFrameIndex.Value, PlayerData.Instance.BadgeIndex.Value);
        }
        
        private void SetupGeneralInfo()
        {
            //txtCurrentStreak.text = $"{PlayerData.Instance.WinStreak}";
            txtFirstTryWin.text = $"{PlayerData.Instance.FirstTryCount}";
            //txtCollectionsDone.text = $"{PlayerData.Instance.CollectionsDone}";
            //txtSweetMealChampion.text = $"{PlayerData.Instance.SweetMealChampion}";
            //txtCompleteTreetopMission.text = $"{PlayerData.Instance.CompleteTreetopMission}";
            txtTotalCoinEarned.text = $"{PlayerData.Instance.TotalCoinEarn}";
            txtBoosterUsed.text = $"{PlayerData.Instance.BoosterUsed}";
        }
    }
}