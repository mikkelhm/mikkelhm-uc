using System.Globalization;

namespace Mikkelhm.Core.CloudAlerts;

public enum TestAlertMode
{
    Show,
    Hide,
    Only,
}

public sealed record CloudAlertFilter
{
    public const int DefaultPageSize = 50;
    private const string DateFormat = "yyyy-MM-dd";

    public string? Project { get; init; }
    public string? Environment { get; init; }
    public string? AlertName { get; init; }
    public string? Severity { get; init; }
    public string? Search { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public TestAlertMode Tests { get; init; } = TestAlertMode.Show;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public static CloudAlertFilter FromQuery(IReadOnlyDictionary<string, string?> query)
    {
        string? Get(string key)
            => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        return new CloudAlertFilter
        {
            Project = Get("project"),
            Environment = Get("env"),
            AlertName = Get("alert"),
            Severity = Get("severity"),
            Search = Get("q"),
            From = ParseDate(Get("from")),
            To = ParseDate(Get("to")),
            Tests = ParseTests(Get("tests")),
            Page = int.TryParse(Get("page"), NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page > 0 ? page : 1,
        };
    }

    public string ToQueryString()
    {
        var parts = new List<string>();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        Add("project", Project);
        Add("env", Environment);
        Add("alert", AlertName);
        Add("severity", Severity);
        Add("q", Search);
        Add("from", From?.ToString(DateFormat, CultureInfo.InvariantCulture));
        Add("to", To?.ToString(DateFormat, CultureInfo.InvariantCulture));
        if (Tests != TestAlertMode.Show)
        {
            Add("tests", Tests.ToString().ToLowerInvariant());
        }

        if (Page > 1)
        {
            Add("page", Page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static TestAlertMode ParseTests(string? value)
        => value?.ToLowerInvariant() switch
        {
            "hide" => TestAlertMode.Hide,
            "only" => TestAlertMode.Only,
            _ => TestAlertMode.Show,
        };
}
