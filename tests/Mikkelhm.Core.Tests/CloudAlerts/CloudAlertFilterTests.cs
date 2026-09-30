using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertFilterTests
{
    private static CloudAlertFilter From(params (string Key, string? Value)[] pairs)
        => CloudAlertFilter.FromQuery(pairs.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void FromQuery_Empty_ReturnsDefaults()
    {
        var filter = From();

        Assert.Equal(new CloudAlertFilter(), filter);
        Assert.Equal(TestAlertMode.Show, filter.Tests);
        Assert.Equal(1, filter.Page);
        Assert.Equal(50, filter.PageSize);
    }

    [Fact]
    public void FromQuery_AllValues_AreParsedAndTrimmed()
    {
        var filter = From(
            ("project", " mikkelhm "), ("env", "Live"), ("alert", "Deployment"), ("severity", "medium"),
            ("q", "completed"), ("from", "2026-09-01"), ("to", "2026-09-30"), ("tests", "HIDE"), ("page", "3"));

        Assert.Equal("mikkelhm", filter.Project);
        Assert.Equal("Live", filter.Environment);
        Assert.Equal("Deployment", filter.AlertName);
        Assert.Equal("medium", filter.Severity);
        Assert.Equal("completed", filter.Search);
        Assert.Equal(new DateOnly(2026, 9, 1), filter.From);
        Assert.Equal(new DateOnly(2026, 9, 30), filter.To);
        Assert.Equal(TestAlertMode.Hide, filter.Tests);
        Assert.Equal(3, filter.Page);
    }

    [Theory]
    [InlineData("page", "abc")]
    [InlineData("page", "-3")]
    [InlineData("page", "0")]
    [InlineData("from", "31-02-2026")]
    [InlineData("to", "2026-02-31")]
    [InlineData("tests", "weird")]
    [InlineData("tests", "7")]
    [InlineData("project", "   ")]
    public void FromQuery_GarbageValues_FallBackToDefaults(string key, string value)
        => Assert.Equal(new CloudAlertFilter(), From((key, value)));

    [Fact]
    public void ToQueryString_Defaults_IsEmpty()
        => Assert.Equal("", new CloudAlertFilter().ToQueryString());

    [Fact]
    public void ToQueryString_EscapesValuesAndOmitsDefaults()
    {
        var filter = new CloudAlertFilter
        {
            Project = "my project", Search = "a&b", From = new DateOnly(2026, 9, 1), Tests = TestAlertMode.Only, Page = 2,
        };

        Assert.Equal("?project=my%20project&q=a%26b&from=2026-09-01&tests=only&page=2", filter.ToQueryString());
    }

    [Fact]
    public void ToQueryString_RoundTripsThroughFromQuery()
    {
        var original = new CloudAlertFilter
        {
            Project = "p", Environment = "Live", AlertName = "Deployment", Severity = "high", Search = "x y",
            From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30), Tests = TestAlertMode.Hide, Page = 4,
        };

        var query = original.ToQueryString().TrimStart('?').Split('&')
            .Select(part => part.Split('='))
            .ToDictionary(kv => kv[0], kv => (string?)Uri.UnescapeDataString(kv[1]));

        Assert.Equal(original, CloudAlertFilter.FromQuery(query));
    }
}
