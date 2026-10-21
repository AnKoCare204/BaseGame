using AppsFlyerSDK;
using SDK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;

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

    public event Action<string> ConversionDataSucceeded;

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

        if (!PlayerPrefs.HasKey("af_first_open_sent"))
        {
            AppsFlyerManager.TrackFirstOpen();
            PlayerPrefs.SetInt("af_first_open_sent", 1);
            PlayerPrefs.Save();
        }
    }

    // Mark AppsFlyer CallBacks
    public void onConversionDataSuccess(string conversionData)
    {
        AppsFlyer.AFLog("didReceiveConversionData", conversionData);
        Debug.Log("didReceiveConversionData: " + conversionData);
        Dictionary<string, object> conversionDataDictionary = AppsFlyer.CallbackStringToDictionary(conversionData);
        // add deferred deeplink logic here

        try
        {
            var status = "";
            if (conversionDataDictionary.ContainsKey("af_status"))
            {
                status = conversionDataDictionary["af_status"].ToString();
            }
            var campaign = "";
            if (conversionDataDictionary.ContainsKey("campaign"))
            {
                campaign = conversionDataDictionary["campaign"].ToString();
            }
            if (campaign != null && campaign.Length > 0)
            {

            }
            var campaignID = "";
            if (conversionDataDictionary.ContainsKey("campaign_id"))
            {
                campaignID = conversionDataDictionary["campaign_id"].ToString();
            }
            if (campaign.Length > 0)
            {
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("camp_name", campaign);
            }
            if (campaignID.Length > 0)
            {
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("camp_id", campaignID);
            }

            if (status.Length > 0)
            {
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("user_source", status);
            }
            bool isFirstLaunch = false;
            // save to player pref
            if (campaign.Length > 0)
            {
                isFirstLaunch = string.IsNullOrEmpty(PlayerPrefs.GetString("af_campaign", ""));
                PlayerPrefs.SetString("af_campaign", campaign);
                //TODO: DEFAULT CONFIG
                //DefaultGlobalConfig.Instance.CampaignName = campaign;
                AppsFlyerManager.TrackGetCampname();
            }
            if (campaignID.Length > 0)
            {
                PlayerPrefs.SetString("af_campaignID", campaignID);
                //TODO: DEFAULT CONFIG
                //DefaultGlobalConfig.Instance.CampaignID = campaignID;
            }

            if (status.Length > 0)
            {
                PlayerPrefs.SetString("af_status", status);
            }
            PlayerPrefs.Save();
            if (isFirstLaunch)
            {
                //TODO: DEFAULT CONFIG
                //DefaultGlobalConfig.Instance.ReFetchRemote().Forget();
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            ConversionDataSucceeded?.Invoke(conversionData);
        }
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
