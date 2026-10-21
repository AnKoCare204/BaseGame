using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

/// RcloneRunner phải đọc stdout và stderr song song: tiến trình in nhiều ra stderr không được làm treo editor,
/// và timeout phải có tác dụng thật.
public class LevelAddrRcloneRunnerTests
{
    private static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;

    [Test]
    public void BigStderr_DoesNotDeadlock()
    {
        string file = IsWindows ? "cmd.exe" : "/bin/sh";
        string line = new string('x', 60);
        string args = IsWindows
            ? $"/c \"for /L %i in (1,1,4000) do @echo {line} 1>&2\""
            : $"-c \"i=0; while [ $i -lt 4000 ]; do echo {line} 1>&2; i=$((i+1)); done\"";

        Stopwatch watch = Stopwatch.StartNew();
        bool ok = RcloneRunner.TryRunProcess(file, args, 30000, out string stdout, out string stderr);
        watch.Stop();

        Assert.IsTrue(ok, "tiến trình phải thoát với mã 0");
        Assert.Greater(stderr.Length, 200000, "phải đọc được hết phần stderr (khoảng 248 KB)");
        Assert.Less(watch.ElapsedMilliseconds, 30000, "không được chạm timeout");
    }

    [Test]
    public void Timeout_KillsProcessAndReturnsFalse()
    {
        string file = IsWindows ? "ping" : "/bin/sleep";
        string args = IsWindows ? "-n 10 127.0.0.1" : "10";

        Stopwatch watch = Stopwatch.StartNew();
        bool ok = RcloneRunner.TryRunProcess(file, args, 500, out string stdout, out string stderr);
        watch.Stop();

        Assert.IsFalse(ok);
        Assert.AreEqual("Timeout.", stderr);
        Assert.Less(watch.ElapsedMilliseconds, 5000, "phải trả về ngay sau khi hết giờ, không đợi tiến trình chạy xong");
    }

    [Test]
    public void MissingExecutable_ReturnsFalseWithMessage()
    {
        bool ok = RcloneRunner.TryRunProcess("definitely-not-a-real-binary-xyz", "", 2000, out string stdout, out string stderr);
        Assert.IsFalse(ok);
        Assert.IsNotEmpty(stderr);
    }

    [Test]
    public void ExitCodeNonZero_ReturnsFalse()
    {
        string file = IsWindows ? "cmd.exe" : "/bin/sh";
        string args = IsWindows ? "/c exit 3" : "-c \"exit 3\"";
        bool ok = RcloneRunner.TryRunProcess(file, args, 5000, out string stdout, out string stderr);
        Assert.IsFalse(ok);
    }
}
