using System;
using UnityEngine;
using System.Collections.Generic;
using R3;
#if R3_TRIGGER
using R3.Triggers;
#endif

namespace SDK {
    public class EventManager : MonoBehaviour 
    {
        private static readonly Dictionary<string, Action> EventDictionary = new();
        private static readonly Stack<Action> EventStack = new();

        private void Start()
        {
#if R3_TRIGGER
            this.UpdateAsObservable().Subscribe(ExecuteNextFrameEvents).AddTo(this);
#else
            Debug.Log("R3_TRIGGER is not defined, please install R3 package com.cysharp.r3 to use EventManager");
#endif
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
            EventStack.Push(listener);
        }

        private static void ExecuteNextFrameEvents(Unit _)
        {
            while (EventStack.Count > 0)
            {
                Action thisEvent = EventStack.Pop();
                thisEvent?.Invoke();
            }
        }
        public static void ClearEvents()
        {
            EventDictionary.Clear();
            EventStack.Clear();
        }
    }
}
