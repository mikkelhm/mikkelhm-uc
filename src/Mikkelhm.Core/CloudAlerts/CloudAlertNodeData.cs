namespace Mikkelhm.Core.CloudAlerts;

public sealed record CloudAlertNodeData(
    string Name,
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
