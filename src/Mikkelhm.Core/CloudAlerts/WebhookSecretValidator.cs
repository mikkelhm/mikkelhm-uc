using System.Security.Cryptography;
using System.Text;

namespace Mikkelhm.Core.CloudAlerts;

public static class WebhookSecretValidator
{
    public const string HeaderName = "uc-webhook-auth";

    public static bool IsConfigured(string? configuredSecret)
        => !string.IsNullOrWhiteSpace(configuredSecret);

    public static bool IsValid(string? configuredSecret, string? providedValue)
    {
        if (!IsConfigured(configuredSecret) || string.IsNullOrEmpty(providedValue))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredSecret!),
            Encoding.UTF8.GetBytes(providedValue));
    }
}
