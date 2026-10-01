namespace Mikkelhm.Core.CloudAlerts;

public static class CloudAlertDisplay
{
    public static string SeverityClass(string severity)
        => severity.Trim().ToLowerInvariant() switch
        {
            "critical" => "sev-critical",
            "high" => "sev-high",
            "medium" => "sev-medium",
            "low" => "sev-low",
            "" => "sev-none",
            _ => "sev-other",
        };
}
