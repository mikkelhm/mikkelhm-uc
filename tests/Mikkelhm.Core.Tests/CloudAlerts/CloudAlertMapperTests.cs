using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertMapperTests
{
    private const string SamplePayload = """
        {
          "alertId": "6b2c86d7-c408-47d0-aa37-fc77591ff329",
          "projectAlias": "mikkelhm",
          "projectUrl": "https://www.s1.umbraco.io/project/mikkelhm",
          "environmentName": "Live",
          "timeFired": "2026-09-30T19:18:41.2585377Z",
          "alertName": "Deployment",
          "details": "[TEST] Deployment completed for mikkelhm (Live)",
          "customerMetadata": { "severity": "medium" },
          "isTest": true
        }
        """;

    private static CloudAlertPayload Parse(string json)
    {
        Assert.True(CloudAlertMapper.TryParse(json, out var payload));
        return payload!;
    }

    [Fact]
    public void TryParse_SamplePayload_ReadsAllFields()
    {
        var payload = Parse(SamplePayload);

        Assert.Equal("6b2c86d7-c408-47d0-aa37-fc77591ff329", payload.AlertId);
        Assert.Equal("mikkelhm", payload.ProjectAlias);
        Assert.Equal("https://www.s1.umbraco.io/project/mikkelhm", payload.ProjectUrl);
        Assert.Equal("Live", payload.EnvironmentName);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 19, 18, 41, TimeSpan.Zero).AddTicks(2585377), payload.TimeFired);
        Assert.Equal("Deployment", payload.AlertName);
        Assert.Equal("[TEST] Deployment completed for mikkelhm (Live)", payload.Details);
        Assert.True(payload.IsTest);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("""{ "alertId": "a", "isTest": "yes" }""")]
    public void TryParse_InvalidJson_ReturnsFalse(string json)
    {
        Assert.False(CloudAlertMapper.TryParse(json, out var payload));
        Assert.Null(payload);
    }

    [Fact]
    public void TryParse_UnknownFields_AreIgnored()
    {
        var payload = Parse("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z", "somethingNew": 42 }""");
        Assert.Equal("a", payload.AlertId);
    }

    [Fact]
    public void Validate_SamplePayload_HasNoErrors()
        => Assert.Empty(CloudAlertMapper.Validate(Parse(SamplePayload)));

    [Fact]
    public void Validate_MissingAlertIdAndTimeFired_ReturnsBothErrors()
    {
        var errors = CloudAlertMapper.Validate(Parse("""{ "alertId": "  " }"""));

        Assert.Contains("alertId is required", errors);
        Assert.Contains("timeFired is required", errors);
    }

    [Fact]
    public void ToNodeData_SamplePayload_MapsEveryProperty()
    {
        var data = CloudAlertMapper.ToNodeData(Parse(SamplePayload), SamplePayload);

        Assert.Equal("Deployment – mikkelhm (Live) 30-09-2026 19:18", data.Name);
        Assert.Equal("6b2c86d7-c408-47d0-aa37-fc77591ff329", data.AlertId);
        Assert.Equal("mikkelhm", data.ProjectAlias);
        Assert.Equal("https://www.s1.umbraco.io/project/mikkelhm", data.ProjectUrl);
        Assert.Equal("Live", data.EnvironmentName);
        Assert.Equal("Deployment", data.AlertName);
        Assert.Equal("[TEST] Deployment completed for mikkelhm (Live)", data.Details);
        Assert.Equal("medium", data.Severity);
        Assert.Equal("""{ "severity": "medium" }""", data.CustomerMetadata);
        Assert.Equal(new DateTime(2026, 9, 30, 19, 18, 41, DateTimeKind.Utc).AddTicks(2585377), data.TimeFiredUtc);
        Assert.Equal(DateTimeKind.Utc, data.TimeFiredUtc.Kind);
        Assert.True(data.IsTest);
        Assert.Equal(SamplePayload, data.RawPayload);
    }

    [Fact]
    public void ToNodeData_NonUtcOffset_IsConvertedToUtc()
    {
        var data = CloudAlertMapper.ToNodeData(
            Parse("""{ "alertId": "a", "alertName": "Deployment", "projectAlias": "p", "environmentName": "Live", "timeFired": "2026-09-30T21:18:41+02:00" }"""),
            "{}");

        Assert.Equal(new DateTime(2026, 9, 30, 19, 18, 41, DateTimeKind.Utc), data.TimeFiredUtc);
        Assert.EndsWith("30-09-2026 19:18", data.Name);
    }

    [Theory]
    [InlineData("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z" }""", "")]
    [InlineData("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z", "customerMetadata": null }""", "")]
    [InlineData("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z", "customerMetadata": "text" }""", "\"text\"")]
    [InlineData("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z", "customerMetadata": { "severity": 3 } }""", """{ "severity": 3 }""")]
    [InlineData("""{ "alertId": "a", "timeFired": "2026-09-30T19:18:41Z", "customerMetadata": { "team": "core" } }""", """{ "team": "core" }""")]
    public void ToNodeData_MetadataWithoutStringSeverity_HasEmptySeverityAndKeepsRawMetadata(string json, string expectedMetadata)
    {
        var data = CloudAlertMapper.ToNodeData(Parse(json), json);

        Assert.Equal("", data.Severity);
        Assert.Equal(expectedMetadata, data.CustomerMetadata);
    }

    [Fact]
    public void ToNodeData_MissingOptionalFields_UsesEmptyStringsAndNameDefaults()
    {
        var data = CloudAlertMapper.ToNodeData(Parse("""{ "alertId": " a ", "timeFired": "2026-09-30T19:18:41Z" }"""), "{}");

        Assert.Equal("a", data.AlertId);
        Assert.Equal("", data.ProjectAlias);
        Assert.Equal("", data.ProjectUrl);
        Assert.Equal("", data.Details);
        Assert.False(data.IsTest);
        Assert.Equal("Alert – unknown (unknown) 30-09-2026 19:18", data.Name);
    }

    [Fact]
    public void BuildName_LongerThan255_IsTruncated()
    {
        var name = CloudAlertMapper.BuildName(new string('x', 300), "p", "Live", new DateTime(2026, 9, 30, 19, 18, 0, DateTimeKind.Utc));

        Assert.Equal(CloudAlertMapper.MaxNameLength, name.Length);
    }

    [Fact]
    public void ToDateTimeEditorValue_UsesTheDateTimeUnspecifiedStorageFormat()
        => Assert.Equal(
            """{"date":"2026-09-30T19:18:41+00:00","timeZone":null}""",
            CloudAlertMapper.ToDateTimeEditorValue(new DateTime(2026, 9, 30, 19, 18, 41, DateTimeKind.Utc).AddTicks(2585377)));

    [Fact]
    public void ToNodeData_WithoutTimeFired_Throws()
        => Assert.Throws<ArgumentException>(() => CloudAlertMapper.ToNodeData(Parse("""{ "alertId": "a" }"""), "{}"));
}
