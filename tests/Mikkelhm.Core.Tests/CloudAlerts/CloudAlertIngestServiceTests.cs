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
