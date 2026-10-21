using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// Chạy các test EditMode có tên bắt đầu bằng "Perf" từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Kết quả in ra Console với tiền tố [PerfTests]; dòng cuối là "done pass=.. fail=.. skip=..".
public static class PerfTestMenu
{
    private class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => Debug.Log("[PerfTests] start");

        public void RunFinished(ITestResultAdaptor result)
            => Debug.Log($"[PerfTests] done pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount}");

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            if (result.TestStatus == TestStatus.Passed) Debug.Log($"[PerfTests] PASS {result.FullName}");
            else Debug.LogError($"[PerfTests] {result.TestStatus} {result.FullName}\n{result.Message}\n{result.StackTrace}");
        }
    }

    [MenuItem("Tools/FishSort/Run Perf Tests")]
    public static void Run()
    {
        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = new[] { "^Perf" } }));
    }
}
