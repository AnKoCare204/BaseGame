using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Tìm và chạy rclone từ Editor. Hai luồng stdout / stderr được đọc bất đồng bộ: đọc tuần tự (ReadToEnd stdout rồi mới
/// tới stderr) sẽ treo khi tiến trình in nhiều ra stderr, vì pipe stderr đầy trong lúc ta còn chờ stdout.
/// </summary>
public static class RcloneRunner
{
    private static string pathCache;

    public static bool IsWindowsEditor => Application.platform == RuntimePlatform.WindowsEditor;

    /// <summary>Đường dẫn rclone đã tìm được; null khi máy chưa cài.</summary>
    public static string RclonePath
    {
        get
        {
            if (string.IsNullOrEmpty(pathCache)) pathCache = ResolveRclonePath();
            return pathCache;
        }
    }

    public static void InvalidateCache() => pathCache = null;

    /// <summary>Chạy rclone, gộp stdout và stderr vào output.</summary>
    public static bool Run(string args, out string output, int timeoutMs = 20000)
    {
        bool ok = RunSplit(args, out string stdout, out string stderr, timeoutMs);
        output = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}".Trim();
        return ok;
    }

    /// <summary>Như Run nhưng tách stdout / stderr (stdout cần nguyên vẹn khi parse JSON của lsjson).</summary>
    public static bool RunSplit(string args, out string stdout, out string stderr, int timeoutMs = 20000)
    {
        string exe = RclonePath;
        if (exe == null)
        {
            stdout = "";
            stderr = "Chưa tìm thấy rclone trên máy.";
            return false;
        }

        return TryRunProcess(exe, args, timeoutMs, out stdout, out stderr);
    }

    public static bool TryRunProcess(string fileName, string args, int timeoutMs, out string stdout, out string stderr)
    {
        StringBuilder outBuffer = new StringBuilder();
        StringBuilder errBuffer = new StringBuilder();
        try
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (outBuffer) outBuffer.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (errBuffer) errBuffer.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // tiến trình vừa tự thoát
                    }

                    lock (outBuffer) stdout = outBuffer.ToString();
                    stderr = "Timeout.";
                    return false;
                }

                // WaitForExit có timeout trả về khi tiến trình thoát, nhưng hai luồng đọc có thể chưa xả hết; bản không tham số đợi nốt.
                process.WaitForExit();
                lock (outBuffer) stdout = outBuffer.ToString();
                lock (errBuffer) stderr = errBuffer.ToString();
                return process.ExitCode == 0;
            }
        }
        catch (Exception e)
        {
            stdout = "";
            stderr = e.Message;
            return false;
        }
    }

    // Unity mở từ GUI thường không có PATH đầy đủ (Windows: PATH cập nhật sau khi Editor mở; macOS: app GUI không nhận
    // PATH của Homebrew), nên dò thêm các vị trí cài phổ biến theo OS.
    private static string ResolveRclonePath()
    {
        if (TryRunProcess("rclone", "version", 10000, out _, out _)) return "rclone";

        List<string> candidates = new List<string>();
        if (IsWindowsEditor)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "rclone.exe"));
            candidates.Add("C:/Program Files/rclone/rclone.exe");
            candidates.Add("C:/ProgramData/chocolatey/bin/rclone.exe");
            candidates.Add(Path.Combine(userProfile, "scoop", "shims", "rclone.exe"));

            string wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetPackages))
            {
                foreach (string dir in Directory.GetDirectories(wingetPackages, "Rclone.Rclone*"))
                {
                    candidates.AddRange(Directory.GetFiles(dir, "rclone.exe", SearchOption.AllDirectories));
                }
            }
        }
        else
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            candidates.Add("/opt/homebrew/bin/rclone");
            candidates.Add("/usr/local/bin/rclone");
            candidates.Add("/usr/bin/rclone");
            candidates.Add(Path.Combine(home, "bin", "rclone"));
        }

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
