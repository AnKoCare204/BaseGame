using AppsFlyerSDK;
using SDK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

// This class is intended to be used the the AppsFlyerObject.prefab

public class AppsFlyerManualObject : MonoBehaviour, IAppsFlyerConversionData
{

    // These fields are set from the editor so do not modify!
    //******************************//
    public string devKey;
    public string appID;
    public string UWPAppID;
    public string macOSAppID;
    public bool isDebug;
    public bool getConversionData;
    //******************************//

    private void Start()
    {
        // These fields are set from the editor so do not modify!
        //******************************//
        AppsFlyer.setIsDebug(isDebug);
#if UNITY_WSA_10_0 && !UNITY_EDITOR
        AppsFlyer.initSDK(devKey, UWPAppID, getConversionData ? this : null);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    AppsFlyer.initSDK(devKey, macOSAppID, getConversionData ? this : null);
#else
        AppsFlyer.initSDK(devKey, appID, getConversionData ? this : null);
#endif
        //******************************/

        AppsFlyer.startSDK();
    }

    // Mark AppsFlyer CallBacks
    public void onConversionDataSuccess(string conversionData)
    {
        AppsFlyer.AFLog("didReceiveConversionData", conversionData);
        Debug.Log("didReceiveConversionData: " + conversionData);
        Dictionary<string, object> conversionDataDictionary = AppsFlyer.CallbackStringToDictionary(conversionData);
        // add deferred deeplink logic here
    }

    public void onConversionDataFail(string error)
    {
        AppsFlyer.AFLog("didReceiveConversionDataWithError", error);
    }

    public void onAppOpenAttribution(string attributionData)
    {
        AppsFlyer.AFLog("onAppOpenAttribution", attributionData);
        Dictionary<string, object> attributionDataDictionary = AppsFlyer.CallbackStringToDictionary(attributionData);
        // add direct deeplink logic here

    }

    public void onAppOpenAttributionFailure(string error)
    {
        AppsFlyer.AFLog("onAppOpenAttributionFailure", error);
    }
}

internal static class AppsFlyerDeepLinkBridge
{
    // Các khóa phổ biến AppsFlyer trả về cho deeplink
    private static readonly string[] UrlKeys = new[]
    {
        "af_dp",              // đường dẫn đầy đủ
        "af_deeplink",        // boolean hoặc string tùy SDK
        "deep_link_value",    // đôi khi chứa path/action
        "af_sub1"             // fallback tùy setup chiến dịch
    };

    public static string ExtractUrl(IDictionary<string, object> data)
    {
        if (data == null) return null;

        foreach (var key in UrlKeys)
        {
            if (data.TryGetValue(key, out var value) && value != null)
            {
                var s = value.ToString();
                if (!string.IsNullOrEmpty(s) && s.Contains("://"))
                {
                    return s;
                }
            }
        }

        // Trường hợp chiến dịch chỉ gửi action, ta tự build URL theo schema ứng dụng
        if (data.TryGetValue("deep_link_value", out var dlv) && dlv != null)
        {
            var action = dlv.ToString();
            if (!string.IsNullOrEmpty(action))
            {
                var url = $"skewersort://{action}";
                
                // Merge thêm eventId nếu có trong data
                if (data.TryGetValue("eventId", out var eventId) && eventId != null)
                {
                    var eventIdStr = eventId.ToString();
                    if (!string.IsNullOrEmpty(eventIdStr))
                    {
                        url += $"?eventId={eventIdStr}";
                    }
                }
                
                return url;
            }
        }

        return null;
    }
}
