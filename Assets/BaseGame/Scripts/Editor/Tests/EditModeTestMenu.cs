using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEditor;
using UnityEngine;

/// Chạy các test EditMode có tên class bắt đầu bằng classPrefix (rỗng = mọi class *Tests) từ menu (điều khiển editor qua MCP, không mở được Test Runner).
/// Kết quả in ra Console với tiền tố [EditModeTests]; dòng cuối là "done pass=.. fail=.. skip=..".
///
/// KHÔNG đi qua TestRunnerApi: bộ chạy của Unity hỏi "Scene(s) Have Been Modified" mỗi khi scene đang mở có thay đổi chưa
/// lưu, và hộp thoại đó chặn cứng editor khi điều khiển từ xa. Ở đây gọi thẳng các hàm [Test] / [TestCase] trên main
/// thread, có [SetUp] / [TearDown]. Các test này vẫn chạy bình thường trong cửa sổ Test Runner.
///
/// Bộ chạy này chỉ hiểu [Test] và [TestCase] trên hàm public trả về void. Gặp thứ nó không chạy đúng được
/// (TestCaseSource, ExpectedResult, UnityTest, hàm không public...) thì TÍNH LÀ FAIL chứ không lặng lẽ bỏ qua.
/// Test in ra LogError / exception ngoài ý muốn, hoặc TearDown lỗi, cũng tính là fail.
public static class EditModeTestMenu
{
    private const string Tag = "[EditModeTests]";
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private enum TestStatusLite
    {
        Passed,
        Failed,
        Skipped,
    }

    [MenuItem("Tools/Game/Run EditMode Tests")]
    public static void Run() => RunTests(string.Empty, Tag);

    /// <summary>Chạy mọi class test có tên bắt đầu bằng classPrefix và kết thúc bằng "Tests", log với tiền tố tag.</summary>
    public static void RunTests(string classPrefix, string tag)
    {
        Debug.Log($"{tag} start");
        int pass = 0, fail = 0, skip = 0;

        try
        {
            // Assert của NUnit cần một TestExecutionContext hiện hành để đếm số assert.
            new TestExecutionContext().EstablishExecutionEnvironment();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{tag} không dựng được TestExecutionContext: {e.Message}");
        }

        IEnumerable<Type> types = typeof(EditModeTestMenu).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested && t.Name.StartsWith(classPrefix) && t.Name.EndsWith("Tests"))
            .OrderBy(t => t.Name);

        foreach (Type type in types)
        {
            MethodInfo[] methods = type.GetMethods(AllInstance);
            MethodInfo[] setUps = methods.Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToArray();
            MethodInfo[] tearDowns = methods.Where(m => m.GetCustomAttribute<TearDownAttribute>() != null).ToArray();

            string fixtureProblem = DescribeUnsupportedFixture(type, setUps, tearDowns);
            if (fixtureProblem != null)
            {
                fail++;
                Debug.LogError($"{tag} Failed {type.Name}: bộ chạy không chạy đúng được class này: {fixtureProblem}");
                continue;
            }

            foreach (MethodInfo method in methods.OrderBy(m => m.Name))
            {
                string problem = DescribeUnsupported(method);
                if (problem != null)
                {
                    fail++;
                    Debug.LogError($"{tag} Failed {type.Name}.{method.Name}: bộ chạy không chạy đúng được test này: {problem}");
                    continue;
                }

                foreach (KeyValuePair<string, object[]> testCase in CollectCases(type, method))
                {
                    switch (RunCase(tag, type, method, setUps, tearDowns, testCase.Key, testCase.Value))
                    {
                        case TestStatusLite.Passed: pass++; break;
                        case TestStatusLite.Skipped: skip++; break;
                        default: fail++; break;
                    }
                }
            }
        }

        Debug.Log($"{tag} done pass={pass} fail={fail} skip={skip}");
    }

    /// <summary>
    /// null khi bộ chạy này chạy đúng được hàm (hoặc hàm không phải test). Ngược lại trả lý do, và hàm đó bị tính là fail:
    /// một test bị bỏ qua trong im lặng là một kết quả xanh giả.
    /// </summary>
    public static string DescribeUnsupported(MethodInfo method)
    {
        if (method == null) return null;

        Attribute[] builders = method.GetCustomAttributes()
            .Where(a => a is ITestBuilder || a is ISimpleTestBuilder)
            .ToArray();
        if (builders.Length == 0) return null;

        foreach (Attribute builder in builders)
        {
            Type type = builder.GetType();
            if (type != typeof(TestAttribute) && type != typeof(TestCaseAttribute))
            {
                return $"attribute {type.Name} không được hỗ trợ";
            }
        }

        if (!method.IsPublic) return "hàm test không public";
        if (method.ReturnType != typeof(void)) return $"hàm test trả về {method.ReturnType.Name}, bộ chạy chỉ hiểu void";

        int parameterCount = method.GetParameters().Length;
        bool isPlainTest = method.GetCustomAttribute<TestAttribute>() != null;
        if (isPlainTest && parameterCount > 0) return "[Test] có tham số";

        foreach (TestCaseAttribute testCase in method.GetCustomAttributes<TestCaseAttribute>())
        {
            if (testCase.HasExpectedResult) return "[TestCase] dùng ExpectedResult";
            object[] arguments = testCase.Arguments ?? new object[] { null };
            if (arguments.Length != parameterCount) return $"[TestCase] có {arguments.Length} đối số cho {parameterCount} tham số";
        }

        return null;
    }

    private static string DescribeUnsupportedFixture(Type type, MethodInfo[] setUps, MethodInfo[] tearDowns)
    {
        foreach (MethodInfo method in setUps.Concat(tearDowns))
        {
            if (!method.IsPublic) return $"{method.Name} ([SetUp] / [TearDown]) không public";
            if (method.GetParameters().Length > 0 || method.ReturnType != typeof(void)) return $"{method.Name} ([SetUp] / [TearDown]) phải là void không tham số";
        }

        foreach (MethodInfo method in type.GetMethods(AllInstance | BindingFlags.Static))
        {
            if (method.GetCustomAttribute<OneTimeSetUpAttribute>() != null || method.GetCustomAttribute<OneTimeTearDownAttribute>() != null)
            {
                return $"{method.Name} dùng [OneTimeSetUp] / [OneTimeTearDown]";
            }
        }

        return null;
    }

    private static List<KeyValuePair<string, object[]>> CollectCases(Type type, MethodInfo method)
    {
        var cases = new List<KeyValuePair<string, object[]>>();
        if (method.GetCustomAttribute<TestAttribute>() != null)
        {
            cases.Add(new KeyValuePair<string, object[]>($"{type.Name}.{method.Name}", new object[0]));
        }

        foreach (TestCaseAttribute testCase in method.GetCustomAttributes<TestCaseAttribute>())
        {
            object[] arguments = testCase.Arguments ?? new object[] { null };
            string name = !string.IsNullOrEmpty(testCase.TestName)
                ? testCase.TestName
                : $"{method.Name}({string.Join(", ", arguments.Select(a => a == null ? "null" : a.ToString()))})";
            cases.Add(new KeyValuePair<string, object[]>($"{type.Name}.{name}", arguments));
        }

        return cases;
    }

    private static TestStatusLite RunCase(string tag, Type type, MethodInfo method, MethodInfo[] setUps, MethodInfo[] tearDowns,
        string name, object[] arguments)
    {
        TestStatusLite status = TestStatusLite.Passed;
        string failure = null;
        List<string> loggedErrors = new List<string>();

        void OnLog(string condition, string stackTrace, LogType logType)
        {
            if (logType == LogType.Error || logType == LogType.Exception || logType == LogType.Assert) loggedErrors.Add(condition);
        }

        object instance = Activator.CreateInstance(type);
        Application.logMessageReceived += OnLog;
        try
        {
            foreach (MethodInfo setUp in setUps) setUp.Invoke(instance, null);
            method.Invoke(instance, arguments);
        }
        catch (Exception e)
        {
            Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            if (inner is IgnoreException || inner is InconclusiveException)
            {
                status = TestStatusLite.Skipped;
                failure = inner.Message;
            }
            else if (!(inner is SuccessException))
            {
                status = TestStatusLite.Failed;
                failure = $"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}";
            }
        }
        finally
        {
            foreach (MethodInfo tearDown in tearDowns)
            {
                try
                {
                    tearDown.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    if (status != TestStatusLite.Failed)
                    {
                        status = TestStatusLite.Failed;
                        failure = $"TearDown lỗi: {inner.GetType().Name}: {inner.Message}";
                    }
                }
            }

            Application.logMessageReceived -= OnLog;
        }

        if (status == TestStatusLite.Passed && loggedErrors.Count > 0)
        {
            status = TestStatusLite.Failed;
            failure = $"test in ra {loggedErrors.Count} lỗi ngoài ý muốn: {loggedErrors[0]}";
        }

        switch (status)
        {
            case TestStatusLite.Passed:
                Debug.Log($"{tag} PASS {name}");
                break;
            case TestStatusLite.Skipped:
                Debug.LogWarning($"{tag} Skipped {name}\n{failure}");
                break;
            default:
                Debug.LogError($"{tag} Failed {name}\n{failure}");
                break;
        }

        return status;
    }
}
