using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoreData;
using CoreGame.GameGlobalConfig;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Kết quả đánh dấu entry. MissingLevels hoặc InvalidFiles khác rỗng thì không được build: người chơi sẽ gặp level thiếu hoặc hỏng.</summary>
public class LevelEntryReport
{
    public int MaxLevel;
    public int MainCount;
    public int SkippedAboveMax;
    public int VariantCount;
    public int VariantSkippedAboveMax;

    /// <summary>Biến thể của level 1-2: game không dùng (FTUE), không đóng gói.</summary>
    public List<string> VariantSkippedApkOnly = new List<string>();
    public List<int> MissingLevels = new List<int>();
    public List<string> IgnoredFiles = new List<string>();

    /// <summary>File level (production hoặc test) không giải mã được, hoặc giải mã ra level rỗng.</summary>
    public List<string> InvalidFiles = new List<string>();

    public Dictionary<string, int> TestPerVersion = new Dictionary<string, int>();

    public bool IsComplete => MissingLevels.Count == 0 && InvalidFiles.Count == 0;

    public string Describe()
    {
        string tests = string.Join(", ", TestPerVersion.Select(kv => $"{kv.Key}={kv.Value}"));
        string text = $"main={MainCount} (1..{MaxLevel}), bỏ qua {SkippedAboveMax} level > {MaxLevel}, test [{tests}]";
        text += $", biến thể {VariantCount}";
        if (VariantSkippedAboveMax > 0) text += $" (bỏ qua {VariantSkippedAboveMax} biến thể của level > {MaxLevel})";
        if (VariantSkippedApkOnly.Count > 0) text += $", BỎ QUA biến thể level 1-2: {string.Join(", ", VariantSkippedApkOnly)}";
        if (MissingLevels.Count > 0) text += $", THIẾU FILE level: {string.Join(", ", MissingLevels)}";
        if (InvalidFiles.Count > 0) text += $", FILE LEVEL HỎNG: {string.Join(", ", InvalidFiles)}";
        if (IgnoredFiles.Count > 0) text += $", bỏ qua file sai chỗ / sai tên: {string.Join(", ", IgnoredFiles)}";
        return text;
    }
}

/// <summary>
/// Tool level Addressables, mô hình "bake toàn bộ, CDN mirror toàn bộ":
/// - APK bake mọi level 1..LevelMaxInConfig (group Levels_Main, build local). App KHÔNG bật remote catalog.
/// - Build Levels: cùng group đó build sang remote (chunk theo label lvl_{n}) + group Levels_Test, ra catalog RIÊNG
///   catalog_levels.bin mà LevelCatalog nạp tay lúc runtime.
/// - Hai layout trên cùng settings: App (trạng thái commit) và Levels (chỉ đặt tạm trong lúc Build Levels).
/// Hàm trong class này KHÔNG mở dialog (điều khiển được qua MCP); dialog nằm ở LevelAddressableWindow.
/// </summary>
public static class LevelAddressableSetup
{
    public enum Layout
    {
        App,
        Levels,
    }

    public const string MainGroupName = "Levels_Main";
    public const string TestGroupName = "Levels_Test";
    private const string ProfileDefaultName = "Default";
    private const string ProfileLevelsName = "Levels";
    private const string MenuRoot = "Tools/FishSort/Level Addressables/";

    // Output "local" của layout Levels (settings.json, catalog local, link.xml) đi vào đây để không đè output của app.
    private const string LevelsLocalBuildPath = "Library/com.unity.addressables/aa-levels/[BuildTarget]";

    // Variant lấy từ scripting define của Build Profile đang active. Không có define thì không build / upload được.
#if CHEAT_ONLY
    public const string Variant = LevelCdn.VariantCheat;
#elif RELEASE_ONLY
    public const string Variant = LevelCdn.VariantRelease;
#else
    public const string Variant = null;
#endif

    public const string NoVariantMessage =
        "Build profile hiện tại không có define CHEAT_ONLY hoặc RELEASE_ONLY. Mở File > Build Profiles, chọn profile Cheat hoặc Release làm active rồi chờ editor recompile.";

    public static bool HasVariant => !string.IsNullOrEmpty(Variant);

    public static string ActiveBuildTargetName =>
        EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : "Android";

    /// <summary>URL catalog mà runtime sẽ nạp cho variant + platform hiện tại.</summary>
    public static string RuntimeCatalogUrl => HasVariant ? LevelCdn.BuildCatalogUrl(Variant, ActiveBuildTargetName) : "";

    public static string UploadPrefix => HasVariant ? LevelCdn.BuildUploadPrefix(Variant) : "";

    public static string RemoteDest => HasVariant ? LevelCdn.BuildRemoteDest(Variant, ActiveBuildTargetName) : "";

    public static string ServerDataDir =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "ServerData", ActiveBuildTargetName);

    public static string MarkerPath => Path.Combine(ServerDataDir, LevelUploadPlan.VariantMarkerFileName);

    // ======================================================
    // ================= VARIANT MARKER =====================
    // ======================================================

    /// <summary>ServerData hiện tại được build cho bản nào; null nếu chưa build hoặc build lỗi.</summary>
    public static string GetBuiltVariant() => ReadMarker(MarkerPath);

    private static string ReadMarker(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static void ClearMarker(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // không xoá được thì lần ghi sau sẽ đè
        }
    }

    private static void WriteMarker(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Variant ?? "");
    }

    // Bundle tên theo hash nên file cũ trong ServerData không bao giờ bị ghi đè; dọn trước mỗi lần build
    // để Upload chỉ đẩy đúng file của lần build này.
    private static void CleanServerData()
    {
        if (!Directory.Exists(ServerDataDir)) return;
        foreach (string file in Directory.GetFiles(ServerDataDir))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelAddressableSetup] Không xoá được {file}: {e.Message}");
            }
        }
    }

    // ======================================================
    // ====================== LAYOUT ========================
    // ======================================================

    /// <summary>Layout đang nằm trong settings (đọc từ cờ remote catalog và schema group chính).</summary>
    public static Layout CurrentLayout
    {
        get
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return Layout.App;
            if (settings.BuildRemoteCatalog) return Layout.Levels;
            BundledAssetGroupSchema schema = settings.FindGroup(MainGroupName)?.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) return Layout.App;
            return schema.BuildPath.GetName(settings) == AddressableAssetSettings.kRemoteBuildPath ? Layout.Levels : Layout.App;
        }
    }

    /// <summary>
    /// App: Levels_Main build local (bake vào APK), Levels_Test không build, mọi group khác build, KHÔNG remote catalog.
    /// Levels: Levels_Main + Levels_Test build remote, mọi group khác không build, remote catalog catalog_levels.*,
    /// profile "Levels" (output local vào thư mục riêng).
    /// </summary>
    public static void ApplyLayout(Layout layout)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        bool levels = layout == Layout.Levels;
        EnsureProfiles(settings, levels);
        AddressableAssetGroup main = EnsureGroup(settings, MainGroupName);
        AddressableAssetGroup test = EnsureGroup(settings, TestGroupName);

        ConfigureSchema(settings, main, remote: levels, includeInBuild: true);
        ConfigureSchema(settings, test, remote: true, includeInBuild: levels);
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null || group == main || group == test) continue;
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null || bundled.IncludeInBuild == !levels) continue;
            bundled.IncludeInBuild = !levels;
            EditorUtility.SetDirty(group);
        }

        string profileId = settings.profileSettings.GetProfileId(levels ? ProfileLevelsName : ProfileDefaultName);
        if (!string.IsNullOrEmpty(profileId)) settings.activeProfileId = profileId;

        settings.BuildRemoteCatalog = levels;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        settings.OverridePlayerVersion = LevelCdn.CatalogVersion;
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        // Bake vào settings.json của app: timeout tải catalog level (+ .hash).
        settings.CatalogRequestsTimeout = 10;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
    }

    private static void EnsureProfiles(AddressableAssetSettings settings, bool levels)
    {
        AddressableAssetProfileSettings profiles = settings.profileSettings;
        string defaultId = profiles.GetProfileId(ProfileDefaultName);
        if (string.IsNullOrEmpty(defaultId)) defaultId = settings.activeProfileId;

        // Ở layout App không group nào load từ Remote.LoadPath, nên ghi một giá trị cố định (bản Release) để file settings
        // trong git không đổi qua lại theo máy. Giá trị theo variant chỉ đặt trong lúc Build Levels.
        string neutral = LevelCdn.BuildRemoteLoadPath(LevelCdn.VariantRelease);
        string forBuild = HasVariant ? LevelCdn.BuildRemoteLoadPath(Variant) : neutral;
        string remoteLoadPath = levels ? forBuild : neutral;

        EnsureVariable(profiles, defaultId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
        EnsureVariable(profiles, defaultId, AddressableAssetSettings.kRemoteLoadPath, neutral);

        string levelsId = profiles.GetProfileId(ProfileLevelsName);
        if (string.IsNullOrEmpty(levelsId)) levelsId = profiles.AddProfile(ProfileLevelsName, defaultId);

        profiles.SetValue(defaultId, AddressableAssetSettings.kRemoteLoadPath, neutral);
        profiles.SetValue(levelsId, AddressableAssetSettings.kRemoteLoadPath, remoteLoadPath);
        profiles.SetValue(levelsId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
        // Addressables validate MỌI group (kể cả IncludeInBuild = false): Local.BuildPath và Local.LoadPath phải cùng
        // dùng dynamic-lookup hoặc cùng không. Layout Levels không có group nào load local nên cả hai trỏ thư mục riêng.
        profiles.SetValue(levelsId, AddressableAssetSettings.kLocalBuildPath, LevelsLocalBuildPath);
        profiles.SetValue(levelsId, AddressableAssetSettings.kLocalLoadPath, LevelsLocalBuildPath);
    }

    private static void EnsureVariable(AddressableAssetProfileSettings profiles, string profileId, string name, string value)
    {
        if (profiles.GetValueByName(profileId, name) == null) profiles.CreateValue(name, value);
    }

    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
    {
        AddressableAssetGroup group = settings.FindGroup(name);
        if (group != null) return group;
        return settings.CreateGroup(name, false, false, false, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
    }

    private static void ConfigureSchema(AddressableAssetSettings settings, AddressableAssetGroup group, bool remote, bool includeInBuild)
    {
        BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
        bundled.BuildPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
        bundled.LoadPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
        // Cả local lẫn remote đều chunk theo label: tên chunk khớp giữa APK và CDN.
        bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
        bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        bundled.IncludeInBuild = includeInBuild;
        bundled.UseAssetBundleCache = true;
        bundled.UseAssetBundleCrc = true;
        bundled.UseAssetBundleCrcForCachedBundles = true;
        bundled.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
        bundled.RetryCount = 3;
        bundled.Timeout = 10;
        EditorUtility.SetDirty(group);
    }

    // ======================================================
    // ===================== ENTRIES ========================
    // ======================================================

    /// <summary>
    /// Đảm bảo mọi file level 1..LevelMaxInConfig và mọi biến thể LevelData_{n}_{x} hợp lệ là entry của Levels_Main, mọi file
    /// trong TestLevel/{Version}/ là entry của Levels_Test. File ngoài dải, sai tên, hoặc đặt ở gốc TestLevel bị gỡ entry. Kết thúc
    /// ở layout App.
    /// </summary>
    public static LevelEntryReport EnsureAllLevelEntries()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        ApplyLayout(Layout.App);
        AddressableAssetGroup main = settings.FindGroup(MainGroupName);
        AddressableAssetGroup test = settings.FindGroup(TestGroupName);

        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        LevelEntryReport report = new LevelEntryReport { MaxLevel = config.LevelMaxInConfig };
        List<int> present = new List<int>();
        List<string> packedPaths = new List<string>();

        string folder = config.LevelFolder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets quét cả thư mục con; level production chỉ nằm ngay trong thư mục này.
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder) continue;

                LevelFileClass file = LevelEntryPlanner.ClassifyMain(Path.GetFileNameWithoutExtension(path), report.MaxLevel);
                switch (file.Kind)
                {
                    case LevelFileKind.Main:
                        ApplyEntry(settings, main, guid, file);
                        packedPaths.Add(path);
                        present.Add(file.Level);
                        report.MainCount++;
                        break;
                    case LevelFileKind.MainAboveMax:
                        RemoveEntry(settings, guid);
                        report.SkippedAboveMax++;
                        break;
                    default:
                        RemoveEntry(settings, guid);
                        report.IgnoredFiles.Add(path);
                        break;
                }
            }
        }

        report.MissingLevels = LevelEntryPlanner.FindMissingLevels(present, report.MaxLevel);

        // Biến thể: chung group và chung label chunk với level gốc, nên bake vào app và lên CDN trong bundle của chunk đó.
        string variantFolder = LevelAddress.VariantFolder;
        if (AssetDatabase.IsValidFolder(variantFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { variantFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != variantFolder)
                {
                    RemoveEntry(settings, guid);
                    report.IgnoredFiles.Add(path);
                    continue;
                }

                LevelFileClass file = LevelEntryPlanner.ClassifyVariant(Path.GetFileNameWithoutExtension(path), report.MaxLevel);
                switch (file.Kind)
                {
                    case LevelFileKind.Variant:
                        ApplyEntry(settings, main, guid, file);
                        packedPaths.Add(path);
                        report.VariantCount++;
                        break;
                    case LevelFileKind.VariantAboveMax:
                        RemoveEntry(settings, guid);
                        report.VariantSkippedAboveMax++;
                        break;
                    case LevelFileKind.VariantApkOnly:
                        RemoveEntry(settings, guid);
                        report.VariantSkippedApkOnly.Add(path);
                        break;
                    default:
                        RemoveEntry(settings, guid);
                        report.IgnoredFiles.Add(path);
                        break;
                }
            }
        }

        string testFolder = LevelFileReader.TestLevelFolder;
        if (AssetDatabase.IsValidFolder(testFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { testFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string relative = path.Substring(testFolder.Length).TrimStart('/');
                LevelFileClass file = LevelEntryPlanner.ClassifyTest(relative);
                if (file.Kind != LevelFileKind.Test)
                {
                    RemoveEntry(settings, guid);
                    report.IgnoredFiles.Add(path);
                    continue;
                }

                ApplyEntry(settings, test, guid, file);
                packedPaths.Add(path);
                report.TestPerVersion[file.Version] = report.TestPerVersion.TryGetValue(file.Version, out int count) ? count + 1 : 1;
            }
        }

        // Tên file đúng chưa đủ: file dính conflict marker sau khi merge, hoặc level lưu dở không có bóng, vẫn sẽ được đóng gói
        // và tới tay người chơi nếu không chặn ở đây.
        report.InvalidFiles = FindUnreadableLevelFiles(packedPaths);

        RemoveUnusedLevelLabels(settings);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        return report;
    }

    /// <summary>Các file không giải mã ra được một level chơi được (xem LevelDataCodec.IsPlayable).</summary>
    public static List<string> FindUnreadableLevelFiles(IEnumerable<string> paths)
    {
        List<string> invalid = new List<string>();
        if (paths == null) return invalid;
        foreach (string path in paths)
        {
            if (LevelFileReader.Read(path) == null) invalid.Add(path);
        }

        return invalid;
    }

    private static void ApplyEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string guid, LevelFileClass file)
    {
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = file.Address;
        settings.AddLabel(file.Label, false);

        // Mỗi entry giữ đúng một label chunk (PackTogetherByLabel pack theo tập label đầy đủ).
        foreach (string old in entry.labels.Where(l => l.StartsWith(LevelAddress.LabelPrefix) && l != file.Label).ToList())
        {
            entry.SetLabel(old, false, false, false);
        }

        entry.SetLabel(file.Label, true, true, false);
    }

    private static void RemoveEntry(AddressableAssetSettings settings, string guid)
    {
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null) return;
        if (entry.parentGroup == null) return;
        if (entry.parentGroup.Name != MainGroupName && entry.parentGroup.Name != TestGroupName) return;
        settings.RemoveAssetEntry(guid, false);
    }

    // Label lvl_* không còn entry nào dùng (bộ test đã xoá, chunk đã rút) thì gỡ khỏi bảng label.
    private static void RemoveUnusedLevelLabels(AddressableAssetSettings settings)
    {
        HashSet<string> used = new HashSet<string>();
        foreach (string groupName in new[] { MainGroupName, TestGroupName })
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null) continue;
            foreach (AddressableAssetEntry entry in group.entries)
            {
                foreach (string label in entry.labels) used.Add(label);
            }
        }

        foreach (string label in settings.GetLabels().Where(l => l.StartsWith(LevelAddress.LabelPrefix) && !used.Contains(l)).ToList())
        {
            settings.RemoveLabel(label, false);
        }
    }

    /// <summary>Đánh dấu entry, dựng lại cache độ khó, kết thúc ở layout App. false khi thiếu file level trong dải.</summary>
    public static bool SetupAll(out string message)
    {
        LevelEntryReport report = EnsureAllLevelEntries();
        LevelGlobalConfig.Instance.RebuildLevelCacheFromFolder();
        AssetDatabase.SaveAssets();
        message = report.Describe();
        if (report.IsComplete) Debug.Log($"[LevelAddressableSetup] Setup xong (layout App): {message}");
        else Debug.LogError($"[LevelAddressableSetup] Setup xong nhưng level thiếu hoặc hỏng: {message}");
        if (report.IgnoredFiles.Count > 0)
        {
            Debug.LogWarning($"[LevelAddressableSetup] Bỏ qua {report.IgnoredFiles.Count} file sai chỗ hoặc sai tên: {string.Join(", ", report.IgnoredFiles)}");
        }

        if (report.VariantSkippedApkOnly.Count > 0)
        {
            Debug.LogWarning($"[LevelAddressableSetup] Game không dùng biến thể của level 1-{LevelSourceRule.ApkOnlyMaxLevel} (FTUE), không đóng gói: {string.Join(", ", report.VariantSkippedApkOnly)}");
        }

        return report.IsComplete;
    }

    // ======================================================
    // ================== BUILD LEVELS ======================
    // ======================================================

    /// <summary>
    /// Build toàn bộ level + bộ test thành bundle remote + catalog_levels.* vào ServerData/[target].
    /// Không phụ thuộc bản app nào. Luôn trả settings về layout App, kể cả khi lỗi.
    /// </summary>
    public static bool BuildLevels(out string message)
    {
        if (!HasVariant)
        {
            message = NoVariantMessage;
            Debug.LogError($"[LevelAddressableSetup] {message}");
            return false;
        }

        // Addressables hỏi "Unsaved Scenes" khi còn scene chưa lưu; hộp thoại đó chặn editor nên dừng từ đây.
        message = LevelEntryPlanner.DescribeUnsavedScenes(GetDirtySceneNames());
        if (message != null)
        {
            Debug.LogError($"[LevelAddressableSetup] Build Levels LỖI: {message}");
            return false;
        }

        CleanServerData();
        bool ok = RunInLevelsLayout(RunAddressablesBuild, MarkerPath, out message);
        if (ok) Debug.Log($"[LevelAddressableSetup] Build Levels xong cho bản {Variant}: {message} -> {ServerDataDir}");
        else Debug.LogError($"[LevelAddressableSetup] Build Levels LỖI: {message}");
        return ok;
    }

    private static List<string> GetDirtySceneNames()
    {
        List<string> names = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty) names.Add(scene.name);
        }

        return names;
    }

    private static string RunAddressablesBuild()
    {
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        return result != null ? result.Error : "BuildPlayerContent không trả kết quả";
    }

    /// <summary>
    /// Chạy build trong layout Levels rồi trả về layout App trong finally. Marker bị xoá TRƯỚC khi build và chỉ ghi lại
    /// SAU khi build thành công, để một lần build lỗi không để lại marker cũ khiến Upload tưởng ServerData còn hợp lệ.
    /// build trả chuỗi lỗi, null hoặc rỗng = thành công.
    /// </summary>
    public static bool RunInLevelsLayout(Func<string> build, string markerPath, out string message, bool saveAssets = true)
    {
        ClearMarker(markerPath);
        try
        {
            LevelEntryReport report = EnsureAllLevelEntries();
            if (!report.IsComplete)
            {
                message = report.Describe();
                return false;
            }

            ApplyLayout(Layout.Levels);
            string error = build();
            if (!string.IsNullOrEmpty(error))
            {
                message = error;
                return false;
            }

            WriteMarker(markerPath);
            message = report.Describe();
            return true;
        }
        catch (Exception e)
        {
            message = e.Message;
            return false;
        }
        finally
        {
            ApplyLayout(Layout.App);
            // Test truyền saveAssets = false: SaveAssets ghi MỌI asset đang dirty trong editor, kể cả việc dở của người dùng.
            if (saveAssets) AssetDatabase.SaveAssets();
        }
    }

    // ======================================================
    // ================ UPLOAD LÊN R2 (CDN) =================
    // ======================================================

    private static System.Diagnostics.Process uploadProcess;
    private static readonly Queue<string> uploadStepArgs = new Queue<string>();
    private static Action<bool, string> uploadFinished;

    public static bool IsUploading => (uploadProcess != null && !uploadProcess.HasExited) || uploadStepArgs.Count > 0;

    /// <summary>Số file và dung lượng sẽ upload theo phạm vi, để cửa sổ hỏi xác nhận.</summary>
    public static void DescribeUpload(LevelUploadScope scope, out int fileCount, out long totalBytes)
    {
        fileCount = 0;
        totalBytes = 0;
        if (!Directory.Exists(ServerDataDir)) return;
        foreach (FileInfo file in new DirectoryInfo(ServerDataDir).GetFiles())
        {
            if (!LevelUploadPlan.IsIncluded(file.Name, scope)) continue;
            totalBytes += file.Length;
            fileCount++;
        }
    }

    /// <summary>
    /// Bắt đầu upload, KHÔNG hỏi xác nhận (nơi gọi chịu trách nhiệm hỏi). Chạy nền hai bước: bundle trước, catalog sau.
    /// false kèm message khi chưa đủ điều kiện; onFinished báo kết quả khi chạy xong.
    /// </summary>
    public static bool StartUpload(LevelUploadScope scope, Action<bool, string> onFinished, out string message)
    {
        if (IsUploading)
        {
            message = "Đang có một phiên upload chạy dở.";
            return false;
        }

        if (!LevelUploadPlan.CanUpload(GetBuiltVariant(), Variant, out message)) return false;

        if (RcloneRunner.RclonePath == null)
        {
            message = "Chưa tìm thấy rclone trên máy. Mở phần 'Cấu hình R2' trong cửa sổ Level Addressables để cài.";
            return false;
        }

        DescribeUpload(scope, out int fileCount, out long totalBytes);
        if (fileCount == 0)
        {
            message = $"Không có file nào trong {ServerDataDir} khớp phạm vi {LevelUploadPlan.ScopeLabel(scope)}.";
            return false;
        }

        uploadStepArgs.Clear();
        foreach (string step in LevelUploadPlan.BuildUploadSteps(ServerDataDir, RemoteDest, scope))
        {
            uploadStepArgs.Enqueue(step);
        }

        uploadFinished = onFinished;
        Debug.Log($"[R2Upload] Bắt đầu upload bản {Variant} ({LevelUploadPlan.ScopeLabel(scope)}): {fileCount} file, {totalBytes / 1024} KB -> {RemoteDest}");
        message = "";
        StartNextUploadStep();
        return true;
    }

    private static void FinishUpload(bool ok, string message)
    {
        uploadStepArgs.Clear();
        uploadProcess = null;
        if (ok) Debug.Log($"[R2Upload] {message}");
        else Debug.LogError($"[R2Upload] {message}");
        Action<bool, string> callback = uploadFinished;
        uploadFinished = null;
        callback?.Invoke(ok, message);
    }

    private static void StartNextUploadStep()
    {
        if (uploadStepArgs.Count == 0)
        {
            FinishUpload(true, "Upload thành công.");
            return;
        }

        System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo
        {
            FileName = RcloneRunner.RclonePath,
            Arguments = uploadStepArgs.Dequeue(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            uploadProcess = System.Diagnostics.Process.Start(info);
        }
        catch (Exception e)
        {
            FinishUpload(false, $"Không chạy được rclone: {e.Message}");
            return;
        }

        uploadProcess.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[R2Upload] {e.Data}");
        };
        uploadProcess.ErrorDataReceived += (_, e) =>
        {
            // rclone -v ghi stats / log qua stderr, không phải lỗi thật; lỗi thật quyết định bằng exit code.
            if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[R2Upload] {e.Data}");
        };
        uploadProcess.BeginOutputReadLine();
        uploadProcess.BeginErrorReadLine();

        EditorApplication.update += WatchUploadProcess;
    }

    private static void WatchUploadProcess()
    {
        if (uploadProcess == null || !uploadProcess.HasExited) return;

        EditorApplication.update -= WatchUploadProcess;
        int exitCode = uploadProcess.ExitCode;
        uploadProcess = null;

        if (exitCode != 0)
        {
            FinishUpload(false, $"Upload THẤT BẠI (rclone exit {exitCode}). Xem log [R2Upload] phía trên.");
            return;
        }

        StartNextUploadStep();
    }

    // ======================================================
    // ============== DỌN FILE CŨ TRÊN CDN ==================
    // ======================================================
    // Upload dùng `rclone copy` nên không bao giờ xoá trên đích; bundle tên theo hash nên mỗi lần sửa level là một chunk
    // mới nằm cạnh chunk cũ. Không dùng `rclone sync` vì nó xoá ngay trong bước bundle, trước khi catalog mới lên.

    [Serializable]
    private class RemoteFileList
    {
        public RemoteFileInfo[] items;
    }

    /// <summary>Liệt kê file đang có trên CDN của variant + platform hiện tại.</summary>
    public static bool TryListRemoteFiles(out List<RemoteFileInfo> files, out string error)
    {
        files = new List<RemoteFileInfo>();
        error = null;
        if (!HasVariant)
        {
            error = NoVariantMessage;
            return false;
        }

        if (!RcloneRunner.RunSplit($"lsjson \"{RemoteDest}\" --files-only", out string stdout, out string stderr, 60000))
        {
            error = string.IsNullOrEmpty(stderr) ? stdout : stderr;
            return false;
        }

        string json = stdout.Trim();
        if (string.IsNullOrEmpty(json) || json == "[]") return true;

        try
        {
            RemoteFileList list = JsonUtility.FromJson<RemoteFileList>("{\"items\":" + json + "}");
            if (list?.items != null)
            {
                foreach (RemoteFileInfo file in list.items)
                {
                    if (file != null && !file.IsDir && !string.IsNullOrEmpty(file.Path)) files.Add(file);
                }
            }

            return true;
        }
        catch (Exception e)
        {
            error = $"Không parse được output lsjson: {e.Message}";
            return false;
        }
    }

    /// <summary>
    /// File trên CDN không có trong ServerData hiện tại và cũ hơn graceDays. null kèm lỗi nếu ServerData chưa được build
    /// cho đúng variant (không biết file nào đang dùng thì không được xoá gì).
    /// </summary>
    public static List<RemoteFileInfo> FindStaleRemoteFiles(List<RemoteFileInfo> remote, int graceDays, out string error)
    {
        if (!Directory.Exists(ServerDataDir) || Directory.GetFiles(ServerDataDir).Length == 0)
        {
            error = "Chưa có ServerData. Bấm 'Build Levels' trước để biết file nào đang được dùng.";
            return null;
        }

        if (!LevelUploadPlan.CanUpload(GetBuiltVariant(), Variant, out error)) return null;

        // ServerData phải chính là thứ đang chạy trên CDN, nếu không bundle đang dùng sẽ bị coi là rác.
        string hashPath = Path.Combine(ServerDataDir, LevelCdn.CatalogHashFileName);
        string localHash = File.Exists(hashPath) ? File.ReadAllText(hashPath) : "";
        RcloneRunner.RunSplit($"cat \"{RemoteDest}/{LevelCdn.CatalogHashFileName}\"", out string remoteHash, out _, 60000);
        if (!LevelUploadPlan.CanClean(localHash, remoteHash, out error)) return null;

        List<string> current = Directory.GetFiles(ServerDataDir).Select(Path.GetFileName).ToList();
        error = null;
        return LevelUploadPlan.FindStale(remote, current, graceDays, DateTime.UtcNow);
    }

    /// <summary>Xoá đúng danh sách file đã quét bằng --files-from. KHÔNG hỏi xác nhận (nơi gọi chịu trách nhiệm hỏi).</summary>
    public static bool DeleteRemoteFiles(List<RemoteFileInfo> files, out string output)
    {
        output = "";
        if (files == null || files.Count == 0) return true;

        string listPath = Path.Combine(Path.GetTempPath(), $"fishsort_r2_cleanup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllLines(listPath, files.Select(f => f.Path));
        try
        {
            bool ok = RcloneRunner.Run($"delete \"{RemoteDest}\" --files-from \"{listPath}\" -v", out output, 300000);
            Debug.Log($"[R2Cleanup] rclone delete exit={(ok ? 0 : 1)} ({files.Count} file)\n{output}");
            return ok;
        }
        finally
        {
            try
            {
                File.Delete(listPath);
            }
            catch
            {
                // file tạm, hệ điều hành tự dọn
            }
        }
    }

    // ======================================================
    // ============ MENU KHÔNG DIALOG (cho MCP) =============
    // ======================================================

    [MenuItem(MenuRoot + "Setup")]
    private static void SetupMenu() => SetupAll(out _);

    [MenuItem(MenuRoot + "Build Levels")]
    private static void BuildLevelsMenu() => BuildLevels(out _);

    [MenuItem(MenuRoot + "Restore App Layout")]
    private static void RestoreAppLayoutMenu()
    {
        ApplyLayout(Layout.App);
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelAddressableSetup] Đã về layout App.");
    }

    [MenuItem(MenuRoot + "Log Status")]
    private static void LogStatusMenu()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        int mainCount = settings?.FindGroup(MainGroupName)?.entries.Count ?? 0;
        int testCount = settings?.FindGroup(TestGroupName)?.entries.Count ?? 0;
        int serverFiles = Directory.Exists(ServerDataDir) ? Directory.GetFiles(ServerDataDir).Length : 0;
        Debug.Log($"[LevelAddressableStatus] variant={Variant ?? "(none)"} target={ActiveBuildTargetName} layout={CurrentLayout} " +
                  $"main={mainCount} test={testCount} max={LevelGlobalConfig.Instance.LevelMaxInConfig} " +
                  $"serverData={serverFiles} built={GetBuiltVariant() ?? "(none)"} url={RuntimeCatalogUrl} rclone={RcloneRunner.RclonePath ?? "(none)"}");
    }

    /// <summary>
    /// Tạo bộ test mẫu VersionSample: level 2 mang nội dung của level 3, để thử luồng level test mà không cần designer
    /// chuẩn bị gì. Xoá thư mục TestLevel/VersionSample khi không cần nữa.
    /// </summary>
    [MenuItem(MenuRoot + "Create Sample Test Set")]
    private static void CreateSampleTestSetMenu()
    {
        LevelData source = LevelFileReader.Read(LevelGlobalConfig.Instance.GetLevelFilePath(3));
        if (source == null)
        {
            Debug.LogError("[LevelAddressableSetup] Không đọc được level 3 để làm mẫu.");
            return;
        }

        source.EditorSetLevel(2);
        string path = LevelFileReader.GetTestLevelPath("VersionSample", 2);
        if (!LevelFileReader.Write(path, source, out string message))
        {
            Debug.LogError($"[LevelAddressableSetup] {message}");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[LevelAddressableSetup] Đã tạo bộ mẫu {path}");
    }
}
