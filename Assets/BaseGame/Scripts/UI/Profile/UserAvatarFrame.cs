using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using Profile.ProfileItem;
using Profile.Manager;
using TW.Reactive.CustomComponent;

namespace Profile.ProfileItem
{
    public class UserAvatarFrame : MonoBehaviour
    {
        [FoldoutGroup("Avatar")][field: SerializeField] public bool autoGetCurrentAvatar = false;
        [FoldoutGroup("Avatar")][field: SerializeField] public bool autoGetIndexAvatar = false;
        [FoldoutGroup("Avatar")][ShowIf("autoGetIndexAvatar")][field: SerializeField] public int indexAutoAvatar = 0;
        [FoldoutGroup("Avatar")]
        [field: SerializeField]
        [Tooltip("Transform để chứa Avatar prefab instance")]
        public Transform avatarContainer;

        [FoldoutGroup("Frame")][field: SerializeField] public bool autoGetCurrentFrame = false;
        [FoldoutGroup("Frame")][field: SerializeField] public bool autoGetIndexFrame = false;
        [FoldoutGroup("Frame")][ShowIf("autoGetIndexFrame")][field: SerializeField] public int indexAutoFrame = 0;
        [FoldoutGroup("Frame")]
        [field: SerializeField]
        [Tooltip("Transform để chứa Frame prefab instance")]
        public Transform frameContainer;

        [FoldoutGroup("Badge")][field: SerializeField] public bool autoGetCurrentBadge = false;
        [FoldoutGroup("Badge")][field: SerializeField] public bool autoGetIndexBadge = false;
        [FoldoutGroup("Badge")][ShowIf("autoGetIndexBadge")][field: SerializeField] public int indexAutoBadge = 0;
        [FoldoutGroup("Badge")][field: SerializeField] public bool disableParentBadge = false;
        [FoldoutGroup("Badge")][ShowIf("disableParentBadge")][field: SerializeField] public GameObject parentBadge;
        [FoldoutGroup("Badge")]
        [field: SerializeField]
        [Tooltip("Transform để chứa Badge prefab instance")]
        public Transform badgeContainer;

        // Prefab instances
        private ProfileItemPrefab currentAvatarPrefab;
        private ProfileItemPrefab currentFramePrefab;
        private ProfileItemPrefab currentBadgePrefab;

        [field: SerializeField] public Reactive<int> AvatarIndex { get; set; } = new(0);
        [field: SerializeField] public Reactive<int> AvatarFrameIndex { get; set; } = new(0);
        [field: SerializeField] public Reactive<int> BadgeIndex { get; set; } = new(0);

        private int idAvatar = -1;
        private int idFrame = -1;
        private int idBadge = -1;

        [field: SerializeField] private bool isCurrentUser = false;

        private void Start()
        {
            AvatarIndex = PlayerData.Instance.AvatarIndex;
            AvatarFrameIndex = PlayerData.Instance.AvatarFrameIndex;
            BadgeIndex = PlayerData.Instance.BadgeIndex;

            AvatarIndex.Subscribe(SetUpAvatarCurrentUser).AddTo(this);
            AvatarFrameIndex.Subscribe(SetUpFrameCurrentUser).AddTo(this);
            BadgeIndex.Subscribe(SetUpBadgeCurrentUser).AddTo(this);

            if (autoGetCurrentAvatar)
            {
                SetUpAvatar(PlayerData.Instance.AvatarIndex.Value);
            }
            if (autoGetCurrentFrame)
            {
                SetUpFrame(PlayerData.Instance.AvatarFrameIndex.Value);
            }
            if (autoGetCurrentBadge)
            {
                SetUpBadge(PlayerData.Instance.BadgeIndex.Value);
            }

            if (autoGetIndexAvatar)
            {
                SetUpAvatar(indexAutoAvatar);
            }
            if (autoGetIndexFrame)
            {
                SetUpFrame(indexAutoFrame);
            }
            if (autoGetIndexBadge)
            {
                SetUpBadge(indexAutoBadge);
            }
        }

        private void OnEnable()
        {
            SetUpAvatar(idAvatar);
            SetUpFrame(idFrame);
            SetUpBadge(idBadge);
        }

        public int GetAvatarIndex()
        {
            return idAvatar;
        }
        public int GetFrameIndex()
        {
            return idFrame;
        }
        public int GetBadgeIndex()
        {
            return idBadge;
        }

        public void SetIsCurrentUser(bool isCurrentUser)
        {
            this.isCurrentUser = isCurrentUser;

            if (this.isCurrentUser)
            {
                SetUpAvatar(PlayerData.Instance.AvatarIndex.Value);
                SetUpFrame(PlayerData.Instance.AvatarFrameIndex.Value);
                SetUpBadge(PlayerData.Instance.BadgeIndex.Value);
            }
        }

        private void SetUpAvatarCurrentUser(int index)
        {
            if (!isCurrentUser)
            {
                return;
            }
            SetUpAvatar(index);
        }

        private void SetUpFrameCurrentUser(int index)
        {
            if (!isCurrentUser)
            {
                return;
            }
            SetUpFrame(index);
        }

        private void SetUpBadgeCurrentUser(int index)
        {
            if (!isCurrentUser)
            {
                return;
            }
            SetUpBadge(index);
        }

        public void SetUpProfile(int avatarIndex, int frameIndex, int badgeIndex)
        {
            SetUpAvatar(avatarIndex);
            SetUpFrame(frameIndex);
            SetUpBadge(badgeIndex);
        }

        public void SetUpAvatar(int index)
        {
            // Rebind cùng avatar (list ảo hoá gọi liên tục khi scroll) → giữ nguyên instance.
            if (currentAvatarPrefab != null && idAvatar == index)
            {
                return;
            }

            ResetAvatar();
            idAvatar = index;

            currentAvatarPrefab = SetUpItem(avatarContainer, index, ProfileItemKind.Avatar);
        }

        public void SetUpFrame(int index)
        {
            if (currentFramePrefab != null && idFrame == index)
            {
                return;
            }

            ResetFrame();
            idFrame = index;

            currentFramePrefab = SetUpItem(frameContainer, index, ProfileItemKind.Frame);
        }

        public void SetUpBadge(int index)
        {
            if (index == -1)
            {
                ResetBadge();
                if (disableParentBadge)
                {
                    parentBadge.SetActive(false);
                }
                return;
            }

            if (ProfileGlobalConfig.Instance.UserBadgeDatas == null || ProfileGlobalConfig.Instance.UserBadgeDatas.Count == 0)
            {
                ResetBadge();
                if (disableParentBadge)
                {
                    parentBadge.SetActive(false);
                }
                return;
            }

            if (currentBadgePrefab != null && idBadge == index)
            {
                return;
            }

            ResetBadge();
            idBadge = index;

            bool hasBadgePrefab = ProfileItemManager.Instance != null &&
                                  ProfileItemManager.Instance.GetBadgePrefab(index) != null;
            if (disableParentBadge)
            {
                parentBadge.SetActive(hasBadgePrefab);
            }

            currentBadgePrefab = SetUpItem(badgeContainer, index, ProfileItemKind.Badge);
        }

        private enum ProfileItemKind
        {
            Avatar,
            Frame,
            Badge,
        }

        private static ProfileItemPrefab GetPrefab(int index, ProfileItemKind kind)
        {
            ProfileItemManager manager = ProfileItemManager.Instance;
            if (manager == null)
                return null;

            switch (kind)
            {
                case ProfileItemKind.Avatar: return manager.GetAvatarPrefab(index);
                case ProfileItemKind.Frame: return manager.GetFramePrefab(index);
                default: return manager.GetBadgePrefab(index);
            }
        }

        private static ProfileItemPrefab RentPrefab(int index, Transform parent, ProfileItemKind kind)
        {
            ProfileItemManager manager = ProfileItemManager.Instance;
            if (manager == null)
                return null;

            switch (kind)
            {
                case ProfileItemKind.Avatar: return manager.RentAvatar(index, parent);
                case ProfileItemKind.Frame: return manager.RentFrame(index, parent);
                default: return manager.RentBadge(index, parent);
            }
        }

        /// <summary>
        /// Dựng 1 profile item vào container: thiếu prefab cho <paramref name="index"/> thì fallback id 0.
        /// Instance lấy từ pool của <see cref="ProfileItemManager"/> thay vì Instantiate/Destroy mỗi lần.
        /// </summary>
        private static ProfileItemPrefab SetUpItem(Transform container, int index, ProfileItemKind kind)
        {
            if (container == null || ProfileItemManager.Instance == null)
            {
                return null;
            }

            int resolvedIndex = GetPrefab(index, kind) != null ? index : 0;
            if (GetPrefab(resolvedIndex, kind) == null)
            {
                return null;
            }

            ProfileItemPrefab instance = RentPrefab(resolvedIndex, container, kind);
            if (instance == null)
            {
                return null;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetupItem();
            return instance;
        }

        private void ResetAvatar()
        {
            idAvatar = -1;
            ReleaseItem(ref currentAvatarPrefab);
            ClearContainer(avatarContainer);
        }

        private void ResetFrame()
        {
            idFrame = -1;
            ReleaseItem(ref currentFramePrefab);
            ClearContainer(frameContainer);
        }

        private void ResetBadge()
        {
            idBadge = -1;
            ReleaseItem(ref currentBadgePrefab);
            ClearContainer(badgeContainer);
        }

        /// <summary>Trả instance về pool; instance không do pool cấp thì Destroy như cũ.</summary>
        private void ReleaseItem(ref ProfileItemPrefab instance)
        {
            if (instance == null)
            {
                instance = null;
                return;
            }

            if (ProfileItemManager.Instance == null || !ProfileItemManager.Instance.Release(instance))
            {
                Destroy(instance.gameObject);
            }

            instance = null;
        }

        public void SetFadeHotPot(float fade)
        {
            // Áp dụng fade cho prefab instances
            if (currentAvatarPrefab != null)
            {
                currentAvatarPrefab.SetFadeAmount(fade);
            }
            if (currentFramePrefab != null)
            {
                currentFramePrefab.SetFadeAmount(fade);
            }
            if (currentBadgePrefab != null)
            {
                currentBadgePrefab.SetFadeAmount(fade);
            }
        }

        private void ClearContainer(Transform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }
}
