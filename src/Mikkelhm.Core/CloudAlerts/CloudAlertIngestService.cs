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
