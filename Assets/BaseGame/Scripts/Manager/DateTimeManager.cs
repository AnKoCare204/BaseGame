using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

public class DateTimeManager : Singleton<DateTimeManager>
{
    private static DateTime ServerTimeAtSync { get; set; }
    private static float LocalTimeAtSync { get; set; }
    public static bool IsSynced { get; set; }
    public static bool IsLocalSynced { get; set; }
    public static int DayOffline { get; set; }

#if UNITY_EDITOR
    [ShowInInspector] private static bool IsSyncedDebug => IsSynced;
    [ShowInInspector] private static bool IsLocalSyncedDebug => IsLocalSynced;
    [ShowInInspector] private static string DateTimeNow => UtcNow.ToString(CultureInfo.InvariantCulture);
    [ShowInInspector] private static int DayOfflineDebug => DayOffline;

#endif
#if CHEAT_ONLY
    private static int TimeAdd { get; set; }
    [field: SerializeField] public int TimeAddEditorDay { get; set; }
    [field: SerializeField] public int TimeAddEditorHour { get; set; }
    [field: SerializeField] public int TimeAddEditorMinute { get; set; }
    [field: SerializeField] public int TimeAddEditorSecond { get; set; }

    protected override void Awake()
    {
        base.Awake();
        TimeAdd =
            TimeAddEditorDay * 86400 + TimeAddEditorHour * 3600 + TimeAddEditorMinute * 60 + TimeAddEditorSecond;
    }

    private void Update()
    {
        TimeAdd =
            TimeAddEditorDay * 86400 + TimeAddEditorHour * 3600 + TimeAddEditorMinute * 60 + TimeAddEditorSecond;
    }
#endif
    public static DateTime UtcNow
    {
        get
        {
#if CHEAT_ONLY
            return DateTime.UtcNow.AddSeconds(TimeAdd);
#endif
            if (!IsSynced)
            {
                return DateTime.UtcNow;
            }

            float elapsed = Time.realtimeSinceStartup - LocalTimeAtSync;
            return ServerTimeAtSync.AddSeconds(elapsed);
        }
    }

    public static async UniTask TrySyncWithServerAsync()
    {
        int maxRetries = 5;
        while (!IsSynced)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: Instance.GetCancellationTokenOnDestroy());
            if (maxRetries > 0)
            {
                maxRetries--;
                if (maxRetries == 4)
                {
                    SyncWithLocal(DateTime.UtcNow);
                }

                if (maxRetries == 0)
                {
                    return;
                }
            }
        }
    }

    public static async UniTask TryGetTimeFromGoogle()
    {
        try
        {
            DateTime googleTime = await GoogleTime.GetUtcTime();
            SyncWithServer(googleTime);
        }
        catch (Exception e)
        {
            
        }
    }

    private static void SyncWithServer(DateTime serverTime)
    {
        if (IsSynced) return;
        ServerTimeAtSync = serverTime;
        LocalTimeAtSync = Time.realtimeSinceStartup;
        IsSynced = true;
    }
    private static void SyncWithLocal(DateTime localTime)
    {
        ServerTimeAtSync = localTime;
        LocalTimeAtSync = Time.realtimeSinceStartup;
        IsLocalSynced = true;
    }

    // Cho phép đồng bộ từ chuỗi UTC (ví dụ lấy từ External Login)
    public static void SyncWithServerUtcString(string serverTimeUtc)
    {
        if (string.IsNullOrEmpty(serverTimeUtc)) return;
        if (DateTime.TryParse(serverTimeUtc, null, DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            SyncWithServer(parsed);
        }
        else
        {
        }
    }
    
}