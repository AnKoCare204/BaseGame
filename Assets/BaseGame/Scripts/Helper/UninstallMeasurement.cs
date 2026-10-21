using AppsFlyerSDK;
using TW.Utility.DesignPattern;
using UnityEngine;

#if UNITY_IOS
using System.Collections;
using Unity.Notifications.iOS;
#endif

namespace _GAME.BaseGame
{
    public class UninstallMeasurement : Singleton<UninstallMeasurement>
    {
        [SerializeField] private AppsFlyerManualObject afObject;

        private void Start()
        {
#if UNITY_IOS
            afObject.ConversionDataSucceeded += onConversionDataSuccess;
#else
            Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
#endif
        }

#if UNITY_IOS
        private void onConversionDataSuccess(string conversionData)
        {
            StartCoroutine(RequestAuthorization());
            IEnumerator RequestAuthorization()
            {
                using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge, true))
                {
                    while (!req.IsFinished)
                    {
                        yield return null;
                    }
                    if (req.Granted && req.DeviceToken != "")
                    {
                        byte[] tokenBytes = ConvertHexStringToByteArray(req.DeviceToken);
                        AppsFlyer.registerUninstall(tokenBytes);
                    }
                }
            }
            byte[] ConvertHexStringToByteArray(string hexString)
            {
                byte[] data = new byte[hexString.Length / 2];
                for (int index = 0; index < data.Length; index++)
                {
                    string byteValue = hexString.Substring(index * 2, 2);
                    data[index] = System.Convert.ToByte(byteValue, 16);
                }
                return data;
            }
        }
#else
        private void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
        {
            AppsFlyer.updateServerUninstallToken(token.Token);
        }
#endif
    }
}