using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertDisplayTests
{
    [Theory]
    [InlineData("https://www.s1.umbraco.io/project/mikkelhm", "https://www.s1.umbraco.io/project/mikkelhm")]
    [InlineData("http://example.com/x", "http://example.com/x")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("JAVASCRIPT:alert(1)", null)]
    [InlineData("data:text/html,hi", null)]
    [InlineData("/relative/path", null)]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SafeHttpUrl_OnlyAllowsAbsoluteHttpAndHttps(string? input, string? expected)
        => Assert.Equal(expected, CloudAlertDisplay.SafeHttpUrl(input));

    [Theory]
    [InlineData("critical", "sev-critical")]
    [InlineData("HIGH", "sev-high")]
    [InlineData("medium", "sev-medium")]
    [InlineData("Low", "sev-low")]
    [InlineData("", "sev-none")]
    [InlineData("\"><script>", "sev-other")]
    public void SeverityClass_MapsKnownValuesAndNeverEchoesInput(string severity, string expected)
        => Assert.Equal(expected, CloudAlertDisplay.SeverityClass(severity));

    [Fact]
    public void PrettyJson_Valid_IsIndented()
        => Assert.Equal("{\n  \"a\": 1\n}", CloudAlertDisplay.PrettyJson("{\"a\":1}").ReplaceLineEndings("\n"));

    [Fact]
    public void PrettyJson_KeepsNonAsciiAndAngleBracketsReadable()
        => Assert.Equal("{\n  \"d\": \"a <b> – c\"\n}", CloudAlertDisplay.PrettyJson("{\"d\":\"a <b> – c\"}").ReplaceLineEndings("\n"));

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void PrettyJson_Invalid_ReturnsInputUnchanged(string input)
        => Assert.Equal(input, CloudAlertDisplay.PrettyJson(input));
}
