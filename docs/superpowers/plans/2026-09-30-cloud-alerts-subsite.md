# Cloud Alerts Subsite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new `Cloud Alerts` subsite that receives Umbraco Cloud alert webhooks, stores each alert as a published content node, and lists them on a public, filterable page at `/cloud-alerts/`.

**Architecture:** Pure, unit-tested logic lives in `Mikkelhm.Core/CloudAlerts/` (payload parsing, mapping, secret check, ingest orchestration, filtering/paging). Umbraco-specific persistence sits behind `ICloudAlertStore` (Core, Umbraco Core APIs only). A thin API controller in `Mikkelhm.Web` exposes the webhook; a self-contained Razor view renders the list using the Core query.

**Tech Stack:** .NET 10, Umbraco CMS 18.2.0 (ModelsBuilder `SourceCodeAuto` into `src/Mikkelhm.Models`), Umbraco MCP (`umbraco-mcp`) for schema/content, xUnit 2.9.3 + NSubstitute 6.2.0, vanilla CSS/JS.

**Spec:** `docs/superpowers/specs/2026-09-30-cloud-alerts-subsite-design.md`

## Global Constraints

- Central Package Management: versions only in `Directory.Packages.props`; `PackageReference` without `Version`.
- `Mikkelhm.Core` references only `Umbraco.Cms.Core` (+ existing ASP.NET abstractions). No `Umbraco.Cms.Infrastructure`/web types in Core. Controllers go in `Mikkelhm.Web`.
- Webhook route: `POST /umbraco/api/cloud-alerts/webhook`. Auth header: `uc-webhook-auth`. Config key: `CloudAlerts:WebhookSecret` (env var `CloudAlerts__WebhookSecret`).
- The secret is never committed, never logged, never written into this repo (including docs). Local dev uses its own generated secret via `dotnet user-secrets`.
- Doc type aliases: `cloudAlertsHome`, `cloudAlert`. Doc type folder: `CloudAlerts`. Template: `CloudAlertsHome`.
- Node name format (UTC): `{alertName} – {projectAlias} ({environmentName}) {timeFired:dd-MM-yyyy HH:mm}`; max 255 chars.
- Dates shown as DD-MM-YYYY. Page size 50. Sort `timeFired` descending.
- Never `git add` anything under `docs/superpowers/`. Stage explicit paths only. Commit messages: plain sentence style (see `git log`), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Do not push.
- Work on branch `feature/cloud-alerts` (created in Task 1).
- Stop the running local site before `dotnet build` of `Mikkelhm.Web` (file locks). Start it with `dotnet run --project src/Mikkelhm.Web/Mikkelhm.Web.csproj --launch-profile UmbracoProject`; it serves `https://localhost:44385`.
- Existing code style: file-scoped namespaces, classic constructor injection with `_camelCase` fields (see `src/Mikkelhm.Web/Controllers/EllabmApiController.cs`).

## Review Focus

1. Header secret differing only by whitespace or letter case (`"secret "`, `"SECRET"`), or a whitespace-only configured secret → rejected / treated as unconfigured. Test in Task 1.
2. Garbage query strings on the listing page (`page=abc`, `page=-3`, `from=31-02-2026`, `tests=weird`) → silently fall back to defaults, never a 500. Test in Task 4.
3. `customerMetadata` that is `null`, absent, a string, or has a non-string `severity` (`{"severity": 3}`) → alert accepted, severity empty, raw metadata kept. Test in Task 2.
4. `timeFired` with a non-UTC offset (`2026-09-30T21:18:41+02:00`) → stored and named as UTC (`19:18`). Test in Task 2.
5. `projectUrl` with a non-http(s) scheme (`javascript:alert(1)`) or not a URL → rendered as plain text, never as a link. Test in Task 7.

---

### Task 1: Test project and webhook secret validation

**Files:**
- Create: `tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
- Create: `tests/Mikkelhm.Core.Tests/CloudAlerts/WebhookSecretValidatorTests.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/WebhookSecretValidator.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertsOptions.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertsConstants.cs`
- Modify: `Directory.Packages.props`, `src/Mikkelhm.sln`

**Interfaces:**
- Produces: `WebhookSecretValidator.HeaderName` (`"uc-webhook-auth"`), `WebhookSecretValidator.IsConfigured(string?) : bool`, `WebhookSecretValidator.IsValid(string? configuredSecret, string? providedValue) : bool`; `CloudAlertsOptions { const string SectionName = "CloudAlerts"; string? WebhookSecret }`; `CloudAlertsConstants.HomeAlias`, `.AlertAlias`, `.Properties.*`.

- [ ] **Step 1: Create the branch**

```bash
git checkout -b feature/cloud-alerts
```

- [ ] **Step 2: Add test package versions** to `Directory.Packages.props`, inside the existing `<ItemGroup>`, keeping alphabetical order:

```xml
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.1" />
```

- [ ] **Step 3: Create `tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Mikkelhm.Core\Mikkelhm.Core.csproj" />
  </ItemGroup>
</Project>
```

Then add it to the solution:

```bash
dotnet sln src/Mikkelhm.sln add tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --solution-folder tests
```

- [ ] **Step 4: Write the failing tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/WebhookSecretValidatorTests.cs`

```csharp
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
```

- [ ] **Step 5: Run to verify it fails**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
Expected: build FAILS with `The type or namespace name 'CloudAlerts' does not exist in the namespace 'Mikkelhm.Core'`.

- [ ] **Step 6: Implement.** `src/Mikkelhm.Core/CloudAlerts/WebhookSecretValidator.cs`:

```csharp
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
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertsOptions.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public sealed class CloudAlertsOptions
{
    public const string SectionName = "CloudAlerts";

    public string? WebhookSecret { get; set; }
}
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertsConstants.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public static class CloudAlertsConstants
{
    public const string HomeAlias = "cloudAlertsHome";
    public const string AlertAlias = "cloudAlert";

    public static class Properties
    {
        public const string AlertId = "alertId";
        public const string ProjectAlias = "projectAlias";
        public const string ProjectUrl = "projectUrl";
        public const string EnvironmentName = "environmentName";
        public const string AlertName = "alertName";
        public const string Details = "details";
        public const string Severity = "severity";
        public const string CustomerMetadata = "customerMetadata";
        public const string TimeFired = "timeFired";
        public const string IsTest = "isTest";
        public const string RawPayload = "rawPayload";
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
Expected: PASS, 15 tests.

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props src/Mikkelhm.sln tests/Mikkelhm.Core.Tests src/Mikkelhm.Core/CloudAlerts
git commit -m "Add Core test project and Cloud Alerts webhook secret validation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Payload parsing and mapping to node data

**Files:**
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertPayload.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertNodeData.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertMapper.cs`
- Test: `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertMapperTests.cs`

**Interfaces:**
- Produces:
  - `sealed record CloudAlertPayload(string? AlertId, string? ProjectAlias, string? ProjectUrl, string? EnvironmentName, DateTimeOffset? TimeFired, string? AlertName, string? Details, JsonElement? CustomerMetadata, bool IsTest)`
  - `sealed record CloudAlertNodeData(string Name, string AlertId, string ProjectAlias, string ProjectUrl, string EnvironmentName, string AlertName, string Details, string Severity, string CustomerMetadata, DateTime TimeFiredUtc, bool IsTest, string RawPayload)`
  - `CloudAlertMapper.TryParse(string rawPayload, out CloudAlertPayload? payload) : bool`
  - `CloudAlertMapper.Validate(CloudAlertPayload payload) : IReadOnlyList<string>`
  - `CloudAlertMapper.ToNodeData(CloudAlertPayload payload, string rawPayload) : CloudAlertNodeData`
  - `CloudAlertMapper.BuildName(string alertName, string projectAlias, string environmentName, DateTime timeFiredUtc) : string`
  - `CloudAlertMapper.MaxNameLength = 255`

- [ ] **Step 1: Write the failing tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertMapperTests.cs`

```csharp
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
    public void ToNodeData_WithoutTimeFired_Throws()
        => Assert.Throws<ArgumentException>(() => CloudAlertMapper.ToNodeData(Parse("""{ "alertId": "a" }"""), "{}"));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --filter FullyQualifiedName~CloudAlertMapperTests`
Expected: build FAILS with `The name 'CloudAlertMapper' does not exist in the current context`.

- [ ] **Step 3: Implement.** `src/Mikkelhm.Core/CloudAlerts/CloudAlertPayload.cs`:

```csharp
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
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertNodeData.cs`:

```csharp
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
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertMapper.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --filter FullyQualifiedName~CloudAlertMapperTests`
Expected: PASS (all tests in the class).

- [ ] **Step 5: Commit**

```bash
git add src/Mikkelhm.Core/CloudAlerts tests/Mikkelhm.Core.Tests/CloudAlerts
git commit -m "Add Cloud Alerts payload parsing and node mapping

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Ingest service

**Files:**
- Create: `src/Mikkelhm.Core/CloudAlerts/ICloudAlertStore.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/IngestResult.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/ICloudAlertIngestService.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertIngestService.cs`
- Test: `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertIngestServiceTests.cs`

**Interfaces:**
- Consumes: `CloudAlertMapper.TryParse/Validate/ToNodeData`, `CloudAlertNodeData` (Task 2).
- Produces:
  - `interface ICloudAlertStore { Task<Guid?> FindContainerKeyAsync(); Task<bool> AlertExistsAsync(Guid containerKey, string alertId); Task<Guid> CreateAsync(Guid containerKey, CloudAlertNodeData data); }`
  - `enum IngestStatus { Created, Duplicate, NoContainer, Invalid }`
  - `sealed record IngestResult(IngestStatus Status, Guid? Key, IReadOnlyList<string> Errors)` with static factories `Created(Guid)`, `Duplicate()`, `NoContainer()`, `Invalid(IReadOnlyList<string>)`
  - `interface ICloudAlertIngestService { Task<IngestResult> IngestAsync(string rawPayload); }`
  - `CloudAlertIngestService(ICloudAlertStore store, ILogger<CloudAlertIngestService> logger)`

- [ ] **Step 1: Write the failing tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertIngestServiceTests.cs`

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Mikkelhm.Core.CloudAlerts;
using NSubstitute;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertIngestServiceTests
{
    private const string Payload = """
        { "alertId": "alert-1", "projectAlias": "mikkelhm", "environmentName": "Live",
          "timeFired": "2026-09-30T19:18:41Z", "alertName": "Deployment", "isTest": true }
        """;

    private readonly ICloudAlertStore _store = Substitute.For<ICloudAlertStore>();
    private readonly CloudAlertIngestService _service;
    private readonly Guid _containerKey = Guid.NewGuid();

    public CloudAlertIngestServiceTests()
    {
        _service = new CloudAlertIngestService(_store, NullLogger<CloudAlertIngestService>.Instance);
    }

    [Fact]
    public async Task IngestAsync_NewAlert_CreatesNodeAndReturnsKey()
    {
        var createdKey = Guid.NewGuid();
        _store.FindContainerKeyAsync().Returns(_containerKey);
        _store.AlertExistsAsync(_containerKey, "alert-1").Returns(false);
        _store.CreateAsync(_containerKey, Arg.Any<CloudAlertNodeData>()).Returns(createdKey);

        var result = await _service.IngestAsync(Payload);

        Assert.Equal(IngestStatus.Created, result.Status);
        Assert.Equal(createdKey, result.Key);
        await _store.Received(1).CreateAsync(_containerKey, Arg.Is<CloudAlertNodeData>(d =>
            d.AlertId == "alert-1" && d.Name == "Deployment – mikkelhm (Live) 30-09-2026 19:18" && d.RawPayload == Payload));
    }

    [Fact]
    public async Task IngestAsync_ExistingAlertId_ReturnsDuplicateWithoutCreating()
    {
        _store.FindContainerKeyAsync().Returns(_containerKey);
        _store.AlertExistsAsync(_containerKey, "alert-1").Returns(true);

        var result = await _service.IngestAsync(Payload);

        Assert.Equal(IngestStatus.Duplicate, result.Status);
        await _store.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<CloudAlertNodeData>());
    }

    [Fact]
    public async Task IngestAsync_NoContainer_ReturnsNoContainer()
    {
        _store.FindContainerKeyAsync().Returns((Guid?)null);

        var result = await _service.IngestAsync(Payload);

        Assert.Equal(IngestStatus.NoContainer, result.Status);
        await _store.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<CloudAlertNodeData>());
    }

    [Fact]
    public async Task IngestAsync_InvalidJson_ReturnsInvalidWithoutTouchingStore()
    {
        var result = await _service.IngestAsync("not json");

        Assert.Equal(IngestStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Errors);
        await _store.DidNotReceive().FindContainerKeyAsync();
    }

    [Fact]
    public async Task IngestAsync_MissingRequiredFields_ReturnsValidationErrors()
    {
        var result = await _service.IngestAsync("""{ "projectAlias": "mikkelhm" }""");

        Assert.Equal(IngestStatus.Invalid, result.Status);
        Assert.Contains("alertId is required", result.Errors);
        Assert.Contains("timeFired is required", result.Errors);
        await _store.DidNotReceive().FindContainerKeyAsync();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --filter FullyQualifiedName~CloudAlertIngestServiceTests`
Expected: build FAILS with `The type or namespace name 'ICloudAlertStore' could not be found`.

- [ ] **Step 3: Implement.** `src/Mikkelhm.Core/CloudAlerts/ICloudAlertStore.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public interface ICloudAlertStore
{
    Task<Guid?> FindContainerKeyAsync();

    Task<bool> AlertExistsAsync(Guid containerKey, string alertId);

    Task<Guid> CreateAsync(Guid containerKey, CloudAlertNodeData data);
}
```

`src/Mikkelhm.Core/CloudAlerts/IngestResult.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public enum IngestStatus
{
    Created,
    Duplicate,
    NoContainer,
    Invalid,
}

public sealed record IngestResult(IngestStatus Status, Guid? Key, IReadOnlyList<string> Errors)
{
    public static IngestResult Created(Guid key) => new(IngestStatus.Created, key, []);

    public static IngestResult Duplicate() => new(IngestStatus.Duplicate, null, []);

    public static IngestResult NoContainer() => new(IngestStatus.NoContainer, null, []);

    public static IngestResult Invalid(IReadOnlyList<string> errors) => new(IngestStatus.Invalid, null, errors);
}
```

`src/Mikkelhm.Core/CloudAlerts/ICloudAlertIngestService.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public interface ICloudAlertIngestService
{
    Task<IngestResult> IngestAsync(string rawPayload);
}
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertIngestService.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace Mikkelhm.Core.CloudAlerts;

public sealed class CloudAlertIngestService : ICloudAlertIngestService
{
    private readonly ICloudAlertStore _store;
    private readonly ILogger<CloudAlertIngestService> _logger;

    public CloudAlertIngestService(ICloudAlertStore store, ILogger<CloudAlertIngestService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<IngestResult> IngestAsync(string rawPayload)
    {
        if (!CloudAlertMapper.TryParse(rawPayload, out var payload))
        {
            _logger.LogWarning("Cloud alert rejected: body is not a valid alert payload");
            return IngestResult.Invalid(["Body is not a valid alert payload"]);
        }

        var errors = CloudAlertMapper.Validate(payload!);
        if (errors.Count > 0)
        {
            _logger.LogWarning("Cloud alert rejected: {Errors}", string.Join(", ", errors));
            return IngestResult.Invalid(errors);
        }

        var data = CloudAlertMapper.ToNodeData(payload!, rawPayload);

        var containerKey = await _store.FindContainerKeyAsync();
        if (containerKey is null)
        {
            _logger.LogError("Cloud alert {AlertId} not stored: no {Alias} node exists", data.AlertId, CloudAlertsConstants.HomeAlias);
            return IngestResult.NoContainer();
        }

        if (await _store.AlertExistsAsync(containerKey.Value, data.AlertId))
        {
            _logger.LogInformation("Cloud alert {AlertId} already stored, skipping", data.AlertId);
            return IngestResult.Duplicate();
        }

        var key = await _store.CreateAsync(containerKey.Value, data);
        _logger.LogInformation("Cloud alert {AlertId} stored as {Key}", data.AlertId, key);
        return IngestResult.Created(key);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
Expected: PASS (all tests, including Tasks 1–2).

- [ ] **Step 5: Commit**

```bash
git add src/Mikkelhm.Core/CloudAlerts tests/Mikkelhm.Core.Tests/CloudAlerts
git commit -m "Add Cloud Alerts ingest service

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Listing query (filtering, facets, paging)

**Files:**
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertView.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertFilter.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertsQuery.cs`
- Test: `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertFilterTests.cs`
- Test: `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertsQueryTests.cs`

**Interfaces:**
- Produces:
  - `sealed record CloudAlertView(Guid Key, string AlertId, string ProjectAlias, string ProjectUrl, string EnvironmentName, string AlertName, string Details, string Severity, string CustomerMetadata, DateTime TimeFiredUtc, bool IsTest, string RawPayload)`
  - `enum TestAlertMode { Show, Hide, Only }`
  - `sealed record CloudAlertFilter { string? Project; string? Environment; string? AlertName; string? Severity; string? Search; DateOnly? From; DateOnly? To; TestAlertMode Tests = Show; int Page = 1; int PageSize = 50 }` (init properties) with `static CloudAlertFilter FromQuery(IReadOnlyDictionary<string, string?> query)` and `string ToQueryString()`. Query keys: `project`, `env`, `alert`, `severity`, `q`, `from`, `to` (`yyyy-MM-dd`), `tests` (`show|hide|only`), `page`.
  - `sealed record SeverityCount(string Severity, int Count)`
  - `sealed record CloudAlertFacets(IReadOnlyList<string> Projects, IReadOnlyList<string> Environments, IReadOnlyList<string> AlertNames, IReadOnlyList<string> Severities)`
  - `sealed record CloudAlertsPage(IReadOnlyList<CloudAlertView> Items, int TotalCount, int Page, int TotalPages, IReadOnlyList<SeverityCount> SeverityCounts, CloudAlertFacets Facets)`
  - `CloudAlertsQuery.Run(IEnumerable<CloudAlertView> alerts, CloudAlertFilter filter) : CloudAlertsPage`

Semantics (pin these in tests):
- String filters: exact match, case-insensitive. Search: `Details` contains, case-insensitive.
- Date range: UTC days, both ends inclusive (`From` 00:00:00 ≤ t < `To`+1 day 00:00:00).
- Facets: from **all** alerts (unfiltered), distinct case-insensitive, sorted, empty values excluded.
- Severity counts: over alerts matching every filter **except** severity; empty severity excluded; ordered by count desc, then name.
- Page < 1 → 1; page beyond last → last; `TotalPages` ≥ 1.

- [ ] **Step 1: Write the failing filter tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertFilterTests.cs`

```csharp
using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertFilterTests
{
    private static CloudAlertFilter From(params (string Key, string? Value)[] pairs)
        => CloudAlertFilter.FromQuery(pairs.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void FromQuery_Empty_ReturnsDefaults()
    {
        var filter = From();

        Assert.Equal(new CloudAlertFilter(), filter);
        Assert.Equal(TestAlertMode.Show, filter.Tests);
        Assert.Equal(1, filter.Page);
        Assert.Equal(50, filter.PageSize);
    }

    [Fact]
    public void FromQuery_AllValues_AreParsedAndTrimmed()
    {
        var filter = From(
            ("project", " mikkelhm "), ("env", "Live"), ("alert", "Deployment"), ("severity", "medium"),
            ("q", "completed"), ("from", "2026-09-01"), ("to", "2026-09-30"), ("tests", "HIDE"), ("page", "3"));

        Assert.Equal("mikkelhm", filter.Project);
        Assert.Equal("Live", filter.Environment);
        Assert.Equal("Deployment", filter.AlertName);
        Assert.Equal("medium", filter.Severity);
        Assert.Equal("completed", filter.Search);
        Assert.Equal(new DateOnly(2026, 9, 1), filter.From);
        Assert.Equal(new DateOnly(2026, 9, 30), filter.To);
        Assert.Equal(TestAlertMode.Hide, filter.Tests);
        Assert.Equal(3, filter.Page);
    }

    [Theory]
    [InlineData("page", "abc")]
    [InlineData("page", "-3")]
    [InlineData("page", "0")]
    [InlineData("from", "31-02-2026")]
    [InlineData("to", "2026-02-31")]
    [InlineData("tests", "weird")]
    [InlineData("tests", "7")]
    [InlineData("project", "   ")]
    public void FromQuery_GarbageValues_FallBackToDefaults(string key, string value)
        => Assert.Equal(new CloudAlertFilter(), From((key, value)));

    [Fact]
    public void ToQueryString_Defaults_IsEmpty()
        => Assert.Equal("", new CloudAlertFilter().ToQueryString());

    [Fact]
    public void ToQueryString_EscapesValuesAndOmitsDefaults()
    {
        var filter = new CloudAlertFilter
        {
            Project = "my project", Search = "a&b", From = new DateOnly(2026, 9, 1), Tests = TestAlertMode.Only, Page = 2,
        };

        Assert.Equal("?project=my%20project&q=a%26b&from=2026-09-01&tests=only&page=2", filter.ToQueryString());
    }

    [Fact]
    public void ToQueryString_RoundTripsThroughFromQuery()
    {
        var original = new CloudAlertFilter
        {
            Project = "p", Environment = "Live", AlertName = "Deployment", Severity = "high", Search = "x y",
            From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30), Tests = TestAlertMode.Hide, Page = 4,
        };

        var query = original.ToQueryString().TrimStart('?').Split('&')
            .Select(part => part.Split('='))
            .ToDictionary(kv => kv[0], kv => (string?)Uri.UnescapeDataString(kv[1]));

        Assert.Equal(original, CloudAlertFilter.FromQuery(query));
    }
}
```

- [ ] **Step 2: Write the failing query tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertsQueryTests.cs`

```csharp
using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertsQueryTests
{
    private static readonly DateTime Noon = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static CloudAlertView Alert(
        string project = "mikkelhm", string env = "Live", string alert = "Deployment", string severity = "medium",
        string details = "Deployment completed", DateTime? time = null, bool isTest = false)
        => new(Guid.NewGuid(), Guid.NewGuid().ToString(), project, $"https://www.s1.umbraco.io/project/{project}",
            env, alert, details, severity, "", time ?? Noon, isTest, "{}");

    [Fact]
    public void Run_NoFilter_ReturnsAllNewestFirst()
    {
        var older = Alert(time: Noon.AddHours(-1));
        var newer = Alert(time: Noon.AddHours(1));

        var page = CloudAlertsQuery.Run([older, newer], new CloudAlertFilter());

        Assert.Equal([newer, older], page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public void Run_StringFilters_MatchExactlyIgnoringCase()
    {
        var match = Alert(project: "mikkelhm", env: "Live", alert: "Deployment", severity: "high");
        var alerts = new[]
        {
            match,
            Alert(project: "other", env: "Live", alert: "Deployment", severity: "high"),
            Alert(project: "mikkelhm", env: "Development", alert: "Deployment", severity: "high"),
            Alert(project: "mikkelhm", env: "Live", alert: "Upgrade", severity: "high"),
            Alert(project: "mikkelhm", env: "Live", alert: "Deployment", severity: "low"),
            Alert(project: "mikkelhm-old", env: "Live", alert: "Deployment", severity: "high"),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter
        {
            Project = "MIKKELHM", Environment = "live", AlertName = "deployment", Severity = "HIGH",
        });

        Assert.Equal([match], page.Items);
    }

    [Fact]
    public void Run_Search_MatchesDetailsIgnoringCase()
    {
        var match = Alert(details: "[TEST] Deployment Completed for mikkelhm");

        var page = CloudAlertsQuery.Run([match, Alert(details: "Upgrade failed")], new CloudAlertFilter { Search = "completed" });

        Assert.Equal([match], page.Items);
    }

    [Fact]
    public void Run_DateRange_IncludesBothWholeDays()
    {
        var startOfDay = Alert(time: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));
        var endOfDay = Alert(time: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc));
        var dayBefore = Alert(time: new DateTime(2026, 9, 29, 23, 59, 59, DateTimeKind.Utc));
        var dayAfter = Alert(time: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        var page = CloudAlertsQuery.Run(
            [startOfDay, endOfDay, dayBefore, dayAfter],
            new CloudAlertFilter { From = new DateOnly(2026, 9, 30), To = new DateOnly(2026, 9, 30) });

        Assert.Equal([endOfDay, startOfDay], page.Items);
    }

    [Theory]
    [InlineData(TestAlertMode.Show, 2)]
    [InlineData(TestAlertMode.Hide, 1)]
    [InlineData(TestAlertMode.Only, 1)]
    public void Run_TestsMode_FiltersTestAlerts(TestAlertMode mode, int expectedCount)
    {
        var real = Alert(isTest: false);
        var test = Alert(isTest: true);

        var page = CloudAlertsQuery.Run([real, test], new CloudAlertFilter { Tests = mode });

        Assert.Equal(expectedCount, page.TotalCount);
        if (mode == TestAlertMode.Hide) Assert.Equal([real], page.Items);
        if (mode == TestAlertMode.Only) Assert.Equal([test], page.Items);
    }

    [Fact]
    public void Run_Paging_ReturnsRequestedPage()
    {
        var alerts = Enumerable.Range(0, 120).Select(i => Alert(time: Noon.AddMinutes(-i))).ToList();

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Page = 3 });

        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.Page);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(alerts[100], page.Items[0]);
    }

    [Fact]
    public void Run_PageBeyondLast_IsClampedToLastPage()
    {
        var alerts = Enumerable.Range(0, 60).Select(i => Alert(time: Noon.AddMinutes(-i))).ToList();

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Page = 99 });

        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.Items.Count);
    }

    [Fact]
    public void Run_NoAlerts_ReturnsEmptySinglePage()
    {
        var page = CloudAlertsQuery.Run([], new CloudAlertFilter { Page = 5 });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.SeverityCounts);
    }

    [Fact]
    public void Run_SeverityCounts_IgnoreSeverityFilterButRespectOthers()
    {
        var alerts = new[]
        {
            Alert(severity: "low"), Alert(severity: "low"), Alert(severity: "HIGH"), Alert(severity: "high"),
            Alert(severity: "medium"), Alert(severity: ""), Alert(project: "other", severity: "medium"),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Project = "mikkelhm", Severity = "low" });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(3, page.SeverityCounts.Count);
        Assert.Equal(2, page.SeverityCounts.Single(s => s.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)).Count);
        Assert.Equal(new SeverityCount("low", 2), page.SeverityCounts.Single(s => s.Severity == "low"));
        Assert.Equal(new SeverityCount("medium", 1), page.SeverityCounts.Last());
    }

    [Fact]
    public void Run_Facets_ComeFromAllAlertsDistinctSortedWithoutEmpty()
    {
        var alerts = new[]
        {
            Alert(project: "zeta", env: "Live", alert: "Upgrade", severity: "low"),
            Alert(project: "alpha", env: "Development", alert: "Deployment", severity: ""),
            Alert(project: "Alpha", env: "Live", alert: "Deployment", severity: "high", isTest: true),
        };

        var page = CloudAlertsQuery.Run(alerts, new CloudAlertFilter { Project = "zeta", Tests = TestAlertMode.Hide });

        Assert.Equal(["alpha", "zeta"], page.Facets.Projects, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["Development", "Live"], page.Facets.Environments);
        Assert.Equal(["Deployment", "Upgrade"], page.Facets.AlertNames);
        Assert.Equal(["high", "low"], page.Facets.Severities);
    }
}
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --filter "FullyQualifiedName~CloudAlertFilterTests|FullyQualifiedName~CloudAlertsQueryTests"`
Expected: build FAILS with `The type or namespace name 'CloudAlertFilter' could not be found`.

- [ ] **Step 4: Implement.** `src/Mikkelhm.Core/CloudAlerts/CloudAlertView.cs`:

```csharp
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
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertFilter.cs`:

```csharp
using System.Globalization;

namespace Mikkelhm.Core.CloudAlerts;

public enum TestAlertMode
{
    Show,
    Hide,
    Only,
}

public sealed record CloudAlertFilter
{
    public const int DefaultPageSize = 50;
    private const string DateFormat = "yyyy-MM-dd";

    public string? Project { get; init; }
    public string? Environment { get; init; }
    public string? AlertName { get; init; }
    public string? Severity { get; init; }
    public string? Search { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public TestAlertMode Tests { get; init; } = TestAlertMode.Show;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public static CloudAlertFilter FromQuery(IReadOnlyDictionary<string, string?> query)
    {
        string? Get(string key)
            => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        return new CloudAlertFilter
        {
            Project = Get("project"),
            Environment = Get("env"),
            AlertName = Get("alert"),
            Severity = Get("severity"),
            Search = Get("q"),
            From = ParseDate(Get("from")),
            To = ParseDate(Get("to")),
            Tests = ParseTests(Get("tests")),
            Page = int.TryParse(Get("page"), NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page > 0 ? page : 1,
        };
    }

    public string ToQueryString()
    {
        var parts = new List<string>();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        Add("project", Project);
        Add("env", Environment);
        Add("alert", AlertName);
        Add("severity", Severity);
        Add("q", Search);
        Add("from", From?.ToString(DateFormat, CultureInfo.InvariantCulture));
        Add("to", To?.ToString(DateFormat, CultureInfo.InvariantCulture));
        if (Tests != TestAlertMode.Show)
        {
            Add("tests", Tests.ToString().ToLowerInvariant());
        }

        if (Page > 1)
        {
            Add("page", Page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static TestAlertMode ParseTests(string? value)
        => value?.ToLowerInvariant() switch
        {
            "hide" => TestAlertMode.Hide,
            "only" => TestAlertMode.Only,
            _ => TestAlertMode.Show,
        };
}
```

`src/Mikkelhm.Core/CloudAlerts/CloudAlertsQuery.cs`:

```csharp
namespace Mikkelhm.Core.CloudAlerts;

public sealed record SeverityCount(string Severity, int Count);

public sealed record CloudAlertFacets(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Environments,
    IReadOnlyList<string> AlertNames,
    IReadOnlyList<string> Severities);

public sealed record CloudAlertsPage(
    IReadOnlyList<CloudAlertView> Items,
    int TotalCount,
    int Page,
    int TotalPages,
    IReadOnlyList<SeverityCount> SeverityCounts,
    CloudAlertFacets Facets);

public static class CloudAlertsQuery
{
    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    public static CloudAlertsPage Run(IEnumerable<CloudAlertView> alerts, CloudAlertFilter filter)
    {
        var all = alerts.ToList();

        var facets = new CloudAlertFacets(
            Distinct(all, a => a.ProjectAlias),
            Distinct(all, a => a.EnvironmentName),
            Distinct(all, a => a.AlertName),
            Distinct(all, a => a.Severity));

        var matchingExceptSeverity = all.Where(a => MatchesAllButSeverity(a, filter)).ToList();

        var severityCounts = matchingExceptSeverity
            .Where(a => a.Severity.Length > 0)
            .GroupBy(a => a.Severity, IgnoreCase)
            .Select(g => new SeverityCount(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Severity, IgnoreCase)
            .ToList();

        var filtered = matchingExceptSeverity
            .Where(a => Matches(a.Severity, filter.Severity))
            .OrderByDescending(a => a.TimeFiredUtc)
            .ThenBy(a => a.Key)
            .ToList();

        var pageSize = Math.Max(1, filter.PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CloudAlertsPage(items, filtered.Count, page, totalPages, severityCounts, facets);
    }

    private static bool MatchesAllButSeverity(CloudAlertView alert, CloudAlertFilter filter)
        => Matches(alert.ProjectAlias, filter.Project)
           && Matches(alert.EnvironmentName, filter.Environment)
           && Matches(alert.AlertName, filter.AlertName)
           && (filter.Search is null || alert.Details.Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
           && (filter.From is null || alert.TimeFiredUtc >= filter.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
           && (filter.To is null || alert.TimeFiredUtc < filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
           && filter.Tests switch
           {
               TestAlertMode.Hide => !alert.IsTest,
               TestAlertMode.Only => alert.IsTest,
               _ => true,
           };

    private static bool Matches(string value, string? wanted)
        => wanted is null || string.Equals(value, wanted, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Distinct(IEnumerable<CloudAlertView> alerts, Func<CloudAlertView, string> selector)
        => alerts.Select(selector)
            .Where(v => v.Length > 0)
            .Distinct(IgnoreCase)
            .Order(IgnoreCase)
            .ToList();
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```bash
git add src/Mikkelhm.Core/CloudAlerts tests/Mikkelhm.Core.Tests/CloudAlerts
git commit -m "Add Cloud Alerts listing query with filters, facets and paging

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Umbraco schema and content (via Umbraco MCP)

No unit tests: the deliverable is schema + content, verified through the MCP and the regenerated models building.

**Files (generated by Umbraco, commit them):**
- Create: `src/Mikkelhm.Web/umbraco/Deploy/Revision/*.uda` (new doc types, data type, template; updated `website` doc type)
- Create: `src/Mikkelhm.Models/CloudAlert.generated.cs`, `src/Mikkelhm.Models/CloudAlertsHome.generated.cs`
- Create: the template view Umbraco writes for `CloudAlertsHome` (expected `src/Mikkelhm.Web/Views/cloudAlertsHome.cshtml`; note the actual filename for Task 7)

**Interfaces:**
- Consumes: aliases from `CloudAlertsConstants` (Task 1).
- Produces: ModelsBuilder classes `Mikkelhm.Models.CloudAlertsHome` (with `sEOSection` properties incl. `MetaTitle`) and `Mikkelhm.Models.CloudAlert` with properties `AlertId`, `ProjectAlias`, `ProjectUrl`, `EnvironmentName`, `AlertName`, `Details`, `Severity`, `CustomerMetadata` (string), `TimeFired` (DateTime), `IsTest` (bool), `RawPayload` (string). A published content tree `Cloud Alerts` (website) → `Cloud Alerts` (cloudAlertsHome) at `/cloud-alerts/`.

- [ ] **Step 1: Start the site and confirm the MCP is connected.** Start the site (see Global Constraints). Load MCP tools with ToolSearch (`select:mcp__umbraco-mcp__get-all-document-types,...`). If `umbraco-mcp` is disconnected, ask Mikkel to run `/mcp`.

- [ ] **Step 2: Look up existing data type ids.** Use `get-all-data-types` / `find-data-type` to find the ids of: `Textstring` (editor `Umbraco.TextBox`), `Textarea` (`Umbraco.TextArea`), `True/false` (`Umbraco.TrueFalse`), and a date-with-time type whose editor is `Umbraco.DateTime` (e.g. `Date Picker with time`). If no `Umbraco.DateTime` data type exists, create one named `Date Picker with time` with `create-data-type`. Also read the collection data type used by `blogPostRepository` (`get-data-type` id `9fea27f9-3228-4586-99bc-1e04e51bb54a`) to copy its configuration shape.

- [ ] **Step 3: Create the collection data type** `Collection - Cloud Alerts` (editor `Umbraco.ListView`, same config shape as Step 2's), with columns (`includeProperties`): Name (system), `alertName`, `projectAlias`, `environmentName`, `severity`, `timeFired`, `isTest`; order by `timeFired` descending; page size 50.

- [ ] **Step 4: Create the doc type folder** `CloudAlerts` at root with `create-document-type-folder`.

- [ ] **Step 5: Create doc type `cloudAlert`** (name `Cloud Alert`, icon `icon-alert`, in folder `CloudAlerts`, not an element, not allowed at root, no templates) with one group `Alert` and properties, in this order:

| Alias | Name | Data type |
|-------|------|-----------|
| `alertId` | Alert ID | Textstring |
| `alertName` | Alert name | Textstring |
| `projectAlias` | Project alias | Textstring |
| `projectUrl` | Project URL | Textstring |
| `environmentName` | Environment | Textstring |
| `severity` | Severity | Textstring |
| `timeFired` | Time fired (UTC) | Date Picker with time |
| `isTest` | Test alert | True/false |
| `details` | Details | Textarea |
| `customerMetadata` | Customer metadata (JSON) | Textarea |
| `rawPayload` | Raw payload (JSON) | Textarea |

- [ ] **Step 6: Create the template** with `create-template`: name `CloudAlertsHome`, alias `cloudAlertsHome`, content:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{
    Layout = null;
}
```

- [ ] **Step 7: Create doc type `cloudAlertsHome`** (name `Cloud Alerts Home`, icon `icon-alert-alt`, folder `CloudAlerts`), composition `sEOSection` (`81391434-77a3-4379-a843-10f0ce4685ea`), allowed template + default template `CloudAlertsHome`, allowed child `cloudAlert` only, collection = `Collection - Cloud Alerts`. No own properties.

- [ ] **Step 8: Allow it under `website`.** `get-document-type-by-id` for `website` (`79f8c2bf-361c-4c3b-ad27-e700b49d9d61`), then `update-document-type` adding `cloudAlertsHome` to its allowed children, keeping every existing allowed child.

- [ ] **Step 9: Create and publish content.** `create-document` a root `website` node named `Cloud Alerts`; publish it. Under it, `create-document` a `cloudAlertsHome` node named `Cloud Alerts`; confirm its template is `CloudAlertsHome` (fix with `update-document` if null); publish it. Verify with `get-document-urls` that the home node URL is `/cloud-alerts/`.

- [ ] **Step 10: Verify generated models and build.** Confirm `src/Mikkelhm.Models/CloudAlert.generated.cs` and `CloudAlertsHome.generated.cs` exist with the property names/types listed under Interfaces (`TimeFired` is `DateTime`, `IsTest` is `bool`). If they don't exist, trigger `post-models-builder-build`. Stop the site, then:

Run: `dotnet build src/Mikkelhm.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Commit** (check `git status` first; stage only these paths):

```bash
git add src/Mikkelhm.Web/umbraco/Deploy/Revision src/Mikkelhm.Models src/Mikkelhm.Web/Views/cloudAlertsHome.cshtml
git commit -m "Add Cloud Alerts document types, collection and template

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Umbraco store, composer and webhook controller

**Files:**
- Create: `src/Mikkelhm.Core/CloudAlerts/UmbracoCloudAlertStore.cs`
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertsComposer.cs`
- Create: `src/Mikkelhm.Web/Controllers/CloudAlertsWebhookController.cs`
- Modify: `src/Mikkelhm.Web/Mikkelhm.Web.csproj` (adds `UserSecretsId` via `dotnet user-secrets init`)

**Interfaces:**
- Consumes: `ICloudAlertStore`, `ICloudAlertIngestService`, `IngestStatus`, `CloudAlertNodeData` (Task 3), `WebhookSecretValidator`, `CloudAlertsOptions`, `CloudAlertsConstants` (Task 1).
- Produces: `POST /umbraco/api/cloud-alerts/webhook` returning 401 / 400 / 413 / 503 / 200 `{status:"duplicate"}` / 201 `{status:"created", key}` / 500.

The store and controller are thin adapters over Umbraco/ASP.NET; they are verified end-to-end in Step 5 rather than with unit tests (the logic they delegate to is covered in Tasks 1–3).

- [ ] **Step 1: Implement the store.** `src/Mikkelhm.Core/CloudAlerts/UmbracoCloudAlertStore.cs`:

```csharp
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;

namespace Mikkelhm.Core.CloudAlerts;

public sealed class UmbracoCloudAlertStore : ICloudAlertStore
{
    private readonly IDocumentNavigationQueryService _navigation;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IContentService _contentService;

    public UmbracoCloudAlertStore(
        IDocumentNavigationQueryService navigation,
        IPublishedContentCache publishedContentCache,
        IContentService contentService)
    {
        _navigation = navigation;
        _publishedContentCache = publishedContentCache;
        _contentService = contentService;
    }

    public Task<Guid?> FindContainerKeyAsync()
    {
        if (_navigation.TryGetRootKeys(out var rootKeys))
        {
            foreach (var rootKey in rootKeys)
            {
                if (_navigation.TryGetDescendantsKeysOfType(rootKey, CloudAlertsConstants.HomeAlias, out var homeKeys)
                    && homeKeys.Any())
                {
                    return Task.FromResult<Guid?>(homeKeys.First());
                }
            }
        }

        return Task.FromResult<Guid?>(null);
    }

    public async Task<bool> AlertExistsAsync(Guid containerKey, string alertId)
    {
        if (!_navigation.TryGetChildrenKeysOfType(containerKey, CloudAlertsConstants.AlertAlias, out var childKeys))
        {
            return false;
        }

        foreach (var childKey in childKeys)
        {
            var child = await _publishedContentCache.GetByIdAsync(childKey);
            var existingId = child?.GetProperty(CloudAlertsConstants.Properties.AlertId)?.GetValue() as string;
            if (string.Equals(existingId, alertId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public Task<Guid> CreateAsync(Guid containerKey, CloudAlertNodeData data)
    {
        var content = _contentService.Create(data.Name, containerKey, CloudAlertsConstants.AlertAlias);
        content.SetValue(CloudAlertsConstants.Properties.AlertId, data.AlertId);
        content.SetValue(CloudAlertsConstants.Properties.ProjectAlias, data.ProjectAlias);
        content.SetValue(CloudAlertsConstants.Properties.ProjectUrl, data.ProjectUrl);
        content.SetValue(CloudAlertsConstants.Properties.EnvironmentName, data.EnvironmentName);
        content.SetValue(CloudAlertsConstants.Properties.AlertName, data.AlertName);
        content.SetValue(CloudAlertsConstants.Properties.Details, data.Details);
        content.SetValue(CloudAlertsConstants.Properties.Severity, data.Severity);
        content.SetValue(CloudAlertsConstants.Properties.CustomerMetadata, data.CustomerMetadata);
        content.SetValue(CloudAlertsConstants.Properties.TimeFired, data.TimeFiredUtc);
        content.SetValue(CloudAlertsConstants.Properties.IsTest, data.IsTest);
        content.SetValue(CloudAlertsConstants.Properties.RawPayload, data.RawPayload);

        var saveResult = _contentService.Save(content);
        if (!saveResult.Success)
        {
            throw new InvalidOperationException($"Saving cloud alert {data.AlertId} failed: {saveResult.Result}");
        }

        var publishResult = _contentService.Publish(content, ["*"]);
        if (!publishResult.Success)
        {
            throw new InvalidOperationException($"Publishing cloud alert {data.AlertId} failed: {publishResult.Result}");
        }

        return Task.FromResult(content.Key);
    }
}
```

- [ ] **Step 2: Register services.** `src/Mikkelhm.Core/CloudAlerts/CloudAlertsComposer.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Mikkelhm.Core.CloudAlerts;

public sealed class CloudAlertsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<CloudAlertsOptions>(builder.Config.GetSection(CloudAlertsOptions.SectionName));
        builder.Services.AddScoped<ICloudAlertStore, UmbracoCloudAlertStore>();
        builder.Services.AddScoped<ICloudAlertIngestService, CloudAlertIngestService>();
    }
}
```

- [ ] **Step 3: Implement the controller.** `src/Mikkelhm.Web/Controllers/CloudAlertsWebhookController.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Web.Controllers;

[ApiController]
[Route("umbraco/api/cloud-alerts")]
public class CloudAlertsWebhookController : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    private readonly ICloudAlertIngestService _ingestService;
    private readonly IOptionsMonitor<CloudAlertsOptions> _options;
    private readonly ILogger<CloudAlertsWebhookController> _logger;

    public CloudAlertsWebhookController(
        ICloudAlertIngestService ingestService,
        IOptionsMonitor<CloudAlertsOptions> options,
        ILogger<CloudAlertsWebhookController> logger)
    {
        _ingestService = ingestService;
        _options = options;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Webhook()
    {
        var secret = _options.CurrentValue.WebhookSecret;
        if (!WebhookSecretValidator.IsConfigured(secret))
        {
            _logger.LogWarning("Cloud alert webhook rejected: {Setting} is not configured", "CloudAlerts:WebhookSecret");
            return Unauthorized();
        }

        var provided = Request.Headers[WebhookSecretValidator.HeaderName].FirstOrDefault();
        if (!WebhookSecretValidator.IsValid(secret, provided))
        {
            _logger.LogWarning("Cloud alert webhook rejected: missing or invalid {Header} header", WebhookSecretValidator.HeaderName);
            return Unauthorized();
        }

        string body;
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            body = await reader.ReadToEndAsync();
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            _logger.LogWarning("Cloud alert webhook rejected: body larger than {MaxBytes} bytes", MaxBodyBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            var result = await _ingestService.IngestAsync(body);
            return result.Status switch
            {
                IngestStatus.Created => StatusCode(StatusCodes.Status201Created, new { status = "created", key = result.Key }),
                IngestStatus.Duplicate => Ok(new { status = "duplicate" }),
                IngestStatus.NoContainer => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Cloud Alerts is not set up" }),
                _ => BadRequest(new { error = "Invalid payload", details = result.Errors }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud alert webhook failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal error" });
        }
    }
}
```

- [ ] **Step 4: Build.** Stop the site if running.

Run: `dotnet build src/Mikkelhm.sln`
Expected: Build succeeded, 0 errors. If `Configure<CloudAlertsOptions>(IConfiguration)` doesn't resolve, add `using Microsoft.Extensions.Configuration;` (the `Microsoft.Extensions.Options.ConfigurationExtensions` package already flows in transitively via `Umbraco.Cms.Core`; do not add a package reference).

- [ ] **Step 5: End-to-end check locally.** Configure a **local-only** secret (never the Live value):

```bash
dotnet user-secrets init --project src/Mikkelhm.Web/Mikkelhm.Web.csproj
LOCAL_SECRET=$(python -c "import secrets;print(secrets.token_urlsafe(32))")
dotnet user-secrets set "CloudAlerts:WebhookSecret" "$LOCAL_SECRET" --project src/Mikkelhm.Web/Mikkelhm.Web.csproj
```

Start the site, then run (`-k` for the dev certificate):

```bash
URL=https://localhost:44385/umbraco/api/cloud-alerts/webhook
BODY='{"alertId":"e2e-0001","projectAlias":"mikkelhm","projectUrl":"https://www.s1.umbraco.io/project/mikkelhm","environmentName":"Live","timeFired":"2026-09-30T19:18:41.2585377Z","alertName":"Deployment","details":"[TEST] Deployment completed for mikkelhm (Live)","customerMetadata":{"severity":"medium"},"isTest":true}'
curl -sk -o /dev/null -w "no header: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -d "$BODY"
curl -sk -o /dev/null -w "wrong header: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -H "uc-webhook-auth: wrong" -d "$BODY"
curl -sk -w "  first: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -H "uc-webhook-auth: $LOCAL_SECRET" -d "$BODY"
curl -sk -w "  repeat: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -H "uc-webhook-auth: $LOCAL_SECRET" -d "$BODY"
curl -sk -w "  bad json: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -H "uc-webhook-auth: $LOCAL_SECRET" -d "not json"
curl -sk -w "  missing fields: %{http_code}\n" -X POST $URL -H "Content-Type: application/json" -H "uc-webhook-auth: $LOCAL_SECRET" -d '{"projectAlias":"x"}'
```

Expected, in order: `401`, `401`, `{"status":"created","key":"<guid>"}  first: 201`, `{"status":"duplicate"}  repeat: 200`, `400`, `400`.

Then verify with the MCP (`get-document-children` of the `Cloud Alerts` home node): one published `cloudAlert` named `Deployment – mikkelhm (Live) 30-09-2026 19:18`, `timeFired` round-trips as 19:18:41 UTC, `isTest` true, `severity` `medium`. If `timeFired` comes back shifted by the server offset, fix the store to write the value the data type expects and re-run this step.

Seed a few more varied alerts for Task 7 (different `alertId`, `environmentName` `Development`, `alertName` `Upgrade`, severities `high`/`low`, one with `"isTest": false`, one without `customerMetadata`) using the same curl with edited `BODY`.

- [ ] **Step 6: Commit**

```bash
git add src/Mikkelhm.Core/CloudAlerts src/Mikkelhm.Web/Controllers/CloudAlertsWebhookController.cs src/Mikkelhm.Web/Mikkelhm.Web.csproj
git commit -m "Add Cloud Alerts webhook endpoint and Umbraco content store

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Listing page

**Files:**
- Create: `src/Mikkelhm.Core/CloudAlerts/CloudAlertDisplay.cs`
- Test: `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertDisplayTests.cs`
- Modify: `src/Mikkelhm.Web/Views/cloudAlertsHome.cshtml` (the template file from Task 5)
- Create: `src/Mikkelhm.Web/wwwroot/cloudalerts/cloudalerts.css`
- Create: `src/Mikkelhm.Web/wwwroot/cloudalerts/cloudalerts.js`

**Interfaces:**
- Consumes: `CloudAlertFilter`, `CloudAlertsQuery`, `CloudAlertView`, `CloudAlertsPage`, `TestAlertMode` (Task 4); `Mikkelhm.Models.CloudAlertsHome`, `Mikkelhm.Models.CloudAlert` (Task 5).
- Produces: `CloudAlertDisplay.SafeHttpUrl(string?) : string?`, `CloudAlertDisplay.SeverityClass(string) : string`, `CloudAlertDisplay.PrettyJson(string) : string`.

- [ ] **Step 1: Write the failing tests** in `tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertDisplayTests.cs`

```csharp
using Mikkelhm.Core.CloudAlerts;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class CloudAlertDisplayTests
{
    [Theory]
    [InlineData("https://www.s1.umbraco.io/project/mikkelhm", "https://www.s1.umbraco.io/project/mikkelhm")]
    [InlineData("http://example.com/x", "http://example.com/x")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("JAVASCRIPT:alert(1)", null)]
    [InlineData("data:text/html,hi", null)]
    [InlineData("/relative/path", null)]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SafeHttpUrl_OnlyAllowsAbsoluteHttpAndHttps(string? input, string? expected)
        => Assert.Equal(expected, CloudAlertDisplay.SafeHttpUrl(input));

    [Theory]
    [InlineData("critical", "sev-critical")]
    [InlineData("HIGH", "sev-high")]
    [InlineData("medium", "sev-medium")]
    [InlineData("Low", "sev-low")]
    [InlineData("", "sev-none")]
    [InlineData("\"><script>", "sev-other")]
    public void SeverityClass_MapsKnownValuesAndNeverEchoesInput(string severity, string expected)
        => Assert.Equal(expected, CloudAlertDisplay.SeverityClass(severity));

    [Fact]
    public void PrettyJson_Valid_IsIndented()
        => Assert.Equal("{\n  \"a\": 1\n}", CloudAlertDisplay.PrettyJson("{\"a\":1}").ReplaceLineEndings("\n"));

    [Fact]
    public void PrettyJson_KeepsNonAsciiAndAngleBracketsReadable()
        => Assert.Equal("{\n  \"d\": \"a <b> – c\"\n}", CloudAlertDisplay.PrettyJson("{\"d\":\"a <b> – c\"}").ReplaceLineEndings("\n"));

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void PrettyJson_Invalid_ReturnsInputUnchanged(string input)
        => Assert.Equal(input, CloudAlertDisplay.PrettyJson(input));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj --filter FullyQualifiedName~CloudAlertDisplayTests`
Expected: build FAILS with `The name 'CloudAlertDisplay' does not exist in the current context`.

- [ ] **Step 3: Implement.** `src/Mikkelhm.Core/CloudAlerts/CloudAlertDisplay.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Mikkelhm.Core.Tests/Mikkelhm.Core.Tests.csproj`
Expected: PASS (all tests).

- [ ] **Step 5: Write the view.** Replace the whole content of `src/Mikkelhm.Web/Views/cloudAlertsHome.cshtml`:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Mikkelhm.Models.CloudAlertsHome>
@using System.Globalization
@using Mikkelhm.Core.CloudAlerts
@inject Microsoft.Extensions.Logging.ILogger<Mikkelhm.Models.CloudAlertsHome> Logger
@{
    Layout = null;

    var query = Context.Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    var filter = CloudAlertFilter.FromQuery(query);

    var alerts = new List<CloudAlertView>();
    foreach (var item in Model.Children<Mikkelhm.Models.CloudAlert>() ?? Enumerable.Empty<Mikkelhm.Models.CloudAlert>())
    {
        if (string.IsNullOrWhiteSpace(item.AlertId) || item.TimeFired == default)
        {
            Logger.LogWarning("Skipping malformed cloud alert node {Key}", item.Key);
            continue;
        }

        alerts.Add(new CloudAlertView(
            item.Key,
            item.AlertId,
            item.ProjectAlias ?? "",
            item.ProjectUrl ?? "",
            item.EnvironmentName ?? "",
            item.AlertName ?? "",
            item.Details ?? "",
            item.Severity ?? "",
            item.CustomerMetadata ?? "",
            DateTime.SpecifyKind(item.TimeFired, DateTimeKind.Utc),
            item.IsTest,
            item.RawPayload ?? ""));
    }

    var result = CloudAlertsQuery.Run(alerts, filter);
    var title = string.IsNullOrWhiteSpace(Model.MetaTitle) ? Model.Name : Model.MetaTitle;
    var baseUrl = Model.Url();
    string PageUrl(CloudAlertFilter f) => baseUrl + f.ToQueryString();
    bool IsSelected(string? current, string value) => string.Equals(current, value, StringComparison.OrdinalIgnoreCase);
}
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@title</title>
    <script>document.documentElement.classList.add('js');</script>
    <link rel="stylesheet" href="/cloudalerts/cloudalerts.css" />
</head>
<body>
    <header class="ca-header">
        <h1>@title</h1>
        <p class="ca-sub">Umbraco Cloud alerts received by webhook</p>
    </header>

    <main class="ca-main">
        <form id="ca-filters" class="ca-filters" method="get" action="@baseUrl">
            <label>Project
                <select name="project">
                    <option value="">All</option>
                    @foreach (var v in result.Facets.Projects) { <option value="@v" selected="@IsSelected(filter.Project, v)">@v</option> }
                </select>
            </label>
            <label>Environment
                <select name="env">
                    <option value="">All</option>
                    @foreach (var v in result.Facets.Environments) { <option value="@v" selected="@IsSelected(filter.Environment, v)">@v</option> }
                </select>
            </label>
            <label>Alert
                <select name="alert">
                    <option value="">All</option>
                    @foreach (var v in result.Facets.AlertNames) { <option value="@v" selected="@IsSelected(filter.AlertName, v)">@v</option> }
                </select>
            </label>
            <label>Severity
                <select name="severity">
                    <option value="">All</option>
                    @foreach (var v in result.Facets.Severities) { <option value="@v" selected="@IsSelected(filter.Severity, v)">@v</option> }
                </select>
            </label>
            <label>From
                <input type="date" name="from" value="@filter.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)" />
            </label>
            <label>To
                <input type="date" name="to" value="@filter.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)" />
            </label>
            <label class="ca-search">Search details
                <input type="search" name="q" value="@filter.Search" placeholder="e.g. completed" />
            </label>
            <fieldset class="ca-tests">
                <legend>Test alerts</legend>
                @foreach (var mode in new[] { TestAlertMode.Show, TestAlertMode.Hide, TestAlertMode.Only })
                {
                    var value = mode.ToString().ToLowerInvariant();
                    <label><input type="radio" name="tests" value="@value" checked="@(filter.Tests == mode)" /> @value</label>
                }
            </fieldset>
            <div class="ca-actions">
                <button type="submit" class="ca-apply">Apply</button>
                <a href="@baseUrl" class="ca-reset">Reset</a>
            </div>
        </form>

        <p class="ca-summary">
            <strong>@result.TotalCount</strong> @(result.TotalCount == 1 ? "alert" : "alerts")
            @foreach (var s in result.SeverityCounts)
            {
                <a class="ca-chip @CloudAlertDisplay.SeverityClass(s.Severity)" href="@PageUrl(filter with { Severity = s.Severity, Page = 1 })">
                    <span class="ca-dot"></span>@s.Count @s.Severity
                </a>
            }
        </p>

        @if (result.Items.Count == 0)
        {
            <p class="ca-empty">@(alerts.Count == 0 ? "No alerts received yet." : "No alerts match these filters.")</p>
        }
        else
        {
            <ol class="ca-list">
                @foreach (var a in result.Items)
                {
                    var projectLink = CloudAlertDisplay.SafeHttpUrl(a.ProjectUrl);
                    <li class="ca-card @CloudAlertDisplay.SeverityClass(a.Severity)">
                        <div class="ca-card-head">
                            <span class="ca-badge">@(a.Severity.Length > 0 ? a.Severity.ToUpperInvariant() : "N/A")</span>
                            <span class="ca-alert">@a.AlertName</span>
                            @if (projectLink is not null)
                            {
                                <a class="ca-project" href="@projectLink" rel="noopener noreferrer" target="_blank">@a.ProjectAlias ↗</a>
                            }
                            else
                            {
                                <span class="ca-project">@a.ProjectAlias</span>
                            }
                            @if (a.EnvironmentName.Length > 0) { <span class="ca-pill">@a.EnvironmentName</span> }
                            @if (a.IsTest) { <span class="ca-pill ca-test">TEST</span> }
                        </div>
                        <p class="ca-details">@a.Details</p>
                        <div class="ca-card-foot">
                            <span>
                                <time datetime="@a.TimeFiredUtc.ToString("O", CultureInfo.InvariantCulture)" data-local>@a.TimeFiredUtc.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) UTC</time>
                                <span class="ca-rel" data-rel></span>
                            </span>
                            <details class="ca-meta">
                                <summary>metadata</summary>
                                @if (a.CustomerMetadata.Length > 0)
                                {
                                    <h3>Customer metadata</h3>
                                    <pre>@CloudAlertDisplay.PrettyJson(a.CustomerMetadata)</pre>
                                }
                                <h3>Raw payload</h3>
                                <pre>@CloudAlertDisplay.PrettyJson(a.RawPayload)</pre>
                            </details>
                        </div>
                    </li>
                }
            </ol>

            @if (result.TotalPages > 1)
            {
                <nav class="ca-pager" aria-label="Pages">
                    @if (result.Page > 1) { <a href="@PageUrl(filter with { Page = result.Page - 1 })">‹ Prev</a> } else { <span></span> }
                    <span>Page @result.Page of @result.TotalPages</span>
                    @if (result.Page < result.TotalPages) { <a href="@PageUrl(filter with { Page = result.Page + 1 })">Next ›</a> } else { <span></span> }
                </nav>
            }
        }
    </main>

    <script src="/cloudalerts/cloudalerts.js"></script>
</body>
</html>
```

(Razor omits the attribute when `selected="@false"` / `checked="@false"`, so only the matching option/radio gets it. `IsSelected` keeps option matching case-insensitive.)

- [ ] **Step 6: Write the stylesheet** `src/Mikkelhm.Web/wwwroot/cloudalerts/cloudalerts.css`:

```css
:root {
  --bg: #f6f7f9;
  --surface: #ffffff;
  --text: #1b1f24;
  --muted: #5d6673;
  --border: #dde1e6;
  --accent: #3544b1;
  --pill: #eef0f4;
  --sev-critical: #9b1c1c;
  --sev-high: #d6453d;
  --sev-medium: #d98a14;
  --sev-low: #2f8f5b;
  --sev-none: #8a93a0;
  --radius: 10px;
  color-scheme: light;
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg: #111418;
    --surface: #1a1f25;
    --text: #e6e9ed;
    --muted: #9aa3ae;
    --border: #2c333b;
    --accent: #8f9bff;
    --pill: #252b33;
    --sev-critical: #f06a6a;
    --sev-high: #f0837c;
    --sev-medium: #f2b457;
    --sev-low: #62c793;
    --sev-none: #7c8591;
    color-scheme: dark;
  }
}

* { box-sizing: border-box; }

body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font: 15px/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
}

a { color: var(--accent); }

.ca-header, .ca-main { max-width: 1000px; margin: 0 auto; padding: 0 16px; }
.ca-header { padding-top: 32px; }
.ca-header h1 { margin: 0; font-size: 1.75rem; }
.ca-sub { margin: 4px 0 20px; color: var(--muted); }

.ca-filters {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
  gap: 12px;
  padding: 16px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}
.ca-filters label { display: flex; flex-direction: column; gap: 4px; font-size: 0.8rem; color: var(--muted); }
.ca-filters select, .ca-filters input[type="date"], .ca-filters input[type="search"] {
  font: inherit; color: var(--text); background: var(--bg);
  border: 1px solid var(--border); border-radius: 6px; padding: 6px 8px; min-width: 0;
}
.ca-search { grid-column: span 2; }
.ca-tests { border: 0; margin: 0; padding: 0; display: flex; flex-wrap: wrap; align-items: end; gap: 10px; font-size: 0.85rem; }
.ca-tests legend { font-size: 0.8rem; color: var(--muted); padding: 0; margin-bottom: 4px; }
.ca-tests label { flex-direction: row; align-items: center; gap: 4px; color: var(--text); }
.ca-actions { display: flex; align-items: end; gap: 12px; }
.ca-apply { font: inherit; padding: 6px 14px; border: 0; border-radius: 6px; background: var(--accent); color: #fff; cursor: pointer; }
.js .ca-apply { display: none; }

.ca-summary { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 20px 0 12px; }
.ca-chip { display: inline-flex; align-items: center; gap: 6px; padding: 2px 10px; border-radius: 999px; background: var(--pill); color: var(--text); text-decoration: none; font-size: 0.85rem; }
.ca-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--sev-color, var(--sev-none)); }

.sev-critical { --sev-color: var(--sev-critical); }
.sev-high { --sev-color: var(--sev-high); }
.sev-medium { --sev-color: var(--sev-medium); }
.sev-low { --sev-color: var(--sev-low); }
.sev-none, .sev-other { --sev-color: var(--sev-none); }

.ca-empty { padding: 32px; text-align: center; color: var(--muted); background: var(--surface); border: 1px dashed var(--border); border-radius: var(--radius); }

.ca-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.ca-card { background: var(--surface); border: 1px solid var(--border); border-left: 4px solid var(--sev-color, var(--sev-none)); border-radius: var(--radius); padding: 12px 16px; }
.ca-card-head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.ca-badge { font-size: 0.7rem; font-weight: 700; letter-spacing: 0.04em; padding: 2px 8px; border-radius: 4px; color: #fff; background: var(--sev-color, var(--sev-none)); }
.ca-alert { font-weight: 600; }
.ca-project { text-decoration: none; }
.ca-pill { font-size: 0.75rem; padding: 1px 8px; border-radius: 999px; background: var(--pill); color: var(--muted); }
.ca-test { border: 1px dashed var(--muted); }
.ca-details { margin: 6px 0; overflow-wrap: anywhere; }
.ca-card-foot { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; font-size: 0.8rem; color: var(--muted); }
.ca-rel::before { content: " · "; }
.ca-rel:empty::before { content: ""; }
.ca-meta summary { cursor: pointer; }
.ca-meta h3 { font-size: 0.75rem; margin: 8px 0 4px; text-transform: uppercase; }
.ca-meta pre { margin: 0; padding: 8px; max-width: 100%; overflow-x: auto; background: var(--bg); border-radius: 6px; color: var(--text); font-size: 0.75rem; }
.ca-meta[open] { flex-basis: 100%; }

.ca-pager { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 40px; }

@media (max-width: 560px) {
  .ca-search { grid-column: auto; }
  .ca-filters { grid-template-columns: 1fr 1fr; }
  .ca-search, .ca-tests, .ca-actions { grid-column: 1 / -1; }
}
```

- [ ] **Step 7: Write the script** `src/Mikkelhm.Web/wwwroot/cloudalerts/cloudalerts.js`:

```js
(function () {
  'use strict';

  var form = document.getElementById('ca-filters');
  if (form) {
    form.addEventListener('change', function () {
      Array.prototype.forEach.call(form.elements, function (el) {
        if (!el.name) return;
        var isDefaultTests = el.name === 'tests' && el.value === 'show';
        if ((el.value === '' && el.type !== 'radio') || isDefaultTests) el.disabled = true;
      });
      form.submit();
    });
  }

  function pad(n) { return n < 10 ? '0' + n : String(n); }

  function formatLocal(d) {
    return pad(d.getDate()) + '-' + pad(d.getMonth() + 1) + '-' + d.getFullYear() + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  }

  var rtf = window.Intl && Intl.RelativeTimeFormat ? new Intl.RelativeTimeFormat('en', { numeric: 'auto' }) : null;

  function relative(d) {
    if (!rtf) return '';
    var seconds = Math.round((d.getTime() - Date.now()) / 1000);
    var units = [['day', 86400], ['hour', 3600], ['minute', 60], ['second', 1]];
    for (var i = 0; i < units.length; i++) {
      if (Math.abs(seconds) >= units[i][1] || units[i][0] === 'second') {
        return rtf.format(Math.round(seconds / units[i][1]), units[i][0]);
      }
    }
    return '';
  }

  document.querySelectorAll('time[data-local]').forEach(function (t) {
    var d = new Date(t.getAttribute('datetime'));
    if (isNaN(d.getTime())) return;
    t.textContent = formatLocal(d);
    t.title = t.getAttribute('datetime');
    var rel = t.parentElement.querySelector('[data-rel]');
    if (rel) rel.textContent = relative(d);
  });
})();
```

- [ ] **Step 8: Verify the view compiles.** Stop the site.

Run: `dotnet build src/Mikkelhm.Web/Mikkelhm.Web.csproj -p:RazorCompileOnBuild=true -p:OutputPath=<scratchpad>/razorcheck/`
Expected: Build succeeded, 0 errors (warnings from other views are pre-existing; no new errors from `cloudAlertsHome.cshtml`).

- [ ] **Step 9: Verify the page.** Start the site. Using the alerts seeded in Task 6:

```bash
B=https://localhost:44385/cloud-alerts/
curl -sk -o /dev/null -w "page: %{http_code}\n" "$B"
curl -sk "$B" | grep -c 'class="ca-card"'
curl -sk "$B?tests=hide" | grep -c 'class="ca-card"'
curl -sk "$B?env=Development" | grep -c 'class="ca-card"'
curl -sk -o /dev/null -w "garbage query: %{http_code}\n" "$B?page=abc&from=31-02-2026&tests=weird"
```

Expected: `page: 200`; card counts match the seeded data (all / non-test only / Development only); `garbage query: 200`. Then open `https://localhost:44385/cloud-alerts/` in a browser (or ask Mikkel to) and check: filters auto-apply on change, severity chips filter, `metadata` expands, times show local, layout works at phone width and in dark mode.

- [ ] **Step 10: Commit**

```bash
git add src/Mikkelhm.Core/CloudAlerts/CloudAlertDisplay.cs tests/Mikkelhm.Core.Tests/CloudAlerts/CloudAlertDisplayTests.cs src/Mikkelhm.Web/Views/cloudAlertsHome.cshtml src/Mikkelhm.Web/wwwroot/cloudalerts
git commit -m "Add Cloud Alerts listing page with filters

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Documentation and rollout handover

**Files:**
- Modify: `CLAUDE.md` (Site Structure table + a Cloud Alerts bullet; frontend assets list; projects list mentions the test project)

- [ ] **Step 1: Update `CLAUDE.md`.**
  - In **Site Structure**: change "Three independent subsites" to "Four independent subsites" and add this row to the table:

    `| Cloud Alerts | Cloud Alerts → Cloud Alerts → alert items | `cloudAlertsHome` (collection), `cloudAlert` | `CloudAlertsHome` | `/cloud-alerts/` (filters via query string) |`

  - Add a bullet under the table: `- **Cloud Alerts webhook**: `POST /umbraco/api/cloud-alerts/webhook` (`CloudAlertsWebhookController`), header `uc-webhook-auth` checked against config `CloudAlerts:WebhookSecret` (user-secrets locally, env var `CloudAlerts__WebhookSecret` on Cloud). Logic lives in `src/Mikkelhm.Core/CloudAlerts/`.`
  - In **Frontend Assets**: add `- `wwwroot/cloudalerts/` - Cloud Alerts listing page styles and script`.
  - In **Project Overview** list: add `- **tests/Mikkelhm.Core.Tests** - xUnit + NSubstitute tests for Core (run `dotnet test src/Mikkelhm.sln`)`.

- [ ] **Step 2: Run the full test suite and build**

Run: `dotnet test src/Mikkelhm.sln`
Expected: all tests PASS, build succeeded.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "Document the Cloud Alerts subsite

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Hand over the manual rollout steps to Mikkel** (do not do these; do not push). Report:
  1. Merge `feature/cloud-alerts` to `main` and push (fetch + rebase first); GitHub Actions deploys.
  2. In the Live backoffice: create `Cloud Alerts` (Website) → `Cloud Alerts` (Cloud Alerts Home), both **published** (or transfer them with Deploy). The home must be published or alert publishing fails with 500.
  3. In the Cloud portal, set `CloudAlerts__WebhookSecret` for Live to the value generated in the brainstorming session.
  4. Configure the webhook: `https://mikkelhm.euwest01.umbraco.io/umbraco/api/cloud-alerts/webhook`, header `uc-webhook-auth: <that value>`; press the test button; expect the alert on `https://mikkelhm.euwest01.umbraco.io/cloud-alerts/`.
  5. Known limit: duplicate detection scans the container's children (fine up to a few thousand alerts).
