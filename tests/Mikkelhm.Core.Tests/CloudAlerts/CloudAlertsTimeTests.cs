using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertsTimeTests
{
    [Fact]
    public void ToLocal_Summer_IsUtcPlusTwo()
        => Assert.Equal(new DateTime(2026, 9, 30, 21, 18, 0), CloudAlertsTime.ToLocal(new DateTime(2026, 9, 30, 19, 18, 0, DateTimeKind.Utc)));

    [Fact]
    public void ToLocal_Winter_IsUtcPlusOne()
        => Assert.Equal(new DateTime(2026, 12, 1, 9, 0, 0), CloudAlertsTime.ToLocal(new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void ToLocal_UnspecifiedKind_IsTreatedAsUtc()
        => Assert.Equal(new DateTime(2026, 9, 30, 21, 18, 0), CloudAlertsTime.ToLocal(new DateTime(2026, 9, 30, 19, 18, 0, DateTimeKind.Unspecified)));

    [Theory]
    [InlineData(2026, 9, 30, 2026, 9, 29, 22)]
    [InlineData(2026, 12, 1, 2026, 11, 30, 23)]
    [InlineData(2026, 10, 25, 2026, 10, 24, 22)]
    [InlineData(2026, 10, 26, 2026, 10, 25, 23)]
    public void StartOfDayUtc_IsCopenhagenMidnight(int y, int m, int d, int uy, int um, int ud, int uh)
        => Assert.Equal(new DateTime(uy, um, ud, uh, 0, 0, DateTimeKind.Utc), CloudAlertsTime.StartOfDayUtc(new DateOnly(y, m, d)));

    [Fact]
    public void ZoneLabel_Summer_IsCest()
        => Assert.Equal("CEST, UTC+02:00", CloudAlertsTime.ZoneLabel(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void ZoneLabel_Winter_IsCet()
        => Assert.Equal("CET, UTC+01:00", CloudAlertsTime.ZoneLabel(new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Abbreviation_FollowsTheAlertsOwnDate()
    {
        Assert.Equal("CEST", CloudAlertsTime.Abbreviation(new DateTime(2026, 10, 25, 0, 59, 0, DateTimeKind.Utc)));
        Assert.Equal("CET", CloudAlertsTime.Abbreviation(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc)));
    }
}
