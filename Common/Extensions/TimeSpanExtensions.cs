using System;

namespace Common.Extensions
{
    public static class TimeSpanExtensions
    {
        public static string AsString(this TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}.{timeSpan.Milliseconds / 10:00}";
        }
    }
}
