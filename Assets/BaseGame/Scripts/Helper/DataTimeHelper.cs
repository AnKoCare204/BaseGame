using System;
using System.Globalization;
using UnityEngine;

namespace Game.Helper
{
    public static class DataTimeHelper
    {
        public static string DateTimeDefaultString()
        {
            return ((DateTime)default).DateTimeToString();
        }
        public static string DateTimeToString(this DateTime dateTime)
        {
            return dateTime.ToString(CultureInfo.InvariantCulture);
        }
        public static DateTime StringToDateTime(this string dateTimeString)
        {
            return DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time) ? time : default;
        }
        public static string TimeSpanToStringUI(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return timeSpan.Hours > 0 ?
                    $"{(int)timeSpan.TotalDays}d{timeSpan.Hours:D2}h" :
                    $"{(int)timeSpan.TotalDays}d";
            }

            if (timeSpan.TotalHours >= 1)
            {
                return timeSpan.Minutes > 0 ?
                    $"{(int)timeSpan.TotalHours}h{timeSpan.Minutes:D2}m" :
                    $"{(int)timeSpan.TotalHours}h";
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return timeSpan.Seconds > 0 ?
                    $"{(int)timeSpan.TotalMinutes}m{timeSpan.Seconds:D2}s" :
                    $"{(int)timeSpan.TotalMinutes}m";
            }

            return $"{(int)timeSpan.TotalSeconds}s";
        }
        public static string TimeSpanToStringUIWithColon(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return timeSpan.Hours > 0 ?
                    $"{(int)timeSpan.TotalDays}:d{timeSpan.Hours:D2}h" :
                    $"{(int)timeSpan.TotalDays}d";
            }

            if (timeSpan.TotalHours >= 1)
            {
                return timeSpan.Minutes > 0 ?
                    $"{(int)timeSpan.TotalHours}h:{timeSpan.Minutes:D2}m" :
                    $"{(int)timeSpan.TotalHours}h";
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return timeSpan.TotalSeconds > 0 ?
                    $"{(int)timeSpan.TotalMinutes}m:{timeSpan.Seconds:D2}s" :
                    $"{(int)timeSpan.TotalMinutes}m";
            }

            return $"{(int)timeSpan.TotalSeconds}s";
        }
    }
}