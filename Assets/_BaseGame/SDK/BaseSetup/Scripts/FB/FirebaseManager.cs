using System;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

namespace SDK {
    public class FirebaseManager : MonoBehaviour {
        public static FirebaseAnalyticsManager FirebaseAnalyticsManager { get; } = new FirebaseAnalyticsManager();
        private static FirebaseRemoteConfigManager FirebaseRemoteConfigManager { get; } = new FirebaseRemoteConfigManager();
        private static FirebaseManager m_Instance;
        public static FirebaseManager Instance => m_Instance;
        
        public static bool IsReady { get; private set; }
        public static FirebaseApp App { get; private set; }


        public FirebaseApp FirebaseApp { get; set; }
        private void Awake() {
            m_Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    App = FirebaseApp.DefaultInstance;
                    IsReady = true;
                    FirebaseLogger.Log("Initialized");
                    // Optional: Trigger your Analytics/Remote Config init here
                    FirebaseAnalyticsManager.Initialize();
                    FirebaseRemoteConfigManager.Initialize();
                }
                else
                {
                    FirebaseLogger.LogError($"Dependencies not available: {status}");
                }
            });
        }

        public void LogFirebaseEvent(string eventName, string eventParameter, double eventValue) {
            if (IsReady) {
                FirebaseAnalyticsManager.LogEvent(eventName, eventParameter, eventValue);
            }
        }
        public void LogFirebaseEvent(string eventName, Parameter[] param) {
            if (IsReady) {
                FirebaseAnalyticsManager.LogEvent(eventName, param);
            }
        }
        public void LogFirebaseEvent(string eventName) {
            if (IsReady) {
                FirebaseAnalyticsManager.LogEvent(eventName);
            }
        }
        public void SetUserProperty(string propertyName, string property) {
            if (IsReady) {
                FirebaseAnalyticsManager.SetUserProperty(propertyName, property);
            }
        }
        public ConfigValue GetConfigValue(string key) {
            return FirebaseRemoteConfigManager.GetValues(key);
        }
        public string GetConfigString(string key)
        {
            return FirebaseRemoteConfigManager.GetValues(key).StringValue;
        }
        public double GetConfigDouble(string key)
        {
            return FirebaseRemoteConfigManager.GetValues(key).DoubleValue;
        }
        public bool GetConfigBool(string key)
        {
            return FirebaseRemoteConfigManager.GetValues(key).BooleanValue;
        }
    }
}

