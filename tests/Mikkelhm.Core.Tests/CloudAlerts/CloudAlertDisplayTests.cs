using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertDisplayTests
{
    [Theory]
    [InlineData("critical", "sev-critical")]
    [InlineData("HIGH", "sev-high")]
    [InlineData("medium", "sev-medium")]
    [InlineData("Low", "sev-low")]
    [InlineData("", "sev-none")]
    [InlineData("\"><script>", "sev-other")]
    public void SeverityClass_MapsKnownValuesAndNeverEchoesInput(string severity, string expected)
        => Assert.Equal(expected, CloudAlertDisplay.SeverityClass(severity));
}
