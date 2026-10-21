using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Profile.ProfileItem
{
    public class UserTeamCell : MonoBehaviour
    {
        [SerializeField] public TextMeshProUGUI lblTeamName;
        [SerializeField] public Image imgTeamLogo;
        [SerializeField] public Sprite spriteNoTeam;
        [SerializeField] public bool isAutoHideWhenNoTeam = true;

        private string _teamName = "";
        private int _teamAvatar = -1;

        public void SetTeamInfo(string teamName, int teamAvatar)
        {
            _teamName = teamName;
            _teamAvatar = teamAvatar;
            if (teamAvatar <= 0)
            {
                lblTeamName.text = "---";
                imgTeamLogo.sprite = spriteNoTeam;
                if (isAutoHideWhenNoTeam)
                {
                    this.gameObject.SetActive(false);
                }
                return;
            }

            this.gameObject.SetActive(true);

            lblTeamName.text = string.IsNullOrEmpty(teamName) ? "---" : teamName;
            //imgTeamLogo.sprite = TeamDataGlobalConfig.Instance.GetLogoTeamConfig(teamAvatar).LogoSprite;
        }

        public string GetTeamName()
        {
            return _teamName;
        }

        public int GetTeamAvatar()
        {
            return _teamAvatar;
        }
    }
}
