using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class WebhookSecretValidatorTests
{
    private const string Secret = "local-dev-secret-123";

    [Fact]
    public void IsValid_MatchingSecret_ReturnsTrue()
        => Assert.True(WebhookSecretValidator.IsValid(Secret, Secret));

    [Theory]
    [InlineData("wrong")]
    [InlineData("local-dev-secret-123 ")]
    [InlineData(" local-dev-secret-123")]
    [InlineData("LOCAL-DEV-SECRET-123")]
    public void IsValid_DifferentValue_ReturnsFalse(string provided)
        => Assert.False(WebhookSecretValidator.IsValid(Secret, provided));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_MissingHeader_ReturnsFalse(string? provided)
        => Assert.False(WebhookSecretValidator.IsValid(Secret, provided));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_SecretNotConfigured_ReturnsFalseEvenWhenHeaderMatches(string? configured)
        => Assert.False(WebhookSecretValidator.IsValid(configured, configured));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(Secret, true)]
    public void IsConfigured_ReflectsWhetherASecretIsSet(string? configured, bool expected)
        => Assert.Equal(expected, WebhookSecretValidator.IsConfigured(configured));

    [Fact]
    public void HeaderName_IsUcWebhookAuth()
        => Assert.Equal("uc-webhook-auth", WebhookSecretValidator.HeaderName);
}
