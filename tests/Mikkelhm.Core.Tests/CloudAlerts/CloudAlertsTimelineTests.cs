using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertsTimelineTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    private static CloudAlertView Alert(DateTime utc)
        => new(Guid.NewGuid(), Guid.NewGuid().ToString(), "mikkelhm", "", "Live", "Deployment", "", "low", "", utc, false, "{}");

    [Fact]
    public void GroupByDay_GroupsOnCopenhagenDateKeepingOrder()
    {
        var lateEvening = Alert(new DateTime(2026, 9, 30, 20, 40, 0, DateTimeKind.Utc));   // 30-09 22:40 local
        var afterMidnight = Alert(new DateTime(2026, 9, 29, 22, 30, 0, DateTimeKind.Utc)); // 30-09 00:30 local
        var dayBefore = Alert(new DateTime(2026, 9, 29, 21, 30, 0, DateTimeKind.Utc));     // 29-09 23:30 local

        var days = CloudAlertsTimeline.GroupByDay([lateEvening, afterMidnight, dayBefore], Today);

        Assert.Equal(2, days.Count);
        Assert.Equal(new DateOnly(2026, 9, 30), days[0].Date);
        Assert.Equal([lateEvening, afterMidnight], days[0].Entries.Select(e => e.Alert));
        Assert.Equal(new DateTime(2026, 9, 30, 22, 40, 0), days[0].Entries[0].LocalTime);
        Assert.Equal(new DateOnly(2026, 9, 29), days[1].Date);
        Assert.Equal([dayBefore], days[1].Entries.Select(e => e.Alert));
    }

    [Fact]
    public void GroupByDay_NoAlerts_ReturnsNoDays()
        => Assert.Empty(CloudAlertsTimeline.GroupByDay([], Today));

    [Theory]
    [InlineData(2026, 9, 30, "Wednesday 30 September")]
    [InlineData(2025, 12, 31, "Wednesday 31 December 2025")]
    public void DayHeading_OmitsTheYearOnlyForTheCurrentYear(int y, int m, int d, string expected)
        => Assert.Equal(expected, CloudAlertsTimeline.DayHeading(new DateOnly(y, m, d), Today));

    [Theory]
    [InlineData("2026-09-28T19:18:00Z", "2026-09-30T20:40:00Z", "from 28 to 30 September 2026")]
    [InlineData("2026-09-28T19:18:00Z", "2026-10-02T08:00:00Z", "from 28 September to 2 October 2026")]
    [InlineData("2025-12-28T10:00:00Z", "2026-01-02T10:00:00Z", "from 28 December 2025 to 2 January 2026")]
    [InlineData("2026-09-30T06:00:00Z", "2026-09-30T20:40:00Z", "on 30 September 2026")]
    [InlineData("2026-09-29T22:30:00Z", "2026-09-30T20:40:00Z", "on 30 September 2026")]
    public void RangeText_DescribesTheCopenhagenDates(string oldest, string newest, string expected)
        => Assert.Equal(expected, CloudAlertsTimeline.RangeText(
            DateTime.Parse(oldest, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            DateTime.Parse(newest, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));
}
