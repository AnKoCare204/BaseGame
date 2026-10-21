using Sirenix.OdinInspector;
using UnityEngine;
using TMPro;
using CoreData;

namespace Profile.ProfileItem
{
    public class UserInfoUI : MonoBehaviour
    {
        [field: SerializeField] public UserAvatarFrame userAvatarFrame;

        [FoldoutGroup("Badge")][field: SerializeField] public bool autoGetCurrentBadge = false;
        [FoldoutGroup("Badge")][field: SerializeField] public bool autoGetIndexBadge = false;
        [FoldoutGroup("Badge")][ShowIf("autoGetIndexBadge")][field: SerializeField] public int indexAutoBadge = 0;
        [FoldoutGroup("Badge")]
        [field: SerializeField]
        [Tooltip("Transform để chứa Badge prefab instance")]
        public Transform badgeContainer;

        [FoldoutGroup("Level")][field: SerializeField] public bool autoGetCurrentLevel = false;
        [FoldoutGroup("Level")][ShowIf("autoGetCurrentLevel")][field: SerializeField] public TextMeshProUGUI levelText;

        private void Start()
        {
            userAvatarFrame.badgeContainer = this.badgeContainer;
            if (autoGetCurrentBadge)
            {
                userAvatarFrame.SetUpBadge(PlayerData.Instance.BadgeIndex.Value);
            }

            if (autoGetIndexBadge)
            {
                userAvatarFrame.SetUpBadge(indexAutoBadge);
            }
            
            if (autoGetCurrentLevel && levelText != null)
            {
                levelText.text = PlayerData.Instance.CurrentLevel.Value.ToString();
            }
        }
    }
}

