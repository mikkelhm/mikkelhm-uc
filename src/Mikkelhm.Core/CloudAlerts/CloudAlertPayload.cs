using System.Text.Json;

namespace Mikkelhm.Core.CloudAlerts;

public sealed record CloudAlertPayload(
    string? AlertId,
    string? ProjectAlias,
    string? ProjectUrl,
    string? EnvironmentName,
    DateTimeOffset? TimeFired,
    string? AlertName,
    string? Details,
    JsonElement? CustomerMetadata,
    bool IsTest);
