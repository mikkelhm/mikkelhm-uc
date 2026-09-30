namespace Mikkelhm.Core.CloudAlerts;

public interface ICloudAlertIngestService
{
    Task<IngestResult> IngestAsync(string rawPayload);
}
