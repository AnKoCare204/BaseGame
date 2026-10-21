using System;
using System.Globalization;

namespace BaseGame.Scripts
{
    public static class TimExtension
    {
        private static readonly CultureInfo EnUsCulture = CultureInfo.GetCultureInfo("en-US");

        private const string StandardDateTimeFormat = "MM/dd/yyyy HH:mm:ss";
        private const string StandardDateFormat = "MM/dd/yyyy";

        public static string ToEnUsString(this DateTime dateTime)
        {
            return dateTime.ToString(StandardDateTimeFormat, EnUsCulture);
        }

        public static DateTime ToDateTime(this string dateString, DateTime defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return defaultValue;

            if (DateTime.TryParse(dateString, EnUsCulture, DateTimeStyles.None, out DateTime result))
                return result;

            return defaultValue;
        }
    }
}