using TW.Utility.DesignPattern;

namespace Profile.Manager
{
    public class ProfileDataManager : Singleton<ProfileDataManager>
    {
        // Badge bắt buộc đeo khi đã unlock mà người chơi chưa đeo badge nào.
        private const int ForcedBadgeIndex = 1;

        private void Start()
        {
            ProfileData.Instance.CheckEnoughData();
            ForceEquipUnlockedBadge();
            EnsureNameEffectUnlocked();
        }

        /// <summary>
        /// Chạy mỗi lần vào game: badge <see cref="ForcedBadgeIndex"/> đã unlock trong ProfileData mà
        /// PlayerData chưa đeo badge nào (BadgeIndex &lt; 0) thì tự đeo luôn.
        /// Người chơi đã tự chọn một badge khác thì giữ nguyên lựa chọn của họ.
        /// </summary>
        private void ForceEquipUnlockedBadge()
        {
            PlayerData playerData = PlayerData.Instance;
            if (playerData == null || playerData.BadgeIndex.Value >= 0) return;

            BadgeData badgeData = ProfileData.Instance.BadgeDatas.Find(x => x.BadgeIndex == ForcedBadgeIndex);
            if (badgeData == null || !badgeData.IsUnlocked) return;

            playerData.BadgeIndex.Value = ForcedBadgeIndex;
            playerData.SavePlayerData();
        }

        /// <summary>
        /// Chạy mỗi lần vào game: name effect đang đeo mà chưa mở khoá (hoặc id rác
        /// từ bản save cũ) thì kéo về <see cref="PlayerData.DefaultEffectNameId"/> —
        /// id ai cũng có. Nhờ vậy người chơi thường luôn hiển thị đúng tên mặc định,
        /// kể cả khi effect bị gỡ khỏi config hay bản save bị chỉnh tay.
        /// </summary>
        private void EnsureNameEffectUnlocked()
        {
            PlayerData playerData = PlayerData.Instance;
            if (playerData == null) return;

            int equipped = playerData.EffectNameID.Value;
            if (equipped == PlayerData.DefaultEffectNameId) return;

            EffectNameData nameData = ProfileData.Instance.EffectNameDatas
                .Find(x => x.NameIndex == equipped);

            if (nameData != null && nameData.IsUnlocked) return;

            playerData.EffectNameID.Value = PlayerData.DefaultEffectNameId;
            playerData.SavePlayerData();
        }
    }
}
