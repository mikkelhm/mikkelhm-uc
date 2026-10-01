using System.Globalization;
using System.Text.Json;

namespace Mikkelhm.Core.CloudAlerts;

public static class CloudAlertMapper
{
    public const int MaxNameLength = 255;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(string rawPayload, out CloudAlertPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<CloudAlertPayload>(rawPayload, JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        return payload is not null;
    }

    public static IReadOnlyList<string> Validate(CloudAlertPayload payload)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(payload.AlertId))
        {
            errors.Add("alertId is required");
        }

        if (payload.TimeFired is null)
        {
            errors.Add("timeFired is required");
        }

        return errors;
    }

    public static CloudAlertNodeData ToNodeData(CloudAlertPayload payload, string rawPayload)
    {
        if (payload.TimeFired is not { } timeFired)
        {
            throw new ArgumentException("timeFired is required", nameof(payload));
        }

        var timeFiredUtc = timeFired.UtcDateTime;
        var alertName = Clean(payload.AlertName);
        var projectAlias = Clean(payload.ProjectAlias);
        var environmentName = Clean(payload.EnvironmentName);

        return new CloudAlertNodeData(
            BuildName(alertName, projectAlias, environmentName, timeFiredUtc),
            Clean(payload.AlertId),
            projectAlias,
            Clean(payload.ProjectUrl),
            environmentName,
            alertName,
            Clean(payload.Details),
            ReadSeverity(payload.CustomerMetadata),
            ReadMetadata(payload.CustomerMetadata),
            timeFiredUtc,
            payload.IsTest,
            rawPayload);
    }

    public static string BuildName(string alertName, string projectAlias, string environmentName, DateTime timeFiredUtc)
    {
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{OrDefault(alertName, "Alert")} – {OrDefault(projectAlias, "unknown")} ({OrDefault(environmentName, "unknown")}) {timeFiredUtc:dd-MM-yyyy HH:mm}");

        return name.Length <= MaxNameLength ? name : name[..MaxNameLength];
    }

    // Storage format of the Umbraco.DateTimeUnspecified editor ("Date Picker with time").
    public static string ToDateTimeEditorValue(DateTime utc)
        => string.Create(CultureInfo.InvariantCulture, $$"""{"date":"{{utc:yyyy-MM-dd'T'HH:mm:ss}}+00:00","timeZone":null}""");

    private static string Clean(string? value) => value?.Trim() ?? "";

    private static string OrDefault(string value, string fallback) => value.Length > 0 ? value : fallback;

    private static string ReadSeverity(JsonElement? metadata)
        => metadata is { ValueKind: JsonValueKind.Object } element
           && element.TryGetProperty("severity", out var severity)
           && severity.ValueKind == JsonValueKind.String
            ? severity.GetString()!.Trim()
            : "";

    private static string ReadMetadata(JsonElement? metadata)
        => metadata is { } element && element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? element.GetRawText()
            : "";
}
