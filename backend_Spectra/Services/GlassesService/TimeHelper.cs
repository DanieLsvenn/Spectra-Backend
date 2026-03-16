namespace Services.GlassesService
{
    /// <summary>
    /// Centralized time helper that provides Vietnam local time (UTC+7) for all
    /// date/time operations. The project stores and compares dates in Vietnam time
    /// since campaign dates, order dates, etc. are entered in local time by Vietnamese users.
    /// </summary>
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                // Windows
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                // Linux/Mac
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
        }

        /// <summary>
        /// Returns the current date and time in Vietnam timezone (UTC+7).
        /// Use this instead of DateTime.UtcNow or DateTime.Now throughout the project.
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }
}
