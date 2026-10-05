using System.Globalization;

namespace MCBusiness.Services
{
    public interface ILocalTimeService
    {
        TimeZoneInfo GetUserTimeZone();
        DateTimeOffset ToLocal(DateTimeOffset utcDateTime);
        DateTimeOffset? ToLocal(DateTimeOffset? utcDateTime);
        string Format(DateTimeOffset utcDateTime, string format = "dd/MM/yyyy HH:mm", CultureInfo culture = null);
        string Format(DateTimeOffset? utcDateTime, string format = "dd/MM/yyyy HH:mm", CultureInfo culture = null);
    }
}
