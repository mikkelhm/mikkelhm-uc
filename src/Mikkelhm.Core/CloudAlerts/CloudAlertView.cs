namespace Mikkelhm.Core.CloudAlerts;

public sealed record CloudAlertView(
    Guid Key,
    string AlertId,
    string ProjectAlias,
    string ProjectUrl,
    string EnvironmentName,
    string AlertName,
    string Details,
    string Severity,
    string CustomerMetadata,
    DateTime TimeFiredUtc,
    bool IsTest,
    string RawPayload);
