using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mikkelhm.Core.CloudAlerts;

public static class CloudAlertDisplay
{
    // Relaxed escaping keeps characters like < and – readable; Razor HTML-encodes the output.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? SafeHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;

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

    public static string PrettyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, Indented);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
