using System.Collections.Generic;
using UnityEngine;

namespace Profile.NameEffects
{
    /// <summary>
    /// Pool instance theo prefab — giống <c>ProfileItemManager</c> nhưng riêng cho
    /// name effect để không phải sửa manager đang chạy. List ảo hoá rebind liên
    /// tục khi cuộn, Instantiate/Destroy mỗi lần là nguồn giật chính.
    /// </summary>
    public class NameEffectPool : MonoBehaviour
    {
        private static NameEffectPool instance;
        private static bool applicationQuitting;

        public static NameEffectPool Instance
        {
            get
            {
                if (applicationQuitting)
                    return null;

                if (instance == null)
                {
                    instance = FindFirstObjectByType<NameEffectPool>();

                    if (instance == null)
                    {
                        var go = new GameObject(nameof(NameEffectPool));
                        instance = go.AddComponent<NameEffectPool>();
                        DontDestroyOnLoad(go);
                    }
                }

                return instance;
            }
        }

        /// <summary>
        /// Instance đang có, KHÔNG tự tạo. Dùng khi đang huỷ scene: tạo GameObject
        /// lúc đó là lỗi runtime của Unity.
        /// </summary>
        public static NameEffectPool ExistingInstance => applicationQuitting ? null : instance;

        private readonly Dictionary<NameEffectView, Stack<NameEffectView>> pools =
            new Dictionary<NameEffectView, Stack<NameEffectView>>();

        private readonly Dictionary<NameEffectView, NameEffectView> instanceSource =
            new Dictionary<NameEffectView, NameEffectView>();

        private Transform poolRoot;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        public NameEffectView Rent(NameEffectView prefab, Transform parent)
        {
            if (prefab == null)
                return null;

            NameEffectView view = null;

            if (pools.TryGetValue(prefab, out Stack<NameEffectView> pool))
            {
                // Instance cũ có thể đã bị Destroy cùng scene trước.
                while (pool.Count > 0 && view == null)
                    view = pool.Pop();
            }

            if (view == null)
            {
                view = Instantiate(prefab, parent);
            }
            else
            {
                view.transform.SetParent(parent, false);
                view.gameObject.SetActive(true);
            }

            instanceSource[view] = prefab;
            return view;
        }

        /// <summary>
        /// Trả instance về pool. Trả <c>false</c> nếu instance không do pool cấp —
        /// caller tự Destroy.
        /// </summary>
        public bool Release(NameEffectView view)
        {
            if (view == null)
                return false;

            if (!instanceSource.TryGetValue(view, out NameEffectView prefab) || prefab == null)
                return false;

            instanceSource.Remove(view);

            if (!pools.TryGetValue(prefab, out Stack<NameEffectView> pool))
            {
                pool = new Stack<NameEffectView>();
                pools[prefab] = pool;
            }

            view.ReleaseView();
            view.gameObject.SetActive(false);
            view.transform.SetParent(GetPoolRoot(), false);
            pool.Push(view);
            return true;
        }

        private Transform GetPoolRoot()
        {
            if (poolRoot == null)
            {
                var go = new GameObject("NameEffectPoolRoot");
                go.SetActive(false);
                poolRoot = go.transform;
                poolRoot.SetParent(transform, false);
            }

            return poolRoot;
        }
    }
}
