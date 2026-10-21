using System;
using System.Collections.Generic;
using R3;
using R3.Triggers;
using TW.Utility.DesignPattern;
using UnityEngine;


public class EventManager : Singleton<EventManager> 
{
    private static readonly Dictionary<string, Action> EventDictionary = new();
    private static readonly Queue<Action> CurrentFrameQueue = new();
    private static readonly Queue<Action> NextFrameQueue = new();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (FindAnyObjectByType<EventManager>() == null)
        {
            GameObject go = new GameObject("EventManager");
            go.AddComponent<EventManager>();
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    CurrentFrameQueue.Clear();
                    NextFrameQueue.Clear();
                    EventDictionary.Clear();
                }
            };
        }
    }
#endif
    private void Start()
    {
        this.UpdateAsObservable().Subscribe(ExecuteNextFrameEvents).AddTo(this);
    }

    public static void StartListening(string eventName, Action listener)
    {
        if (EventDictionary.TryGetValue(eventName, out Action thisEvent))
        {
            thisEvent += listener;
            EventDictionary[eventName] = thisEvent;
        }
        else
        {
            EventDictionary[eventName] = listener;
        }
    }
    public static void StopListening(string eventName, Action listener)
    {
        if (!EventDictionary.TryGetValue(eventName, out Action thisEvent)) return;
        thisEvent -= listener;
        if (thisEvent == null)
        {
            EventDictionary.Remove(eventName);
        }
        else
        {
            EventDictionary[eventName] = thisEvent;
        }
    }
    public static void TriggerEvent(string eventName)
    {
        if (EventDictionary.TryGetValue(eventName, out Action thisEvent))
        {
            thisEvent.Invoke();
        }
    }
    public static void AddEventNextFrame(Action listener)
    {
        // Ensure the singleton exists so queued events are actually executed next frame
        _ = Instance;
        NextFrameQueue.Enqueue(listener);
    }

    private static void ExecuteNextFrameEvents(Unit _)
    {
        while (CurrentFrameQueue.Count > 0)
        {
            Action thisEvent = CurrentFrameQueue.Dequeue();
            thisEvent?.Invoke();
        }

        while (NextFrameQueue.Count > 0)
        {
            CurrentFrameQueue.Enqueue(NextFrameQueue.Dequeue());
        }
    }
    public static void ClearEvents()
    {
        EventDictionary.Clear();
        CurrentFrameQueue.Clear();
        NextFrameQueue.Clear();
    }
}