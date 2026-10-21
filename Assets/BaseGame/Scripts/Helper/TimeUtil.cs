using System;
using System.Globalization;
using Cysharp.Text;
using UnityEngine;

namespace BaseGame.Scripts.Common
{
    public class TimeUtil : MonoBehaviour {
        public static string RemainTimeToString(float remainTime) {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainTime);
            return string.Format("{0:D1}h{1:D2}m{2:D2}s", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }
        public static string RemainTimeToString2(float remainTime) {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainTime);
            return string.Format("{0:D1}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }
        public static string RemainTimeToString3(float remainTime) {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainTime);
            return string.Format("{0:D1}h{1:D2}m", timeSpan.Hours, timeSpan.Minutes);
        }
        public static string TimeToString(double inputTime, TimeFommat timeFommat = TimeFommat.Symbol)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(inputTime);
            if (timeFommat == TimeFommat.Keyword)
            {
                if (timeSpan.TotalDays >= 1)
                {
                    if(timeSpan.Hours == 0)
                        return ZString.Format("{0:D1}d", timeSpan.Days);
                    return ZString.Format("{0:D2}d {1:D2}h {2:D2}m", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    if (timeSpan.Seconds == 0)
                    {
                        if (timeSpan.Minutes == 0)
                            return ZString.Format("{0:D1}h", timeSpan.Hours);
                        else
                            return ZString.Format("{0:D2}h {1:D2}m", timeSpan.Hours, timeSpan.Minutes);
                    }
                    return ZString.Format("{0:D2}h {1:D2}m {2:D2}s", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
                }
                else if (timeSpan.TotalMinutes >= 1)
                {
                    if (timeSpan.Seconds == 0)
                        return ZString.Format("{0:D2}m", timeSpan.Minutes);
                    return ZString.Format("{0:D2}m {1:D2}s", timeSpan.Minutes, timeSpan.Seconds);
                }
                if (timeSpan.TotalSeconds < 10)
                {
                    return ZString.Format("{0:D2}s", timeSpan.Seconds);
                }
                return ZString.Format("{0:D2}s", timeSpan.Seconds);
            }
            else if (timeFommat == TimeFommat.Symbol)
            {
                if (timeSpan.TotalDays >= 1)
                {
                    return ZString.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    if (timeSpan.Seconds == 0)
                    {
                        if (timeSpan.Minutes == 0)
                            return ZString.Format("{0:D2}:00:00", timeSpan.Hours);
                        else
                            return ZString.Format("{0:D2}:{1:D2}:00", timeSpan.Hours, timeSpan.Minutes);
                    }
                    return ZString.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
                }
                else if (timeSpan.TotalMinutes >= 1)
                {
                    return ZString.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
                }
                if (timeSpan.TotalSeconds < 10)
                {
                    return ZString.Format("00:{0:D2}", timeSpan.Seconds);
                }
                return ZString.Format("00:{0:D2}", timeSpan.Seconds);
            }
            return "";
        }

        public static string ConvertFloatToString(float value, string format = "{0:0.00}") {
            if (value == (int)value) return value.ToString();
            return string.Format(format, value);
        }

        public static DateTime Parse(string s)
        {
            try
            {
                DateTime time = DateTime.Parse(s, new CultureInfo("en-US"));
                return time;
            }
            catch
            {
                DateTime time = DateTime.Parse(s);
                return time;
            }
        }
        public static string DateTimeToString(DateTime t)
        {
            return t.ToString(new CultureInfo("en-US"));
        }

        public static CultureInfo cultureInfor;
        public static CultureInfo GetCultureInfo() {
            if (cultureInfor == null)
                cultureInfor = new CultureInfo("en-US");

            return cultureInfor;
        }
    }

    public enum TimeFommat
    {
        Keyword,
        Symbol
    }
}