using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// Chạy các test EditMode có tên bắt đầu bằng "Tumble" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Kết quả in ra Console với tiền tố [TumbleTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class TumbleTestMenu
{
    private class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => Debug.Log("[TumbleTests] start");

        public void RunFinished(ITestResultAdaptor result)
            => Debug.Log($"[TumbleTests] done pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount}");

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            if (result.TestStatus == TestStatus.Passed) Debug.Log($"[TumbleTests] PASS {result.FullName}");
            else Debug.LogError($"[TumbleTests] {result.TestStatus} {result.FullName}\n{result.Message}\n{result.StackTrace}");
        }
    }

    [MenuItem("Tools/FishSort/Run Tumble Tests")]
    public static void Run()
    {
        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = new[] { "^Tumble" } }));
    }
}
