namespace Mikkelhm.Core.CloudAlerts;

public sealed class CloudAlertsOptions
{
    public const string SectionName = "CloudAlerts";

    public string? WebhookSecret { get; set; }
}
