using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoreData;
using CoreGame.GameGlobalConfig;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ quản lý level Addressables: Setup, Build Levels (bundle + catalog_levels.* cho CDN), upload lên R2, dọn CDN.
/// Variant (Cheat / Release) tự nhận từ scripting define của Build Profile đang active, KHÔNG cho chọn tay để không thể
/// build bản này upload nhầm bản kia. Đây là nơi duy nhất mở dialog; mọi việc thật nằm ở LevelAddressableSetup.
/// </summary>
public class LevelAddressableWindow : EditorWindow
{
    [MenuItem("Tools/FishSort/Level Addressables/Open Window", priority = -10)]
    public static void Open()
    {
        LevelAddressableWindow window = GetWindow<LevelAddressableWindow>("Level Addressables");
        window.minSize = new Vector2(460, 540);
    }

    // Trạng thái rclone (null = chưa kiểm tra).
    private static bool? rcloneInstalled;
    private static bool? remoteConfigured;

    // Form nhập key chỉ nằm trong RAM tới khi bấm Lưu (ghi vào rclone.conf), KHÔNG lưu vào EditorPrefs hay project.
    private string accountId = "";
    private string accessKey = "";
    private string secretKey = "";
    private bool showR2Config;

    private bool showCleanup;
    private int cleanupGraceDays = 2;
    private List<RemoteFileInfo> staleFiles;
    private string cleanupStatus;
    private Vector2 cleanupScroll;

    private static readonly string[] UploadScopeLabels = { "Cả level gốc + test", "Chỉ level gốc", "Chỉ level test" };
    private static int uploadScopeIndex;

    private void OnInspectorUpdate() => Repaint();

    private void OnEnable()
    {
        RefreshRcloneStatus();
        showR2Config = remoteConfigured != true;
    }

    private void OnGUI()
    {
        GUILayout.Space(8);
        DrawVariantBanner();
        GUILayout.Space(8);

        if (!LevelAddressableSetup.HasVariant)
        {
            EditorGUILayout.HelpBox(LevelAddressableSetup.NoVariantMessage, MessageType.Error);
            return;
        }

        DrawInfo();
        GUILayout.Space(8);
        DrawR2Config();
        GUILayout.Space(12);
        DrawButtons();
        GUILayout.Space(12);
        DrawCleanup();
    }

    private void DrawVariantBanner()
    {
        bool isCheat = LevelAddressableSetup.Variant == LevelCdn.VariantCheat;
        bool isRelease = LevelAddressableSetup.Variant == LevelCdn.VariantRelease;
        Color background = isCheat ? new Color(0.95f, 0.65f, 0.1f, 0.45f)
            : isRelease ? new Color(0.25f, 0.75f, 0.35f, 0.45f)
            : new Color(0.9f, 0.2f, 0.2f, 0.45f);
        string label = LevelAddressableSetup.HasVariant
            ? $"BẢN {LevelAddressableSetup.Variant.ToUpperInvariant()}"
            : "CHƯA CHỌN BUILD PROFILE";

        Rect rect = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, background);
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        GUI.Label(rect, label, style);
    }

    private void DrawInfo()
    {
        string target = LevelAddressableSetup.ActiveBuildTargetName;
        EditorGUILayout.LabelField("Build target", target);
        EditorGUILayout.LabelField("Catalog runtime", LevelAddressableSetup.RuntimeCatalogUrl);
        EditorGUILayout.LabelField("Upload tới", $"{LevelCdn.BucketName}/{LevelAddressableSetup.UploadPrefix}/{target}");
        EditorGUILayout.LabelField("Level bake + upload", $"1..{LevelGlobalConfig.Instance.LevelMaxInConfig} (sửa trong LevelGlobalConfig)");

        if (LevelAddressableSetup.CurrentLayout != LevelAddressableSetup.Layout.App)
        {
            EditorGUILayout.HelpBox(
                "Addressables settings đang ở layout LEVELS (build remote + remote catalog), chỉ hợp lệ trong lúc Build Levels.\n" +
                "Hãy restore ngay để không commit nhầm.",
                MessageType.Error);
            if (GUILayout.Button("Restore layout App", GUILayout.Height(26)))
            {
                LevelAddressableSetup.ApplyLayout(LevelAddressableSetup.Layout.App);
                AssetDatabase.SaveAssets();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Layout", "App (bake toàn bộ level, không remote catalog)");
        }

        bool editorCatalog = EditorPrefs.GetBool(LevelCatalog.EditorEnabledPrefKey, false);
        bool newEditorCatalog = EditorGUILayout.ToggleLeft(
            new GUIContent("Editor play mode nạp catalog CDN",
                "Bật khi muốn thử đúng đường Addressables trong Editor. Tắt (mặc định) thì Play mode đọc thẳng file level trên đĩa."),
            editorCatalog);
        if (newEditorCatalog != editorCatalog) EditorPrefs.SetBool(LevelCatalog.EditorEnabledPrefKey, newEditorCatalog);

        string dir = LevelAddressableSetup.ServerDataDir;
        if (Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0)
        {
            FileInfo[] files = new DirectoryInfo(dir).GetFiles();
            long sizeKb = files.Sum(f => f.Length) / 1024;
            EditorGUILayout.LabelField("ServerData", $"{files.Length} file, {sizeKb} KB, build lúc {Directory.GetLastWriteTime(dir):HH:mm dd/MM}");

            if (!LevelUploadPlan.CanUpload(LevelAddressableSetup.GetBuiltVariant(), LevelAddressableSetup.Variant, out string reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Chưa có ServerData. Bấm 'Build Levels'.", MessageType.Info);
        }
    }

    // =========================================================
    // Cấu hình R2 (rclone): form nhập key, ghi vào rclone.conf
    // =========================================================

    private void DrawR2Config()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string status = rcloneInstalled != true
                ? "rclone: CHƯA CÀI"
                : remoteConfigured == true ? $"rclone: OK, remote '{LevelCdn.RcloneRemote}' sẵn sàng" : $"rclone: đã cài, remote '{LevelCdn.RcloneRemote}' CHƯA cấu hình";
            showR2Config = EditorGUILayout.Foldout(showR2Config, $"Cấu hình R2  ({status})", true);
            if (!showR2Config) return;

            if (rcloneInstalled != true)
            {
                DrawInstallRclone();
                return;
            }

            EditorGUILayout.LabelField($"rclone: {RcloneRunner.RclonePath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Lấy 3 giá trị từ Cloudflare: R2 > Manage R2 API Tokens (token của bucket {LevelCdn.BucketName}).", EditorStyles.miniLabel);
            accountId = EditorGUILayout.TextField("Account ID", accountId);
            accessKey = EditorGUILayout.TextField("Access Key ID", accessKey);
            secretKey = EditorGUILayout.PasswordField("Secret Access Key", secretKey);
            EditorGUILayout.LabelField("Key chỉ được ghi vào rclone.conf của máy này, không lưu vào project hay git.", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey)))
                {
                    if (GUILayout.Button("Lưu cấu hình rclone", GUILayout.Height(26))) SaveRcloneConfig();
                }

                using (new EditorGUI.DisabledScope(remoteConfigured != true))
                {
                    if (GUILayout.Button("Test kết nối bucket", GUILayout.Height(26))) TestRcloneConnection();
                }
            }
        }

        if (remoteConfigured != true)
        {
            EditorGUILayout.HelpBox($"Upload sẽ thất bại cho tới khi remote '{LevelCdn.RcloneRemote}' được cấu hình ở trên.", MessageType.Warning);
        }
    }

    private void DrawInstallRclone()
    {
        bool isWindows = RcloneRunner.IsWindowsEditor;
        string installCommand = isWindows ? "winget install Rclone.Rclone" : "brew install rclone";
        EditorGUILayout.HelpBox(
            "Chưa tìm thấy rclone trên máy.\n" +
            (isWindows
                ? "Bấm 'Cài rclone' bên dưới (mở cửa sổ cmd chạy winget, chờ nó báo xong), "
                : $"Bấm nút bên dưới để copy lệnh '{installCommand}', dán vào Terminal chạy xong, ") +
            "rồi quay lại bấm 'Kiểm tra lại'. Không cần khởi động lại Unity.",
            MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (isWindows)
            {
                if (GUILayout.Button("Cài rclone (winget)", GUILayout.Height(26)))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/k " + installCommand,
                        UseShellExecute = true,
                    });
                }
            }
            else if (GUILayout.Button("Copy lệnh cài (brew)", GUILayout.Height(26)))
            {
                EditorGUIUtility.systemCopyBuffer = installCommand;
                EditorUtility.DisplayDialog("Cài rclone",
                    $"Đã copy lệnh:\n{installCommand}\n\nMở Terminal, dán và chạy. Xong quay lại bấm 'Kiểm tra lại'.", "OK");
            }

            if (GUILayout.Button("Kiểm tra lại", GUILayout.Height(26))) RefreshRcloneStatus();
        }
    }

    private static void RefreshRcloneStatus()
    {
        RcloneRunner.InvalidateCache();
        rcloneInstalled = RcloneRunner.RclonePath != null;
        remoteConfigured = rcloneInstalled == true
                           && RcloneRunner.Run("listremotes", out string remotes)
                           && remotes.Split('\n').Any(line => line.Trim() == LevelCdn.RcloneRemote + ":");
    }

    private void SaveRcloneConfig()
    {
        string id = accountId.Trim();
        string key = accessKey.Trim();
        string secret = secretKey.Trim();

        if (id.Length != 32 &&
            !EditorUtility.DisplayDialog("Cấu hình R2",
                $"Account ID thường là chuỗi hex 32 ký tự, bạn nhập {id.Length} ký tự.\nVẫn lưu?", "Lưu", "Huỷ"))
        {
            return;
        }

        string args = $"config create {LevelCdn.RcloneRemote} s3 provider=Cloudflare access_key_id={key} secret_access_key={secret} " +
                      $"endpoint=https://{id}.r2.cloudflarestorage.com no_check_bucket=true";

        // KHÔNG Debug.Log args hay output của lệnh này: chúng chứa secret key.
        if (RcloneRunner.Run(args, out string output))
        {
            secretKey = "";
            RefreshRcloneStatus();
            EditorUtility.DisplayDialog("Cấu hình R2", $"Đã lưu remote '{LevelCdn.RcloneRemote}' vào rclone.conf.\nBấm 'Test kết nối bucket' để kiểm tra.", "OK");
        }
        else
        {
            string scrubbed = string.IsNullOrEmpty(secret) ? output : output.Replace(secret, "***");
            EditorUtility.DisplayDialog("Cấu hình R2", $"Lưu thất bại:\n{scrubbed}", "OK");
        }
    }

    private void TestRcloneConnection()
    {
        if (RcloneRunner.Run($"lsjson {LevelCdn.RemoteBucket} --max-depth 1", out string output))
        {
            EditorUtility.DisplayDialog("Test R2", $"Kết nối bucket {LevelCdn.BucketName} OK!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Test R2",
                $"Kết nối thất bại:\n{output}\n\nKiểm tra lại Access Key / Secret / Account ID và token có scope đúng bucket không.", "OK");
        }
    }

    // =========================================================
    // Nút chính
    // =========================================================

    private void DrawButtons()
    {
        bool uploading = LevelAddressableSetup.IsUploading;

        using (new EditorGUI.DisabledScope(uploading))
        {
            if (GUILayout.Button("1. Setup  (đánh dấu entry 1..Max, dựng cache độ khó, layout App)", GUILayout.Height(30)))
            {
                EditorApplication.delayCall += RunSetup;
            }

            if (GUILayout.Button("2. Build Levels  (bundle remote + catalog_levels, máy nào cũng build được)", GUILayout.Height(30)))
            {
                EditorApplication.delayCall += () => RunBuild(null);
            }

            GUILayout.Space(6);

            uploadScopeIndex = EditorGUILayout.Popup("Phạm vi upload", uploadScopeIndex, UploadScopeLabels);
            LevelUploadScope scope = (LevelUploadScope)uploadScopeIndex;
            if (scope != LevelUploadScope.All)
            {
                EditorGUILayout.HelpBox(
                    "Catalog luôn upload kèm. Chỉ dùng phạm vi một phần khi phần còn lại KHÔNG thay đổi so với lần upload trước.",
                    MessageType.Warning);
            }

            if (GUILayout.Button($"3. Upload lên R2  (bản {LevelAddressableSetup.Variant})", GUILayout.Height(30)))
            {
                EditorApplication.delayCall += () => RunUpload(scope);
            }

            GUILayout.Space(8);

            GUIStyle accent = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
            if (GUILayout.Button($"Build Levels + Upload\n(bản {LevelAddressableSetup.Variant}, cập nhật level cho mọi bản app)", accent, GUILayout.Height(44)))
            {
                EditorApplication.delayCall += () => RunBuild(() => RunUpload(scope));
            }
        }

        if (uploading)
        {
            EditorGUILayout.HelpBox("Đang upload... theo dõi Console (tag [R2Upload]).", MessageType.Info);
        }
    }

    private static void RunSetup()
    {
        bool ok = LevelAddressableSetup.SetupAll(out string message);
        EditorUtility.DisplayDialog("Setup", (ok ? "Setup xong.\n\n" : "Setup xong nhưng THIẾU LEVEL.\n\n") + message, "OK");
    }

    private static void RunBuild(System.Action onSuccess)
    {
        if (!LevelAddressableSetup.BuildLevels(out string message))
        {
            EditorUtility.DisplayDialog("Build Levels", $"Build lỗi:\n{message}", "OK");
            return;
        }

        if (onSuccess != null) onSuccess();
        else EditorUtility.DisplayDialog("Build Levels", $"Build xong.\n\n{message}", "OK");
    }

    private static void RunUpload(LevelUploadScope scope)
    {
        if (!LevelUploadPlan.CanUpload(LevelAddressableSetup.GetBuiltVariant(), LevelAddressableSetup.Variant, out string reason))
        {
            EditorUtility.DisplayDialog("Upload R2", reason, "OK");
            return;
        }

        LevelAddressableSetup.DescribeUpload(scope, out int fileCount, out long totalBytes);
        string scopeWarning = scope == LevelUploadScope.All
            ? ""
            : "\n\nCatalog luôn upload kèm. Chỉ dùng phạm vi này khi phần còn lại KHÔNG đổi so với lần upload trước.";
        if (!EditorUtility.DisplayDialog($"Upload R2, bản {LevelAddressableSetup.Variant}",
                $"Upload {fileCount} file ({totalBytes / 1024} KB), phạm vi: {LevelUploadPlan.ScopeLabel(scope)}\n\n" +
                $"ServerData/{LevelAddressableSetup.ActiveBuildTargetName}  ->  {LevelCdn.BucketName}/{LevelAddressableSetup.UploadPrefix}/{LevelAddressableSetup.ActiveBuildTargetName}{scopeWarning}",
                $"Upload bản {LevelAddressableSetup.Variant}", "Huỷ"))
        {
            return;
        }

        if (!LevelAddressableSetup.StartUpload(scope, OnUploadFinished, out string message))
        {
            EditorUtility.DisplayDialog("Upload R2", message, "OK");
        }
    }

    private static void OnUploadFinished(bool ok, string message)
    {
        EditorUtility.DisplayDialog("Upload R2", message, "OK");
    }

    // =========================================================
    // Dọn file cũ trên CDN
    // =========================================================

    private void DrawCleanup()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showCleanup = EditorGUILayout.Foldout(showCleanup, "Dọn file cũ trên CDN", true);
            if (!showCleanup) return;

            EditorGUILayout.HelpBox(
                "Upload chỉ thêm file, không xoá. Quét sẽ liệt kê file trên CDN KHÔNG có trong ServerData của lần Build Levels " +
                "hiện tại và cũ hơn số ngày ân hạn (client còn cầm catalog cũ vẫn tải được chunk cũ trong thời gian đó).",
                MessageType.Info);

            cleanupGraceDays = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Ân hạn (ngày)", "Chỉ xoá file cũ hơn số ngày này. 0 = xoá mọi file không còn dùng (không khuyến nghị)."),
                cleanupGraceDays));

            using (new EditorGUI.DisabledScope(LevelAddressableSetup.IsUploading || remoteConfigured != true))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Quét file cũ", GUILayout.Height(26))) ScanStaleRemoteFiles();

                    using (new EditorGUI.DisabledScope(staleFiles == null || staleFiles.Count == 0))
                    {
                        long staleBytes = staleFiles?.Sum(f => f.Size) ?? 0;
                        if (GUILayout.Button($"Xoá {staleFiles?.Count ?? 0} file ({staleBytes / 1024} KB)", GUILayout.Height(26)))
                        {
                            DeleteStaleRemoteFiles();
                        }
                    }
                }
            }

            if (remoteConfigured != true)
            {
                EditorGUILayout.HelpBox($"Cần cấu hình remote '{LevelCdn.RcloneRemote}' ở phần Cấu hình R2 trước.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(cleanupStatus))
            {
                EditorGUILayout.LabelField(cleanupStatus, EditorStyles.wordWrappedMiniLabel);
            }

            if (staleFiles == null || staleFiles.Count == 0) return;

            cleanupScroll = EditorGUILayout.BeginScrollView(cleanupScroll, GUILayout.MaxHeight(160));
            foreach (RemoteFileInfo file in staleFiles)
            {
                EditorGUILayout.LabelField($"{file.Path}  ({file.Size / 1024} KB, {file.ModTime})", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void ScanStaleRemoteFiles()
    {
        staleFiles = null;
        if (!LevelAddressableSetup.TryListRemoteFiles(out List<RemoteFileInfo> remote, out string error))
        {
            cleanupStatus = "Liệt kê thất bại: " + error;
            return;
        }

        List<RemoteFileInfo> stale = LevelAddressableSetup.FindStaleRemoteFiles(remote, cleanupGraceDays, out error);
        if (stale == null)
        {
            cleanupStatus = error;
            return;
        }

        staleFiles = stale;
        cleanupStatus = $"CDN ({LevelAddressableSetup.RemoteDest}) có {remote.Count} file, {remote.Sum(f => f.Size) / 1024} KB. " +
                        $"Không còn dùng và cũ hơn {cleanupGraceDays} ngày: {stale.Count} file, {stale.Sum(f => f.Size) / 1024} KB.";
    }

    private void DeleteStaleRemoteFiles()
    {
        if (staleFiles == null || staleFiles.Count == 0) return;

        long staleBytes = staleFiles.Sum(f => f.Size);
        if (!EditorUtility.DisplayDialog($"Xoá file cũ trên CDN, bản {LevelAddressableSetup.Variant}",
                $"Xoá {staleFiles.Count} file ({staleBytes / 1024} KB) khỏi\n{LevelAddressableSetup.RemoteDest}\n\n" +
                $"Chỉ xoá file không có trong ServerData hiện tại và cũ hơn {cleanupGraceDays} ngày.",
                "Xoá", "Huỷ"))
        {
            return;
        }

        bool ok = LevelAddressableSetup.DeleteRemoteFiles(staleFiles, out string output);
        cleanupStatus = ok
            ? $"Đã xoá {staleFiles.Count} file. Bấm 'Quét file cũ' để kiểm tra lại."
            : "Xoá thất bại, xem Console (tag [R2Cleanup]).";
        if (ok) staleFiles = null;
        EditorUtility.DisplayDialog("Dọn file cũ", ok ? "Xoá xong." : $"Xoá thất bại:\n{output}", "OK");
    }
}
