using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Analytics;
// using Manager;

namespace SDK.Analytics
{
    /// <summary>
    /// Centralized analytics manager for tracking ads and game events
    /// </summary>
    public static class AnalyticsManager
    {
        private const string EVENT_AD_IMPRESSION = "ad_impression";
        private const string EVENT_AD_IMPRESSION_ABI = "ad_impression_abi";

        #region Platform Mapping
        private static readonly Dictionary<AdsMediationType, string> PlatformNames = new Dictionary<AdsMediationType, string>
        {
            { AdsMediationType.MAX, "Applovin" },
            { AdsMediationType.ADMOB, "Admob" },
            { AdsMediationType.IRONSOURCE, "Ironsource" }
        };
        #endregion

        #region Ad Impression Tracking
        /// <summary>
        /// Track ad impression with revenue data
        /// </summary>
        public static void TrackAdImpression(ImpressionData impressionData)
        {
            if (impressionData == null)
            {
                SDKDebugLogger.LogWarning("ImpressionData is null");
                return;
            }

            string platformName = GetPlatformName(impressionData.ad_mediation);

            var parameters = new Parameter[]
            {
                //new Parameter("level", GameManager.Instance.Level.Value.ToString()),
                new Parameter("ad_platform", platformName),
                new Parameter("ad_source", impressionData.ad_source ?? "unknown"),
                new Parameter("ad_unit_name", impressionData.ad_unit_name ?? "unknown"),
                new Parameter("ad_format", impressionData.ad_format ?? "unknown"),
                new Parameter("value", impressionData.ad_revenue),
                new Parameter("currency", "USD")
            };

            FirebaseManager.Instance.LogFirebaseEvent(EVENT_AD_IMPRESSION, parameters);
            FirebaseManager.Instance.LogFirebaseEvent(EVENT_AD_IMPRESSION_ABI, parameters);
#if UNITY_APPSFLYER
            Dictionary<string, string> eventValue = new Dictionary<string, string>
            {
                // { "level", GameManager.Instance.Level.Value.ToString() },
                { "ad_platform", platformName },
                { "ad_source", impressionData.ad_source ?? "unknown" },
                { "ad_unit_name", impressionData.ad_unit_name ?? "unknown" },
                { "ad_format", impressionData.ad_format ?? "unknown" },
                { "value", impressionData.ad_revenue.ToString() },
                { "currency", "USD" }
            };
            AppsFlyerManager.TrackAdImpression(eventValue);
#endif
        }

        private static string GetPlatformName(AdsMediationType mediationType)
        {
            return PlatformNames.TryGetValue(mediationType, out string platformName)
                ? platformName
                : mediationType.ToString();
        }
        #endregion
    }
}
