using System;
using System.Collections.Generic;
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
                    EventTrackingManager.TrackEventFirebase += LogFirebaseEvent;
                    UserPropertyManager.SetUserPropertyFirebase += SetUserProperty;
                    FirebaseAnalyticsManager.Initialize();
                    FirebaseRemoteConfigManager.Initialize();
                    // Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
                    // Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
                }
                else
                {
                    FirebaseLogger.LogError($"Dependencies not available: {status}");
                }
            });
        }

        public void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
        {
            FirebaseLogger.Log("Received Registration Token: " + token.Token);
        }

        public void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs message)
        {
            FirebaseLogger.Log("Received a new message from: " + message.Message.From);
        }


        public void LogFirebaseEvent(string eventName, string eventParameter, double eventValue)
        {
            if (IsReady)
            {
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
        public void LogFirebaseEvent(string eventName, params AnalyticsParameter[] parameters) {
            if (!IsReady) {
                return;
            }

            if (parameters == null || parameters.Length == 0) {
                FirebaseAnalyticsManager.LogEvent(eventName);
                return;
            }

            var firebaseParameters = new List<Parameter>(parameters.Length);
            foreach (var parameter in parameters) {
                if (parameter == null || string.IsNullOrEmpty(parameter.Name)) {
                    continue;
                }

                switch (parameter.ValueType) {
                    case 0:
                        firebaseParameters.Add(new Parameter(parameter.Name, parameter.StringValue ?? string.Empty));
                        break;
                    case 1:
                        firebaseParameters.Add(new Parameter(parameter.Name, parameter.LongValue));
                        break;
                    case 2:
                        firebaseParameters.Add(new Parameter(parameter.Name, parameter.DoubleValue));
                        break;
                    default:
                        firebaseParameters.Add(new Parameter(parameter.Name, parameter.StringValue ?? string.Empty));
                        break;
                }
            }

            if (firebaseParameters.Count == 0) {
                FirebaseAnalyticsManager.LogEvent(eventName);
                return;
            }

            FirebaseAnalyticsManager.LogEvent(eventName, firebaseParameters.ToArray());
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

