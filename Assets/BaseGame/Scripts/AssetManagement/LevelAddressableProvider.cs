using System;
using System.Collections.Generic;
using Core.GamePlay;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace CoreData
{
    /// <summary>
    /// Load level qua Addressables với hai nguồn: level bake trong APK (catalog của app) và level trên CDN (catalog riêng,
    /// xem <see cref="LevelCatalog"/>). CDN mirror TOÀN BỘ level, pack theo chunk (label lvl_{n}); bundle tên theo hash nên
    /// chỉ chunk đổi mới phải tải lại.
    ///
    /// Quy tắc chọn bản: bản server đã tải về, rồi bản bake, rồi bản server chưa tải (chỉ với level ngoài APK). Riêng level 1-2
    /// (LevelSourceRule) luôn chơi bản bake, bỏ qua CDN và bộ test. Chơi lại đủ lần thì biến thể LevelData_{n}_{x} thay bản gốc
    /// (LevelVariantRule), trừ khi bộ test đang bật có level đó.
    /// Trong Editor, khi chưa bật "nạp catalog CDN", mọi thứ đọc thẳng file level trên đĩa.
    /// </summary>
    public static class LevelAddressableProvider
    {
        public const float BootRemoteWaitSeconds = 10f;
        public const float GateRemoteWaitSeconds = 3f;
        public const float GateTestDownloadSeconds = 10f;

        // Số lượt tải đang chặn người chơi; sync nền nhường băng thông bằng cách dừng giữa các chunk.
        private static int gateDownloadCount;
        private static bool syncRunning;
        private static bool syncRequested;
        private static bool initialized;
        private static bool loadBusy;
        private static string reportedMissingVersion;

        // Bảng biến thể: theo catalog CDN (dựng lại khi LevelCatalog đổi locator) và theo catalog của app (dựng một lần).
        private static IResourceLocator variantIndexLocator;
        private static LevelVariantIndex remoteVariantIndex = LevelVariantIndex.Empty;
        private static LevelVariantIndex localVariantIndex;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode()
        {
            gateDownloadCount = 0;
            syncRunning = false;
            syncRequested = false;
            initialized = false;
            loadBusy = false;
            reportedMissingVersion = null;
            variantIndexLocator = null;
            remoteVariantIndex = LevelVariantIndex.Empty;
            localVariantIndex = null;
        }

        private static bool UseEditorFiles => !LevelCatalog.EditorCdnEnabled;
#endif

        private static bool IsOnline => Application.internetReachability != NetworkReachability.NotReachable;

        private static bool IsRemoteConfigRead => RemoteConfigManager.Instance.IsRemoteConfigLoaded;

        private static string GetTestVersionFor(LevelRequest request)
        {
            return request.AllowTest ? LevelTestSelector.ActiveVersion : "";
        }

        // ================== RESOLVE ==================

        /// <summary>
        /// Tra level ở cả hai catalog, đã tính bộ test và biến thể: chỉ dùng bản test khi chunk test đã tải về; chưa thì trả bản gốc
        /// và tải nền chunk test. Không có bộ test cho level này thì xét biến thể (LevelVariantRule) trước bản gốc.
        /// </summary>
        public static async UniTask<LevelLocation> ResolveAsync(LevelRequest request)
        {
            if (!request.IsValid) return default;
            if (LevelSourceRule.IsApkOnly(request.SourceLevel))
            {
                LevelLocation baked = await LocateApkOnlyAsync(request.Address);
                if (baked.HasAny) return baked;   // app thiếu level (build thường không xảy ra): theo luật thường, CDN cứu
            }

            bool testHasLevel = false;
            string version = GetTestVersionFor(request);
            if (version.Length > 0)
            {
                LevelLocation test = await LocateAsync(LevelAddress.GetTestAddress(request.SourceLevel, version), version);
                if (test.RemoteCached) return test;
                if (test.Remote != null)
                {
                    testHasLevel = true;
                    PrefetchChunkAsync(LevelAddress.GetTestLabel(version)).Forget();
                }
            }

            // Bộ test có level này (kể cả bản test chưa tải xong): nhóm A/B không bao giờ dính biến thể.
            if (!testHasLevel)
            {
                LevelLocation variant = await LocateVariantAsync(request);
                if (variant.HasAny) return variant;
            }

            return await LocateAsync(request.Address, "");
        }

        private static async UniTask<LevelLocation> LocateAsync(string address, string testVersion)
        {
            await EnsureInitializedAsync();
            IResourceLocation remote = LevelCatalog.TryLocate(address);
            // Bộ test chỉ có trên CDN. Trong Editor catalog của app có thể thấy cả entry test, không được dùng bản đó.
            IResourceLocation local = string.IsNullOrEmpty(testVersion) ? LocateLocal(address) : null;
            bool cached = remote != null && await GetDownloadSizeAsync(remote) == 0;
            return new LevelLocation(remote, local, cached, testVersion);
        }

        /// <summary>Level chỉ chơi bản bake (LevelSourceRule): chỉ tra catalog của app; không thấy thì trả default.</summary>
        private static async UniTask<LevelLocation> LocateApkOnlyAsync(string address)
        {
            await EnsureInitializedAsync();
            IResourceLocation local = LocateLocal(address);
            return local != null ? LevelLocation.ApkOnly(local) : default;
        }

        // Catalog của APK = mọi locator trừ locator của catalog level.
        private static IResourceLocation LocateLocal(string address)
        {
            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                if (locator == null || locator == LevelCatalog.Locator || locator.LocatorId == LevelCdn.CatalogUrl) continue;
                if (locator.Locate(address, typeof(TextAsset), out IList<IResourceLocation> locations) && locations.Count > 0)
                {
                    return locations[0];
                }
            }

            return null;
        }

        private static async UniTask EnsureInitializedAsync()
        {
            if (initialized) return;
            AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync(false);
            try
            {
                await handle.ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelAddressable] Addressables init failed: {e.Message}");
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            initialized = true;
        }

        // ================== BIẾN THỂ ==================

        /// <summary>
        /// Bậc biến thể cao nhất đã có trên máy cho lượt này; bậc cao hơn có trên CDN mà chưa tải thì tải nền chunk của level.
        /// default (HasAny = false) = không dùng biến thể. Không bao giờ bắt người chơi chờ tải biến thể.
        /// </summary>
        private static async UniTask<LevelLocation> LocateVariantAsync(LevelRequest request)
        {
            if (!LevelVariantRule.IsEligible(request)) return default;
            await EnsureInitializedAsync();

            List<int> candidates = LevelVariantRule.Candidates(request.RetryCount, GetVariantIndex().GetVariants(request.SourceLevel));
            bool prefetched = false;
            foreach (int variant in candidates)
            {
                LevelLocation location = await LocateAsync(LevelAddress.GetVariantAddress(request.SourceLevel, variant), "");
                if (location.IsReadyOffline) return location.WithVariant(variant);
                if (location.Remote != null && !prefetched)
                {
                    prefetched = true;
                    PrefetchChunkAsync(LevelAddress.GetChunkLabel(request.SourceLevel)).Forget();
                }
            }

            return default;
        }

        /// <summary>
        /// Danh sách biến thể đang dùng: theo catalog CDN khi đã nạp (xoá biến thể trên CDN là tắt nó, kể cả bản đã bake), không
        /// thì theo catalog của app (offline từ lúc mở app, chưa có cache catalog).
        /// </summary>
        private static LevelVariantIndex GetVariantIndex()
        {
            IResourceLocator remote = LevelCatalog.Locator;
            if (remote != null)
            {
                if (remote != variantIndexLocator)
                {
                    variantIndexLocator = remote;
                    remoteVariantIndex = LevelVariantIndex.Build(remote.Keys);
                }

                return remoteVariantIndex;
            }

            return localVariantIndex ??= BuildLocalVariantIndex();
        }

        // Catalog của app = mọi locator trừ locator của catalog level (giống LocateLocal).
        private static LevelVariantIndex BuildLocalVariantIndex()
        {
            List<object> keys = new List<object>();
            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                if (locator == null || locator == LevelCatalog.Locator || locator.LocatorId == LevelCdn.CatalogUrl) continue;
                keys.AddRange(locator.Keys);
            }

            return LevelVariantIndex.Build(keys);
        }

        // ================== LOAD ==================

        /// <summary>Load data theo quy tắc chọn bản; server có bản chưa tải thì tải nền cho lần sau.</summary>
        public static async UniTask<LevelLoadResult> LoadLevelDataForRequestAsync(LevelRequest request)
        {
            if (!request.IsValid) return default;
#if UNITY_EDITOR
            if (UseEditorFiles) return LoadFromEditorFile(request);
#endif
            LevelLocation location = await ResolveAsync(request);
            if (!location.HasAny)
            {
                Debug.LogError($"[LevelAddressable] Level {request.Level} ({request.Address}) không có ở APK lẫn CDN.");
                return default;
            }

            if (location.Remote != null && !location.RemoteCached && location.Preferred != location.Remote)
            {
                PrefetchChunkAsync(LevelAddress.GetChunkLabelForAddress(location.Remote.PrimaryKey)).Forget();
            }

            LevelLoadResult result = await LoadWithFallbackAsync(location);
            if (!result.Success && (location.TestVersion.Length > 0 || location.Variant > 0))
            {
                // Bản test / biến thể hỏng: người chơi vẫn phải vào được level, dùng bản production.
                string what = location.Variant > 0 ? $"Biến thể {location.Variant}" : $"Bản test '{location.TestVersion}'";
                Debug.LogWarning($"[LevelAddressable] {what} của level {request.Level} không dùng được, chuyển sang bản production.");
                LevelLocation production = await LocateAsync(request.Address, "");
                if (production.HasAny) result = await LoadWithFallbackAsync(production);
            }

            return result;
        }

        /// <summary>
        /// Nạp bản được chọn; bản CDN không nạp hoặc không giải mã được thì thử bản trong APK. Một lần upload lỗi không
        /// được chặn level mà bản cài vẫn có sẵn.
        /// </summary>
        private static async UniTask<LevelLoadResult> LoadWithFallbackAsync(LevelLocation location)
        {
            IResourceLocation chosen = location.Preferred;
            if (chosen == null) return default;

            LevelData data = await LoadLevelDataAsync(chosen);
            if (data == null)
            {
                IResourceLocation fallback = location.FallbackFor(chosen);
                if (fallback != null)
                {
                    Debug.LogWarning($"[LevelAddressable] Bản CDN của '{chosen.PrimaryKey}' không dùng được, chuyển sang bản trong APK.");
                    chosen = fallback;
                    data = await LoadLevelDataAsync(chosen);
                }
            }

            return new LevelLoadResult(data, data != null ? location.DescribeSource(chosen) : "", data != null ? location.Variant : 0);
        }

        private static async UniTask<LevelData> LoadLevelDataAsync(IResourceLocation location)
        {
            // Hàng đợi tuần tự: bundle trong APK và bundle trên CDN của cùng một chunk có cùng tên nội bộ,
            // nạp hai bản cùng lúc sẽ bị Unity từ chối.
            while (loadBusy) await UniTask.Yield();
            loadBusy = true;

            AsyncOperationHandle<TextAsset> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<TextAsset>(location);
                TextAsset textAsset = await handle.ToUniTask();
                if (textAsset == null) return null;
                return LevelDataCodec.TryDecode(textAsset.text, out LevelData data) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelAddressable] Load '{location.PrimaryKey}' failed: {e.Message}");
                return null;
            }
            finally
            {
                // LevelData đã parse là object thuần C#, release bundle ngay được.
                if (handle.IsValid()) Addressables.Release(handle);
                loadBusy = false;
            }
        }

        /// <summary>
        /// Đọc Difficulty thật từ file level, chỉ từ bản có sẵn trên máy (không kéo mạng chỉ để hiện một cái nhãn).
        /// null nếu chưa có bản nào trên máy.
        /// </summary>
        public static async UniTask<LevelDifficulty?> PeekLevelDifficultyAsync(LevelRequest request)
        {
            if (!request.IsValid) return null;
#if UNITY_EDITOR
            if (UseEditorFiles)
            {
                LevelLoadResult fromFile = LoadFromEditorFile(request);
                return fromFile.Success ? fromFile.Data.Difficulty : (LevelDifficulty?)null;
            }
#endif
            LevelLocation location = await ResolveAsync(request);
            if (!location.IsReadyOffline) return null;
            LevelLoadResult result = await LoadWithFallbackAsync(location);
            return result.Success ? result.Data.Difficulty : (LevelDifficulty?)null;
        }

#if UNITY_EDITOR
        private static LevelLoadResult LoadFromEditorFile(LevelRequest request)
        {
            bool testHasLevel = false;
            string version = GetTestVersionFor(request);
            if (version.Length > 0)
            {
                string testPath = LevelFileReader.GetTestLevelPath(version, request.SourceLevel);
                testHasLevel = System.IO.File.Exists(testPath);
                LevelData test = LevelFileReader.Read(testPath);
                if (test != null) return new LevelLoadResult(test, $"{LevelSource.EditorFile}:{LevelSource.TestPrefix}{version}");
            }

            // Như ResolveAsync: bộ test có level này thì không xét biến thể. File biến thể đọc lỗi thì lùi bậc.
            if (!testHasLevel && LevelVariantRule.IsEligible(request))
            {
                IReadOnlyList<int> variants = LevelFileReader.GetVariantIndex().GetVariants(request.SourceLevel);
                foreach (int variant in LevelVariantRule.Candidates(request.RetryCount, variants))
                {
                    LevelData data = LevelFileReader.Read(LevelFileReader.GetVariantLevelPath(request.SourceLevel, variant));
                    if (data != null) return new LevelLoadResult(data, LevelSource.WithVariant(LevelSource.EditorFile, variant), variant);
                    Debug.LogWarning($"[LevelAddressable] Biến thể {variant} của level {request.SourceLevel} không đọc được, thử bậc thấp hơn.");
                }
            }

            LevelData production = LevelFileReader.Read(LevelGlobalConfig.Instance.GetLevelFilePath(request.SourceLevel));
            return new LevelLoadResult(production, LevelSource.EditorFile);
        }
#endif

        // ================== GATE ==================

        /// <summary>
        /// Gọi TRƯỚC khi vào level. true = level đã sẵn trên máy (kể cả bản test nếu bộ test đang bật và tải được).
        /// waitForRemote = false ở đường vào level lúc mở app, để không làm chậm lần mở đầu.
        /// onPhase báo cho UI biết lúc nào cần hiện loading; luôn kết thúc bằng Idle.
        /// </summary>
        public static async UniTask<bool> EnsureReadyAsync(LevelRequest request, bool waitForRemote, Action<LevelGatePhase> onPhase)
        {
            if (!request.IsValid) return false;
#if UNITY_EDITOR
            if (UseEditorFiles) return LoadFromEditorFile(request).Success;
#endif
            try
            {
                // Level chỉ chơi bản bake có sẵn trong app: vào ngay, không đợi Remote Config / catalog, không tải bộ test.
                if (LevelSourceRule.IsApkOnly(request.SourceLevel) && (await LocateApkOnlyAsync(request.Address)).HasAny) return true;

                LevelLocation location = await LocateAsync(request.Address, "");

                if (waitForRemote)
                {
                    // Chỉ đợi khi câu trả lời có thể đổi kết quả: CDN hỏng không được làm chậm level đã nằm trên máy.
                    LevelGateWait wait = LevelGateRule.Decide(IsOnline, location.IsReadyOffline, IsRemoteConfigRead,
                        GetTestVersionFor(request).Length > 0, LevelCatalog.IsLoaded, LevelCatalog.IsRetryAllowed);
                    if (wait != LevelGateWait.None)
                    {
                        onPhase?.Invoke(LevelGatePhase.Waiting);
                        await UniTask.WhenAny(SettleRemoteStateAsync(wait),
                            UniTask.Delay(TimeSpan.FromSeconds(GateRemoteWaitSeconds), ignoreTimeScale: true));
                    }
                }

                // Hỏi lại sau khi đợi: Remote Config vừa đọc xong có thể bật một bộ test.
                string version = GetTestVersionFor(request);
                if (version.Length > 0 && IsOnline)
                {
                    LevelLocation test = await LocateAsync(LevelAddress.GetTestAddress(request.SourceLevel, version), version);
                    if (test.Remote != null && !test.RemoteCached)
                    {
                        onPhase?.Invoke(LevelGatePhase.Downloading);
                        // Tải lỗi hoặc quá giờ thì bỏ qua: level vào bằng bản production, chunk test tải tiếp ở nền.
                        await UniTask.WhenAny(DownloadAsync(test.Remote).AsUniTask(),
                            UniTask.Delay(TimeSpan.FromSeconds(GateTestDownloadSeconds), ignoreTimeScale: true));
                    }
                }

                if (location.IsReadyOffline) return true;

                if (!LevelCatalog.IsLoaded && LevelCatalog.IsRetryAllowed)
                {
                    onPhase?.Invoke(LevelGatePhase.Waiting);
                    await LevelCatalog.LoadAsync();
                }

                location = await LocateAsync(request.Address, "");
                if (location.IsReadyOffline) return true;
                if (location.Remote == null) return false;

                onPhase?.Invoke(LevelGatePhase.Downloading);
                return await DownloadAsync(location.Remote);
            }
            finally
            {
                onPhase?.Invoke(LevelGatePhase.Idle);
            }
        }

        private static async UniTask SettleRemoteStateAsync(LevelGateWait wait)
        {
            if ((wait & LevelGateWait.Catalog) != 0 && !LevelCatalog.IsLoaded) await LevelCatalog.LoadAsync();
            if ((wait & LevelGateWait.RemoteConfig) == 0) return;
            while (!IsRemoteConfigRead) await UniTask.Yield();
        }

        /// <summary>Tải bundle remote của một location. false = lỗi mạng.</summary>
        private static async UniTask<bool> DownloadAsync(IResourceLocation remote)
        {
            if (remote == null) return false;
            gateDownloadCount++;
            AsyncOperationHandle handle = default;
            try
            {
                handle = Addressables.DownloadDependenciesAsync(new List<IResourceLocation> { remote }, false);
                await handle.ToUniTask();
                return handle.Status == AsyncOperationStatus.Succeeded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelAddressable] Download '{remote.PrimaryKey}' failed (offline?): {e.Message}");
                return false;
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
                gateDownloadCount--;
            }
        }

        private static async UniTask<long> GetDownloadSizeAsync(object locationOrList)
        {
            AsyncOperationHandle<long> handle = default;
            try
            {
                handle = Addressables.GetDownloadSizeAsync(locationOrList);
                return await handle.ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelAddressable] GetDownloadSize failed: {e.Message}");
                return -1;
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        // ================== PREFETCH / SYNC ==================

        /// <summary>
        /// Gọi một lần lúc loading: nạp catalog (không chặn loading), đợi Remote Config để biết bộ test nào đang bật,
        /// rồi tải chunk quanh người chơi và sync toàn bộ chunk đã đổi.
        /// </summary>
        public static void StartBootSync(int currentLevel)
        {
            BootSyncAsync(currentLevel).Forget();
        }

        private static async UniTask BootSyncAsync(int currentLevel)
        {
#if UNITY_EDITOR
            if (UseEditorFiles) return;
#endif
            if (!await LevelCatalog.LoadAsync()) return;

            float deadline = Time.realtimeSinceStartup + BootRemoteWaitSeconds;
            while (!IsRemoteConfigRead && Time.realtimeSinceStartup < deadline) await UniTask.Yield();

            await PrefetchChunkAsync(LevelAddress.GetChunkLabel(currentLevel));
            await PrefetchChunkAsync(LevelAddress.GetChunkLabel(currentLevel + LevelAddress.ChunkSize));
            string version = LevelTestSelector.ActiveVersion;
            if (version.Length > 0) await PrefetchChunkAsync(LevelAddress.GetTestLabel(version));
            RequestSyncAllChunks();
        }

        /// <summary>Tải nền chunk hiện tại + kế tiếp (gọi sau mỗi lần load level).</summary>
        public static void PrefetchUpcoming(int level)
        {
#if UNITY_EDITOR
            if (UseEditorFiles) return;
#endif
            PrefetchChunkAsync(LevelAddress.GetChunkLabel(level)).Forget();
            PrefetchChunkAsync(LevelAddress.GetChunkLabel(level + LevelAddress.ChunkSize)).Forget();
        }

        /// <summary>
        /// Quét mọi chunk trong catalog level, tải chunk nào chưa có trong cache (chunk mới hoặc đã đổi hash).
        /// Gọi trong lúc đang chạy thì chạy thêm một vòng sau khi xong (catalog hoặc bộ test vừa đổi).
        /// </summary>
        public static void RequestSyncAllChunks()
        {
#if UNITY_EDITOR
            if (UseEditorFiles) return;
#endif
            if (syncRunning)
            {
                syncRequested = true;
                return;
            }

            SyncAllChunksAsync().Forget();
        }

        private static async UniTask SyncAllChunksAsync()
        {
            syncRunning = true;
            try
            {
                do
                {
                    syncRequested = false;
                    if (!LevelCatalog.IsLoaded) return;
                    VerifyRemoteTestVersion();

                    List<string> labels = LevelCatalog.GetChunkLabels();
                    int currentChunk = (Mathf.Max(1, UserData.Instance.CurrentLevel) - 1) / LevelAddress.ChunkSize;
                    labels.Sort((a, b) => LevelAddress.SyncOrder(a, currentChunk).CompareTo(LevelAddress.SyncOrder(b, currentChunk)));

                    // Bộ test: chỉ tải bộ đang bật, không tải bộ của nhóm khác.
                    string version = LevelTestSelector.ActiveVersion;
                    string activeTestLabel = version.Length > 0 ? LevelAddress.GetTestLabel(version) : null;

                    foreach (string label in labels)
                    {
                        if (LevelAddress.IsTestLabel(label) && label != activeTestLabel) continue;

                        // Mất mạng thì dừng; lần về Home sau (CheckForUpdateAsync) sẽ sync tiếp.
                        if (!IsOnline) return;

                        // Gate đang tải chặn người chơi thì nhường, xong mới tải tiếp.
                        while (gateDownloadCount > 0)
                        {
                            await UniTask.Delay(TimeSpan.FromMilliseconds(500), ignoreTimeScale: true);
                        }

                        await PrefetchChunkAsync(label);
                    }

                    Debug.Log("[LevelAddressable] Sync chunk xong.");
                } while (syncRequested);
            }
            finally
            {
                syncRunning = false;
            }
        }

        private static async UniTask PrefetchChunkAsync(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (!LevelCatalog.TryLocateAll(label, out IList<IResourceLocation> locations)) return; // chunk không có trên CDN
            if (!IsOnline) return;

            AsyncOperationHandle handle = default;
            try
            {
                long size = await GetDownloadSizeAsync(locations);
                if (size <= 0) return; // đã cache, hoặc không xác định được
                handle = Addressables.DownloadDependenciesAsync(locations, false);
                await handle.ToUniTask();
                Debug.Log($"[LevelAddressable] Chunk '{label}' downloaded ({size / 1024f:F0} KB).");
            }
            catch (Exception)
            {
                // Mất mạng giữa chừng: bỏ qua, lần sync sau (về Home) thử lại.
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        // ================== BỘ TEST ==================

        /// <summary>
        /// Danh sách bộ test có đáng tin không. Catalog chưa nạp được thì danh sách rỗng chỉ có nghĩa là "chưa biết",
        /// không phải "không có bộ nào".
        /// </summary>
        public static bool AreTestVersionsAuthoritative
        {
            get
            {
#if UNITY_EDITOR
                if (UseEditorFiles) return true;
#endif
                return LevelCatalog.IsLoaded;
            }
        }

        /// <summary>
        /// Khi đã có cả catalog lẫn Remote Config: bộ test mà Remote Config chỉ định không có trong catalog (sai tên, sai
        /// hoa thường, chưa upload) thì cảnh báo và ghi user property là "off", vì người chơi thực tế chơi bản production.
        /// </summary>
        private static void VerifyRemoteTestVersion()
        {
            if (!LevelCatalog.IsLoaded || !IsRemoteConfigRead) return;
            TestLevelRemoteConfigData remote = RemoteConfigManager.Instance.TestLevelRemoteConfigData;
            if (remote == null) return;

            bool available = LevelTestSelector.IsVersionAvailable(remote.TestVersion, GetAvailableTestVersions());
            if (remote.IsEnabled && !available && reportedMissingVersion != remote.TestVersion)
            {
                reportedMissingVersion = remote.TestVersion;
                Debug.LogWarning($"[LevelAddressable] Remote Config bật bộ test '{remote.TestVersion}' nhưng catalog không có bộ này " +
                                 "(sai tên, sai hoa thường, hoặc chưa upload). Người chơi dùng bản production.");
            }

            remote.ReportUserProperty(available);
        }

        /// <summary>Các bộ test đang có: theo label trong catalog CDN, hoặc theo thư mục con TestLevel khi Editor đọc file.</summary>
        public static List<string> GetAvailableTestVersions()
        {
#if UNITY_EDITOR
            if (UseEditorFiles) return LevelFileReader.GetTestVersions();
#endif
            List<string> versions = new List<string>();
            foreach (string label in LevelCatalog.GetChunkLabels())
            {
                if (LevelAddress.IsTestLabel(label)) versions.Add(LevelAddress.GetVersionFromTestLabel(label));
            }

            versions.Sort(StringComparer.Ordinal);
            return versions;
        }
    }
}
