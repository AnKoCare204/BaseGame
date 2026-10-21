using System.Collections.Generic;
using Profile.ProfileItem;
using UnityEngine;

namespace Profile.Manager
{
    /// <summary>
    /// Manager để quản lý và load profile item prefabs (Avatar/Frame/Badge)
    /// </summary>
    public class ProfileItemManager : MonoBehaviour
    {
        private static ProfileItemManager _instance;
        public static ProfileItemManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ProfileItemManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ProfileItemManager");
                        _instance = go.AddComponent<ProfileItemManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [System.Serializable]
        public class ProfileItemPrefabData
        {
            public int id;
            public ProfileItemPrefab prefab;
        }

        [Header("Avatar Prefabs")]
        [SerializeField] private List<ProfileItemPrefabData> avatarPrefabs = new List<ProfileItemPrefabData>();

        [Header("Frame Prefabs")]
        [SerializeField] private List<ProfileItemPrefabData> framePrefabs = new List<ProfileItemPrefabData>();

        [Header("Badge Prefabs")]
        [SerializeField] private List<ProfileItemPrefabData> badgePrefabs = new List<ProfileItemPrefabData>();

        private Dictionary<int, ProfileItemPrefab> avatarPrefabDict = new Dictionary<int, ProfileItemPrefab>();
        private Dictionary<int, ProfileItemPrefab> framePrefabDict = new Dictionary<int, ProfileItemPrefab>();
        private Dictionary<int, ProfileItemPrefab> badgePrefabDict = new Dictionary<int, ProfileItemPrefab>();

        /// <summary>Instance rảnh theo từng prefab gốc — tránh Instantiate/Destroy khi rebind liên tục (list ảo hoá).</summary>
        private readonly Dictionary<ProfileItemPrefab, Stack<ProfileItemPrefab>> itemPools =
            new Dictionary<ProfileItemPrefab, Stack<ProfileItemPrefab>>();

        /// <summary>Instance đang dùng → prefab gốc, để biết trả về pool nào.</summary>
        private readonly Dictionary<ProfileItemPrefab, ProfileItemPrefab> instanceSource =
            new Dictionary<ProfileItemPrefab, ProfileItemPrefab>();

        private Transform poolRoot;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDictionaries();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDictionaries()
        {
            // Initialize Avatar dictionary
            foreach (var item in avatarPrefabs)
            {
                if (item.prefab != null && !avatarPrefabDict.ContainsKey(item.id))
                {
                    avatarPrefabDict[item.id] = item.prefab;
                }
            }

            // Initialize Frame dictionary
            foreach (var item in framePrefabs)
            {
                if (item.prefab != null && !framePrefabDict.ContainsKey(item.id))
                {
                    framePrefabDict[item.id] = item.prefab;
                }
            }

            // Initialize Badge dictionary
            foreach (var item in badgePrefabs)
            {
                if (item.prefab != null && !badgePrefabDict.ContainsKey(item.id))
                {
                    badgePrefabDict[item.id] = item.prefab;
                }
            }
        }

        /// <summary>
        /// Get Avatar prefab theo id
        /// </summary>
        public ProfileItemPrefab GetAvatarPrefab(int id)
        {
            if (avatarPrefabDict.TryGetValue(id, out ProfileItemPrefab prefab))
            {
                return prefab;
            }
            return null;
        }

        /// <summary>
        /// Get Frame prefab theo id
        /// </summary>
        public ProfileItemPrefab GetFramePrefab(int id)
        {
            if (framePrefabDict.TryGetValue(id, out ProfileItemPrefab prefab))
            {
                return prefab;
            }
            return null;
        }

        /// <summary>
        /// Get Badge prefab theo id
        /// </summary>
        public ProfileItemPrefab GetBadgePrefab(int id)
        {
            if (badgePrefabDict.TryGetValue(id, out ProfileItemPrefab prefab))
            {
                return prefab;
            }
            return null;
        }

        /// <summary>
        /// Instantiate Avatar prefab và setup
        /// </summary>
        public ProfileItemPrefab InstantiateAvatar(int id, Transform parent)
        {
            ProfileItemPrefab prefab = GetAvatarPrefab(id);
            if (prefab != null)
            {
                ProfileItemPrefab instance = Instantiate(prefab, parent);
                return instance;
            }
            return null;
        }

        /// <summary>
        /// Instantiate Frame prefab và setup
        /// </summary>
        public ProfileItemPrefab InstantiateFrame(int id, Transform parent)
        {
            ProfileItemPrefab prefab = GetFramePrefab(id);
            if (prefab != null)
            {
                ProfileItemPrefab instance = Instantiate(prefab, parent);
                return instance;
            }
            return null;
        }

        /// <summary>
        /// Instantiate Badge prefab và setup
        /// </summary>
        public ProfileItemPrefab InstantiateBadge(int id, Transform parent)
        {
            ProfileItemPrefab prefab = GetBadgePrefab(id);
            if (prefab != null)
            {
                ProfileItemPrefab instance = Instantiate(prefab, parent);
                return instance;
            }
            return null;
        }

        /// <summary>Lấy Avatar instance từ pool (Instantiate nếu pool rỗng).</summary>
        public ProfileItemPrefab RentAvatar(int id, Transform parent) => Rent(GetAvatarPrefab(id), parent);

        /// <summary>Lấy Frame instance từ pool (Instantiate nếu pool rỗng).</summary>
        public ProfileItemPrefab RentFrame(int id, Transform parent) => Rent(GetFramePrefab(id), parent);

        /// <summary>Lấy Badge instance từ pool (Instantiate nếu pool rỗng).</summary>
        public ProfileItemPrefab RentBadge(int id, Transform parent) => Rent(GetBadgePrefab(id), parent);

        /// <summary>
        /// Trả instance về pool. Trả <c>false</c> nếu instance không do pool cấp
        /// (caller tự Destroy như cũ).
        /// </summary>
        public bool Release(ProfileItemPrefab instance)
        {
            if (instance == null)
                return false;

            if (!instanceSource.TryGetValue(instance, out ProfileItemPrefab prefab) || prefab == null)
                return false;

            instanceSource.Remove(instance);

            if (!itemPools.TryGetValue(prefab, out Stack<ProfileItemPrefab> pool))
            {
                pool = new Stack<ProfileItemPrefab>();
                itemPools[prefab] = pool;
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(GetPoolRoot(), false);
            pool.Push(instance);
            return true;
        }

        private ProfileItemPrefab Rent(ProfileItemPrefab prefab, Transform parent)
        {
            if (prefab == null)
                return null;

            ProfileItemPrefab instance = null;
            if (itemPools.TryGetValue(prefab, out Stack<ProfileItemPrefab> pool))
            {
                while (pool.Count > 0 && instance == null)
                {
                    // Instance có thể đã bị Destroy cùng scene trước đó.
                    instance = pool.Pop();
                }
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, parent);
            }
            else
            {
                instance.transform.SetParent(parent, false);
                instance.gameObject.SetActive(true);
            }

            instanceSource[instance] = prefab;
            return instance;
        }

        private Transform GetPoolRoot()
        {
            if (poolRoot == null)
            {
                var go = new GameObject("ProfileItemPool");
                go.SetActive(false);
                poolRoot = go.transform;
                poolRoot.SetParent(transform, false);
            }

            return poolRoot;
        }
    }
}

