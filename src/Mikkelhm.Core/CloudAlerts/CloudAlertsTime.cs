using System.Globalization;

namespace Mikkelhm.Core.CloudAlerts;

// The listing page shows and filters every alert in Copenhagen time.
public static class CloudAlertsTime
{
    public static TimeZoneInfo Zone { get; } = FindZone();

    public static DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime StartOfDayUtc(DateOnly day)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);

    public static string Abbreviation(DateTime utc)
        => Zone.IsDaylightSavingTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) ? "CEST" : "CET";

    public static string ZoneLabel(DateTime utc)
    {
        var offset = Zone.GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return string.Create(CultureInfo.InvariantCulture, $"{Abbreviation(utc)}, UTC+{offset:hh\\:mm}");
    }

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows id, for hosts without IANA time zone support.
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}
