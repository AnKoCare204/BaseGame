using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public class ActionExecuteRunner : MonoBehaviour
{
    private static readonly Queue<IExecuteAble> ExecuteAbleQueue = new Queue<IExecuteAble>();
#if UNITY_EDITOR
    [ShowInInspector] private List<string> EditorOnlyList => ExecuteAbleQueue.Select(x => x.GetType().Name).ToList();

    [UnityEditor.InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (FindObjectOfType<ActionExecuteRunner>() == null)
        {
            GameObject go = new GameObject("ActionExecuteRunner");
            go.AddComponent<ActionExecuteRunner>();
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    ExecuteAbleQueue.Clear();
                }
            };
        }
    }


#endif
    public static void AddExecuteAble(IExecuteAble executeAble)
    {
        ExecuteAbleQueue.Enqueue(executeAble);
    }

    private void Start()
    {
        Execute().Forget();
    }

    private async UniTask Execute()
    {
        UniTaskCancelableAsyncEnumerable<AsyncUnit> asyncEnumerable = UniTaskAsyncEnumerable.EveryUpdate()
            .WithCancellation(this.GetCancellationTokenOnDestroy());
        await foreach (AsyncUnit _ in asyncEnumerable)
        {
            int priority = -1;
            while (ExecuteAbleQueue.Count > 0)
            {
                IExecuteAble executeAble = ExecuteAbleQueue.Dequeue();
                if (executeAble.Priority <= priority) continue;
                try
                {
                    await executeAble.Execute();
                    priority = executeAble.Priority;
                }
                catch (Exception e)
                {
                    Debug.Log("ActionExecuteRunner execution cancelled");
                }

                // Debug.Log("Executed Completed");
            }
        }
    }
}

public interface IExecuteAble
{
    public int Priority { get; }
    public UniTask Execute();
}

public class PlayerAction : IExecuteAble
{
    public int Priority { get; set; }
    private Action Action { get; set; }

    public PlayerAction(int priority, Action action)
    {
        Priority = priority;
        Action = action;
    }

    public UniTask Execute()
    {
        Action.Invoke();
        return UniTask.CompletedTask;
    }
}

public class PlayerToggleAction : IExecuteAble
{
    public int Priority { get; set; }
    private Action<bool> Action { get; set; }
    private bool Value { get; set; }

    public PlayerToggleAction(int priority, Action<bool> action, bool value)
    {
        Priority = priority;
        Action = action;
        Value = value;
    }

    public UniTask Execute()
    {
        Action.Invoke(Value);
        return UniTask.CompletedTask;
    }
}

public class PlayerTask : IExecuteAble
{
    public int Priority { get; set; }
    private Func<UniTask> Task { get; set; }

    public PlayerTask(int priority, Func<UniTask> task)
    {
        Priority = priority;
        Task = task;
    }

    public async UniTask Execute()
    {
        await Task.Invoke();
    }
}