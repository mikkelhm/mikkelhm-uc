namespace Mikkelhm.Core.CloudAlerts;

public sealed record SeverityCount(string Severity, int Count);

public sealed record CloudAlertFacets(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Environments,
    IReadOnlyList<string> AlertNames,
    IReadOnlyList<string> Severities);

public sealed record CloudAlertsPage(
    IReadOnlyList<CloudAlertView> Items,
    int TotalCount,
    int Page,
    int TotalPages,
    IReadOnlyList<SeverityCount> SeverityCounts,
    CloudAlertFacets Facets,
    DateTime? OldestUtc,
    DateTime? NewestUtc);

public static class CloudAlertsQuery
{
    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    public static CloudAlertsPage Run(IEnumerable<CloudAlertView> alerts, CloudAlertFilter filter)
    {
        var all = alerts.ToList();

        var facets = new CloudAlertFacets(
            Distinct(all, a => a.ProjectAlias),
            Distinct(all, a => a.EnvironmentName),
            Distinct(all, a => a.AlertName),
            Distinct(all, a => a.Severity));

        var matchingExceptSeverity = all.Where(a => MatchesAllButSeverity(a, filter)).ToList();

        var severityCounts = matchingExceptSeverity
            .Where(a => a.Severity.Length > 0)
            .GroupBy(a => a.Severity, IgnoreCase)
            .Select(g => new SeverityCount(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Severity, IgnoreCase)
            .ToList();

        var filtered = matchingExceptSeverity
            .Where(a => Matches(a.Severity, filter.Severity))
            .OrderByDescending(a => a.TimeFiredUtc)
            .ThenBy(a => a.Key)
            .ToList();

        var pageSize = Math.Max(1, filter.PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CloudAlertsPage(
            items,
            filtered.Count,
            page,
            totalPages,
            severityCounts,
            facets,
            filtered.Count > 0 ? filtered[^1].TimeFiredUtc : null,
            filtered.Count > 0 ? filtered[0].TimeFiredUtc : null);
    }

    private static bool MatchesAllButSeverity(CloudAlertView alert, CloudAlertFilter filter)
        => Matches(alert.ProjectAlias, filter.Project)
           && Matches(alert.EnvironmentName, filter.Environment)
           && Matches(alert.AlertName, filter.AlertName)
           && (filter.Search is null || alert.Details.Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
           && (filter.From is null || alert.TimeFiredUtc >= CloudAlertsTime.StartOfDayUtc(filter.From.Value))
           && (filter.To is null || alert.TimeFiredUtc < CloudAlertsTime.StartOfDayUtc(filter.To.Value.AddDays(1)))
           && filter.Tests switch
           {
               TestAlertMode.Hide => !alert.IsTest,
               TestAlertMode.Only => alert.IsTest,
               _ => true,
           };

    private static bool Matches(string value, string? wanted)
        => wanted is null || string.Equals(value, wanted, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Distinct(IEnumerable<CloudAlertView> alerts, Func<CloudAlertView, string> selector)
        => alerts.Select(selector)
            .Where(v => v.Length > 0)
            .Distinct(IgnoreCase)
            .Order(IgnoreCase)
            .ToList();
}
