using System.Globalization;

namespace Mikkelhm.Core.CloudAlerts;

public sealed record CloudAlertTimelineEntry(CloudAlertView Alert, DateTime LocalTime);

public sealed record CloudAlertTimelineDay(DateOnly Date, string Heading, IReadOnlyList<CloudAlertTimelineEntry> Entries);

public static class CloudAlertsTimeline
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    // Keeps the input order (newest first) inside and across days.
    public static IReadOnlyList<CloudAlertTimelineDay> GroupByDay(IEnumerable<CloudAlertView> alerts, DateOnly today)
        => alerts
            .Select(a => new CloudAlertTimelineEntry(a, CloudAlertsTime.ToLocal(a.TimeFiredUtc)))
            .GroupBy(e => DateOnly.FromDateTime(e.LocalTime))
            .Select(g => new CloudAlertTimelineDay(g.Key, DayHeading(g.Key, today), g.ToList()))
            .ToList();

    public static string DayHeading(DateOnly date, DateOnly today)
        => date.ToString(date.Year == today.Year ? "dddd d MMMM" : "dddd d MMMM yyyy", English);

    public static string RangeText(DateTime oldestUtc, DateTime newestUtc)
    {
        var first = DateOnly.FromDateTime(CloudAlertsTime.ToLocal(oldestUtc));
        var last = DateOnly.FromDateTime(CloudAlertsTime.ToLocal(newestUtc));

        if (first == last)
        {
            return "on " + Format(last, "d MMMM yyyy");
        }

        if (first.Year != last.Year)
        {
            return $"from {Format(first, "d MMMM yyyy")} to {Format(last, "d MMMM yyyy")}";
        }

        return first.Month == last.Month
            ? $"from {Format(first, "%d")} to {Format(last, "d MMMM yyyy")}"
            : $"from {Format(first, "d MMMM")} to {Format(last, "d MMMM yyyy")}";
    }

    private static string Format(DateOnly date, string format) => date.ToString(format, English);
}
