using System.Collections.Generic;
using System.Globalization;
using System;
using UnityEngine;


// #if UNITY_APPSFLYER
using AppsFlyerSDK;
// #endif

namespace SDK
{
// #if UNITY_APPSFLYER
    public static class AppsFlyerManager
    {
        #region Platform Mapping
        #endregion
        public static void SendEvent(string eventName, Dictionary<string, string> pairs)
        {
            AppsFlyer.sendEvent(eventName, pairs);
        }
        public static void SendEvent(string eventName)
        {
            SDKDebugLogger.Log("Appsflyer call send event " + eventName);
            AppsFlyer.sendEvent(eventName, new Dictionary<string, string>());
        }

        #region Tracking
        public const string af_inters_logicgame = "af_inters_logicgame";
        public const string af_inters_successfullyloaded = "af_inters_successfullyloaded";
        public const string af_inters_displayed = "af_inters_displayed";

        public const string af_inters_show_count = "af_inters_show_count_";

        public const string af_rewarded_logicgame = "af_rewarded_logicgame";
        public const string af_rewarded_successfullyloaded = "af_rewarded_successfullyloaded";
        public const string af_rewarded_displayed = "af_rewarded_displayed";

        public const string af_ad_impression = "af_ad_impression_abi";

        public const string af_rewarded_show_count = "af_rewarded_show_count_";

        public const string af_completed_level = "completed_level_";
        public const string af_first_open = "af_first_open";
        public const string af_level_start = "af_level_start";
        public const string af_level_achieved = "af_level_achieved";


        /// <summary>
        /// 
        /// </summary>
        public static void TrackInterstitial_ClickShowButton()
        {
            SendEvent(af_inters_logicgame);
        }
        public static void TrackInterstitial_LoadedSuccess()
        {
            SendEvent(af_inters_successfullyloaded);
        }
        public static void TrackInterstitial_Displayed()
        {
            SendEvent(af_inters_displayed);
        }
        public static void TrackInterstitial_ShowCount(int total) {
            SDKDebugLogger.Log("TrackInterstitial_ShowCount " + total);
            if (total == 0) return;
            if (total <= 20)
            {
                string eventName = af_inters_show_count + total;
                SendEvent(eventName);
            }
        }
        public static void TrackRewarded_ClickShowButton()
        {
            SendEvent(af_rewarded_logicgame);
        }
        public static void TrackRewarded_LoadedSuccess()
        {
            SendEvent(af_rewarded_successfullyloaded);
        }
        public static void TrackRewarded_Displayed()
        {
            SendEvent(af_rewarded_displayed);
        }

        public static void TrackFirstOpen()
        {
            SendEvent(af_first_open); 
        }
        public static void TrackLevelStart(int level)
        {
            string eventName = af_level_start;
            SendEvent(eventName, new Dictionary<string, string> { { "level", level.ToString() } });
        }
        public static void TrackLevelAchieved(int level)
        {
            string eventName = af_level_achieved;
            SendEvent(eventName, new Dictionary<string, string> { { "level", level.ToString() } });
        }
        public static void TrackAdImpression(Dictionary<string, string> eventValue)
        {
            SendEvent(af_ad_impression, eventValue);
        }
        public static void TrackRewarded_ShowCount(int total)
        {
            if (total == 0) return;
            // bool isTracking = total % 5 == 0;
            //
            // if (!isTracking) return;
            // string eventName = af_rewarded_show_count + total;
            // SendEvent(eventName);
            if (total % 5 == 0)
            {
                SendEvent("af_rewarded_view_5");
            }
            if (total % 20 == 0)
            {
                SendEvent("af_rewarded_view_20");
            }
            if (total % 32 == 0)
            {
                SendEvent("af_rewarded_view_32");
            }
            if (total % 40 == 0)
            {
                SendEvent("af_rewarded_view_40");
            }
        }
        public static void TrackAppsFlyerPurchase(string purchaseId, decimal cost, string currency) {
            float fCost = (float)cost;
            fCost *= 0.65f;
            Dictionary<string, string> eventValue = new Dictionary<string, string>();
            eventValue.Add(AFInAppEvents.REVENUE, fCost.ToString(CultureInfo.InvariantCulture));
            eventValue.Add(AFInAppEvents.CURRENCY, currency);
            eventValue.Add(AFInAppEvents.QUANTITY, "1");
            AppsFlyer.sendEvent(AFInAppEvents.PURCHASE, eventValue);
        }

        // public static void TrackAppsflyerAdRevenue(ImpressionData impressionData)
        // {
        //     SDKDebugLogger.Log("TrackAppsflyerAdRevenue " + impressionData.ad_revenue + " " + impressionData.ad_source + " " + impressionData.ad_mediation + " " + impressionData.ad_unit_name + " " + impressionData.ad_format);
        //     MediationNetwork mediationNetwork = MediationNetwork.ApplovinMax;
        //     switch (impressionData.ad_mediation)
        //     {
        //         case AdsMediationType.MAX:
        //             {
        //                 mediationNetwork = MediationNetwork.ApplovinMax;
        //                 break;
        //             }
        //         case AdsMediationType.ADMOB:
        //             {
        //                 mediationNetwork = MediationNetwork.GoogleAdMob;
        //                 break;
        //             }
        //         case AdsMediationType.IRONSOURCE:
        //             {
        //                 mediationNetwork = MediationNetwork.IronSource;
        //                 break;
        //             }
        //     }
        //     Dictionary<string, string> additionalParams = new Dictionary<string, string>();
        //     additionalParams.Add(AdRevenueScheme.COUNTRY, "USA");
        //     additionalParams.Add(AdRevenueScheme.AD_UNIT, impressionData.ad_unit_name);
        //     additionalParams.Add(AdRevenueScheme.AD_TYPE, impressionData.ad_format);
        //     additionalParams.Add(AdRevenueScheme.PLACEMENT, "");
        //     var logRevenue = new AFAdRevenueData(impressionData.ad_source, mediationNetwork, "USD", impressionData.ad_revenue);
        //     AppsFlyer.logAdRevenue(logRevenue, additionalParams);
        // }
        public static void TrackCompleteLevel(int level)
        {
            // string eventName = af_completed_level + level;
            // SendEvent(eventName);
            if (level % 5 == 0)
            {
                string eventPassLevelName = $"pass_level_{level}";
                SendEvent(eventPassLevelName);
            }
        }
        #endregion

    }
// #endif
}

