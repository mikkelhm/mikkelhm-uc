using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertsQueryTests
{
    private static readonly DateTime Noon = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static CloudAlertView Alert(
        string project = "mikkelhm", string env = "Live", string alert = "Deployment", string severity = "medium",
        string details = "Deployment completed", DateTime? time = null, bool isTest = false)
        => new(Guid.NewGuid(), Guid.NewGuid().ToString(), project, $"https://www.s1.umbraco.io/project/{project}",
            env, alert, details, severity, "", time ?? Noon, isTest, "{}");

    [Fact]
    public void Run_NoFilter_ReturnsAllNewestFirst()
    {
        var older = Alert(time: Noon.AddHours(-1));
        var newer = Alert(time: Noon.AddHours(1));

        var page = CloudAlertsQuery.Run([older, newer], new CloudAlertFilter());

        Assert.Equal([newer, older], page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public void Run_StringFilters_MatchExactlyIgnoringCase()
    {
        var match = Alert(project: "mikkelhm", env: "Live", alert: "Deployment", severity: "high");
        var alerts = new[]
        {
            match,
            Alert(project: "other", env: "Live", alert: "Deployment", severity: "high"),
            Alert(project: "mikkelhm", env: "Development", alert: "Deployment", severity: "high"),
            Alert(project: "mikkelhm", env: "Live", alert: "Upgrade", severity: "high"),
            Alert(project: "mikkelhm", env: "Live", alert: "Deployment", severity: "low"),
            Alert(project: "mikkelhm-old", env: "Live", alert: "Deployment", severity: "high"),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter
        {
            Project = "MIKKELHM", Environment = "live", AlertName = "deployment", Severity = "HIGH",
        });

        Assert.Equal([match], page.Items);
    }

    [Fact]
    public void Run_Search_MatchesDetailsIgnoringCase()
    {
        var match = Alert(details: "[TEST] Deployment Completed for mikkelhm");

        var page = CloudAlertsQuery.Run([match, Alert(details: "Upgrade failed")], new CloudAlertFilter { Search = "completed" });

        Assert.Equal([match], page.Items);
    }

    [Fact]
    public void Run_DateRange_IncludesBothWholeCopenhagenDays()
    {
        // 30-09-2026 in Copenhagen (CEST, UTC+2) runs from 29-09 22:00 UTC to 30-09 22:00 UTC.
        var startOfDay = Alert(time: new DateTime(2026, 9, 29, 22, 0, 0, DateTimeKind.Utc));
        var endOfDay = Alert(time: new DateTime(2026, 9, 30, 21, 59, 59, DateTimeKind.Utc));
        var dayBefore = Alert(time: new DateTime(2026, 9, 29, 21, 59, 59, DateTimeKind.Utc));
        var dayAfter = Alert(time: new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc));

        var page = CloudAlertsQuery.Run(
            [startOfDay, endOfDay, dayBefore, dayAfter],
            new CloudAlertFilter { From = new DateOnly(2026, 9, 30), To = new DateOnly(2026, 9, 30) });

        Assert.Equal([endOfDay, startOfDay], page.Items);
    }

    [Theory]
    [InlineData(TestAlertMode.Show, 2)]
    [InlineData(TestAlertMode.Hide, 1)]
    [InlineData(TestAlertMode.Only, 1)]
    public void Run_TestsMode_FiltersTestAlerts(TestAlertMode mode, int expectedCount)
    {
        var real = Alert(isTest: false);
        var test = Alert(isTest: true);

        var page = CloudAlertsQuery.Run([real, test], new CloudAlertFilter { Tests = mode });

        Assert.Equal(expectedCount, page.TotalCount);
        if (mode == TestAlertMode.Hide) Assert.Equal([real], page.Items);
        if (mode == TestAlertMode.Only) Assert.Equal([test], page.Items);
    }

    [Fact]
    public void Run_Paging_ReturnsRequestedPage()
    {
        var alerts = Enumerable.Range(0, 120).Select(i => Alert(time: Noon.AddMinutes(-i))).ToList();

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Page = 3 });

        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.Page);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(alerts[100], page.Items[0]);
    }

    [Fact]
    public void Run_PageBeyondLast_IsClampedToLastPage()
    {
        var alerts = Enumerable.Range(0, 60).Select(i => Alert(time: Noon.AddMinutes(-i))).ToList();

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Page = 99 });

        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.Items.Count);
    }

    [Fact]
    public void Run_DateRange_UsesWinterOffsetAfterSummerTimeEnds()
    {
        // 26-10-2026 in Copenhagen (CET, UTC+1) runs from 25-10 23:00 UTC to 26-10 23:00 UTC.
        var inside = Alert(time: new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc));
        var before = Alert(time: new DateTime(2026, 10, 25, 22, 59, 59, DateTimeKind.Utc));

        var page = CloudAlertsQuery.Run([inside, before], new CloudAlertFilter { From = new DateOnly(2026, 10, 26), To = new DateOnly(2026, 10, 26) });

        Assert.Equal([inside], page.Items);
    }

    [Fact]
    public void Run_ReportsOldestAndNewestOfAllFilteredAlerts()
    {
        var alerts = Enumerable.Range(0, 60).Select(i => Alert(time: Noon.AddHours(-i))).Append(Alert(project: "other", time: Noon.AddDays(5))).ToList();

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Project = "mikkelhm", Page = 1 });

        Assert.Equal(Noon, page.NewestUtc);
        Assert.Equal(Noon.AddHours(-59), page.OldestUtc);
    }

    [Fact]
    public void Run_NoAlerts_ReturnsEmptySinglePage()
    {
        var page = CloudAlertsQuery.Run([], new CloudAlertFilter { Page = 5 });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.SeverityCounts);
        Assert.Null(page.OldestUtc);
        Assert.Null(page.NewestUtc);
    }

    [Fact]
    public void Run_SeverityCounts_IgnoreSeverityFilterButRespectOthers()
    {
        var alerts = new[]
        {
            Alert(severity: "low"), Alert(severity: "low"), Alert(severity: "HIGH"), Alert(severity: "high"),
            Alert(severity: "medium"), Alert(severity: ""), Alert(project: "other", severity: "medium"),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Project = "mikkelhm", Severity = "low" });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(3, page.SeverityCounts.Count);
        Assert.Equal(2, page.SeverityCounts.Single(s => s.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)).Count);
        Assert.Equal(new SeverityCount("low", 2), page.SeverityCounts.Single(s => s.Severity == "low"));
        Assert.Equal(new SeverityCount("medium", 1), page.SeverityCounts.Last());
    }

    [Fact]
    public void Run_Facets_ComeFromAllAlertsDistinctSortedWithoutEmpty()
    {
        var alerts = new[]
        {
            Alert(project: "zeta", env: "Live", alert: "Upgrade", severity: "low"),
            Alert(project: "alpha", env: "Development", alert: "Deployment", severity: ""),
            Alert(project: "Alpha", env: "Live", alert: "Deployment", severity: "high", isTest: true),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Project = "zeta", Tests = TestAlertMode.Hide });

        Assert.Equal(["alpha", "zeta"], page.Facets.Projects, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["Development", "Live"], page.Facets.Environments);
        Assert.Equal(["Deployment", "Upgrade"], page.Facets.AlertNames);
        Assert.Equal(["high", "low"], page.Facets.Severities);
    }
}
