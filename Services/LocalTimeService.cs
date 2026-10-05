using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace MCBusiness.Services
{
    public class LocalTimeService : ILocalTimeService
    {
        private const string TimeZoneCookieName = "MCBusiness.TimeZone";
        private static readonly TimeZoneInfo DefaultTimeZone = ResolveDefaultTimeZone();
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalTimeService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public TimeZoneInfo GetUserTimeZone()
        {
            var timeZoneId = _httpContextAccessor.HttpContext?.Request.Cookies[TimeZoneCookieName];

            if (TryFindTimeZone(timeZoneId, out var timeZone))
            {
                return timeZone;
            }

            return DefaultTimeZone;
        }

        public DateTimeOffset ToLocal(DateTimeOffset utcDateTime)
        {
            return TimeZoneInfo.ConvertTime(utcDateTime.ToUniversalTime(), GetUserTimeZone());
        }

        public DateTimeOffset? ToLocal(DateTimeOffset? utcDateTime)
        {
            return utcDateTime.HasValue ? ToLocal(utcDateTime.Value) : null;
        }

        public string Format(DateTimeOffset utcDateTime, string format = "dd/MM/yyyy HH:mm", CultureInfo culture = null)
        {
            return ToLocal(utcDateTime).ToString(format, culture ?? CultureInfo.CurrentCulture);
        }

        public string Format(DateTimeOffset? utcDateTime, string format = "dd/MM/yyyy HH:mm", CultureInfo culture = null)
        {
            return utcDateTime.HasValue
                ? Format(utcDateTime.Value, format, culture)
                : string.Empty;
        }

        private static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
        {
            timeZone = null;

            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return false;
            }

            var normalizedId = Uri.UnescapeDataString(timeZoneId.Trim());

            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(normalizedId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalizedId, out var windowsId))
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    return true;
                }
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

            return false;
        }

        private static TimeZoneInfo ResolveDefaultTimeZone()
        {
            if (TryFindTimeZone("America/Port-au-Prince", out var haitiTimeZone))
            {
                return haitiTimeZone;
            }

            return TimeZoneInfo.Local;
        }
    }
}
