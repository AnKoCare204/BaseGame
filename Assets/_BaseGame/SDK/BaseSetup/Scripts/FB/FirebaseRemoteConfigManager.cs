using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.RemoteConfig;
using Firebase.Extensions;
using UnityEngine;

namespace SDK
{
    public class FirebaseRemoteConfigManager
    {
        public static bool IsReady { get; private set; }
        public static Action OnFirebaseRemoteConfigReady { get; set; }
        public void Initialize()
        {
            Dictionary<string, object> defaults =
                new Dictionary<string, object>
                {
                    { Keys.key_inter_show_level, 6 },
                };

            FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            remoteConfig.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
            {
                FetchDataAsync();
            });
        }

        public Task FetchDataAsync()
        {
            FirebaseLogger.Log("Fetching data...");
            Debug.Log("Fetching data...");
            Task fetchTask =
                FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
            return fetchTask.ContinueWithOnMainThread(FetchComplete);
        }

        private void FetchComplete(Task fetchTask)
        {
            if (!fetchTask.IsCompleted)
            {
                FirebaseLogger.LogError("Retrieval hasn't finished.");
                Debug.LogError("Retrieval hasn't finished.");
                return;
            }

            FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            ConfigInfo info = remoteConfig.Info;
            if (info.LastFetchStatus != LastFetchStatus.Success)
            {
                FirebaseLogger.LogError(
                    $"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
                Debug.LogError($"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
                return;
            }

            // Fetch successful. Parameter values must be activated to use.
            remoteConfig.ActivateAsync()
                .ContinueWithOnMainThread(task =>
                {
                    FirebaseLogger.Log($"Remote data loaded and ready for use. Last fetch time {info.FetchTime}.");
                    OnFirebaseRemoteConfigReady?.Invoke();
                    IsReady = true;
                    Debug.Log("FirebaseRemoteConfigManager.FetchComplete: " + IsReady);
                });
        }

        public ConfigValue GetValues(string key)
        {
            return FirebaseRemoteConfig.DefaultInstance.GetValue(key);
        }

        // public void FetchRemoteConfig(System.Action onFetchAndActivateSuccessful)
        // {
        //     if (ABIFirebaseManager.Instance.FirebaseApp == null)
        //     {
        //         return;
        //     }
        //
        //     SDKDebugLogger.Log("Fetching data...");
        //     FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        //     remoteConfig.FetchAsync(System.TimeSpan.Zero).ContinueWithOnMainThread(previousTask =>
        //     {
        //         if (!previousTask.IsCompleted)
        //         {
        //             SDKDebugLogger.LogError(
        //                 $"{nameof(remoteConfig.FetchAsync)} incomplete: Status '{previousTask.Status}'");
        //             return;
        //         }
        //
        //         ActivateRetrievedRemoteConfigValues(onFetchAndActivateSuccessful);
        //     });
        // }
        //
        // private void ActivateRetrievedRemoteConfigValues(System.Action onFetchAndActivateSuccessful)
        // {
        //     FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        //     ConfigInfo info = remoteConfig.Info;
        //     if (info.LastFetchStatus == LastFetchStatus.Success)
        //     {
        //         remoteConfig.ActivateAsync().ContinueWithOnMainThread(previousTask =>
        //         {
        //             SDKDebugLogger.Log($"Remote data loaded and ready (last fetch time {info.FetchTime}).");
        //             onFetchAndActivateSuccessful();
        //         });
        //     }
        // }
    }
}