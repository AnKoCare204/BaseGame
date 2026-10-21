using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace CoreData
{
    /// <summary>
    /// Catalog Addressables RIÊNG cho level, nạp tay từ CDN sau khi Addressables init.
    ///
    /// Bản app KHÔNG bật remote catalog: catalog trong APK chỉ có UI + level bake và không bao giờ bị catalog trên CDN
    /// thay thế. Catalog level chỉ chứa bundle level nên máy nào cũng build + upload được, không cần content state.
    ///
    /// Addressables tự so file .hash kế bên catalog, cache catalog vào persistentDataPath, offline thì dùng bản cache.
    /// </summary>
    public static class LevelCatalog
    {
        // Về Home mới check lại catalog, nhưng không dày hơn 5 phút (khớp cache catalog trên Cloudflare).
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>Locator của catalog level; null khi chưa nạp được (offline lần đầu chưa có cache, CDN lỗi).</summary>
        public static IResourceLocator Locator { get; private set; }

        public static bool IsLoaded => Locator != null;

        /// <summary>Đã thử nạp catalog ít nhất một lần trong phiên, thành công hay không.</summary>
        public static bool HasAttempted { get; private set; }

        private static UniTaskCompletionSource<bool> loading;
        private static AsyncOperationHandle<IResourceLocator> loadHandle;
        private static DateTime lastUpdateCheckUtc = DateTime.MinValue;
        private static bool updateChecking;
        private static float lastFailureRealtime = -1f;

        /// <summary>
        /// Gate có được thử nạp lại catalog ngay không. Sau một lần thất bại gate nghỉ một lúc, để CDN hỏng không làm chậm
        /// mọi lần vào level; về Home và nút Refresh của cheat vẫn thử bất kể.
        /// </summary>
        public static bool IsRetryAllowed =>
            LevelGateRule.IsCatalogRetryAllowed(lastFailureRealtime < 0f ? -1d : Time.realtimeSinceStartup - lastFailureRealtime);

        public const string EditorEnabledPrefKey = "FishSort.LevelCatalog.EditorEnabled";

#if UNITY_EDITOR
        /// <summary>
        /// Trong Editor mặc định KHÔNG nạp catalog CDN: Play mode đọc thẳng file level. Bật trong cửa sổ
        /// Tools/FishSort/Level Addressables khi muốn thử đúng đường Addressables như trên máy thật.
        /// </summary>
        public static bool EditorCdnEnabled => UnityEditor.EditorPrefs.GetBool(EditorEnabledPrefKey, false);

        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode()
        {
            // Enter Play Mode không reload domain thì static còn sống từ phiên trước.
            Locator = null;
            HasAttempted = false;
            loading = null;
            loadHandle = default;
            lastUpdateCheckUtc = DateTime.MinValue;
            updateChecking = false;
            lastFailureRealtime = -1f;
        }
#endif

        /// <summary>
        /// Nạp catalog. Đang nạp thì đợi chung; đã nạp thành công thì trả true ngay. Lần nạp THẤT BẠI không được giữ lại:
        /// lần gọi sau sẽ nạp lại (mở app offline rồi có mạng thì gate tự lấy được catalog).
        /// </summary>
        public static async UniTask<bool> LoadAsync()
        {
            if (IsLoaded) return true;
            if (loading != null) return await loading.Task;

            loading = new UniTaskCompletionSource<bool>();
            bool ok = false;
            try
            {
                ok = await LoadInternalAsync();
            }
            finally
            {
                HasAttempted = true;
                lastFailureRealtime = ok ? -1f : Time.realtimeSinceStartup;
                UniTaskCompletionSource<bool> finished = loading;
                loading = null;
                finished.TrySetResult(ok);
            }

            return ok;
        }

        private static async UniTask<bool> LoadInternalAsync()
        {
#if UNITY_EDITOR
            if (!EditorCdnEnabled)
            {
                Debug.Log("[LevelCatalog] Editor: bỏ qua catalog CDN (bật trong Tools/FishSort/Level Addressables nếu cần).");
                return false;
            }
#endif
            string url = LevelCdn.CatalogUrl;
            try
            {
                loadHandle = Addressables.LoadContentCatalogAsync(url, false);
                IResourceLocator locator = await loadHandle.ToUniTask();
                if (locator == null)
                {
                    Debug.LogWarning($"[LevelCatalog] Catalog null: {url}");
                    ReleaseLoadHandle();
                    return false;
                }

                Locator = locator;
                Debug.Log($"[LevelCatalog] Loaded {url}");
                return true;
            }
            catch (Exception e)
            {
                // Offline chưa có cache, hoặc CDN lỗi: level bake trong APK vẫn chơi bình thường.
                Debug.LogWarning($"[LevelCatalog] Load failed: {e.Message}");
                ReleaseLoadHandle();
                return false;
            }
        }

        private static void ReleaseLoadHandle()
        {
            if (loadHandle.IsValid()) Addressables.Release(loadHandle);
            loadHandle = default;
        }

        /// <summary>Location của address trong catalog level; null nếu catalog chưa nạp hoặc không có address.</summary>
        public static IResourceLocation TryLocate(string address)
        {
            return TryLocateAll(address, out IList<IResourceLocation> locations) ? locations[0] : null;
        }

        /// <summary>Mọi location của key (address hoặc label) trong catalog level.</summary>
        public static bool TryLocateAll(object key, out IList<IResourceLocation> locations)
        {
            locations = null;
            if (Locator == null || key == null) return false;
            return Locator.Locate(key, typeof(TextAsset), out locations) && locations != null && locations.Count > 0;
        }

        /// <summary>Mọi label chunk (lvl_*) có trong catalog level, chưa sắp xếp.</summary>
        public static List<string> GetChunkLabels()
        {
            List<string> labels = new List<string>();
            if (Locator == null) return labels;
            foreach (object key in Locator.Keys)
            {
                if (key is string s && s.StartsWith(LevelAddress.LabelPrefix, StringComparison.Ordinal)) labels.Add(s);
            }

            return labels;
        }

        /// <summary>
        /// Gọi mỗi lần về Home (giãn 5 phút). Catalog chưa nạp được thì thử nạp lại; đã nạp thì so .hash trên CDN, khác thì
        /// thay catalog rồi tải nền các chunk đã đổi. force = true bỏ qua giãn cách (nút Refresh levels của cheat).
        /// </summary>
        public static async UniTask CheckForUpdateAsync(bool force = false)
        {
            if (updateChecking) return;
            if (Application.internetReachability == NetworkReachability.NotReachable) return;
            if (!force && DateTime.UtcNow - lastUpdateCheckUtc < UpdateCheckInterval) return;
#if UNITY_EDITOR
            if (!EditorCdnEnabled) return;
#endif
            lastUpdateCheckUtc = DateTime.UtcNow;
            updateChecking = true;
            try
            {
                if (Locator == null)
                {
                    if (await LoadAsync()) LevelAddressableProvider.RequestSyncAllChunks();
                    return;
                }

                List<string> outdated;
                AsyncOperationHandle<List<string>> check = Addressables.CheckForCatalogUpdates(false);
                try
                {
                    outdated = await check.ToUniTask();
                }
                finally
                {
                    if (check.IsValid()) Addressables.Release(check);
                }

                if (outdated == null || outdated.Count == 0) return;

                List<IResourceLocator> locators;
                AsyncOperationHandle<List<IResourceLocator>> update = Addressables.UpdateCatalogs(outdated, false);
                try
                {
                    locators = await update.ToUniTask();
                }
                finally
                {
                    if (update.IsValid()) Addressables.Release(update);
                }

                // Locator mới thay locator cũ. Chỉ catalog level có .hash nên thường chỉ có đúng một cái.
                if (locators != null)
                {
                    IResourceLocator replacement = null;
                    foreach (IResourceLocator locator in locators)
                    {
                        if (locator == null) continue;
                        if (locator.LocatorId == Locator.LocatorId)
                        {
                            replacement = locator;
                            break;
                        }

                        replacement ??= locator;
                    }

                    if (replacement != null) Locator = replacement;
                }

                Debug.Log("[LevelCatalog] Catalog updated, sync chunk.");
                LevelAddressableProvider.RequestSyncAllChunks();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelCatalog] CheckForUpdate failed: {e.Message}");
            }
            finally
            {
                updateChecking = false;
            }
        }
    }
}
