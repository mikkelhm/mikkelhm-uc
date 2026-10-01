namespace Mikkelhm.Core.CloudAlerts;

public interface ICloudAlertStore
{
    Task<Guid?> FindContainerKeyAsync();

    Task<bool> AlertExistsAsync(Guid containerKey, string alertId);

    Task<Guid> CreateAsync(Guid containerKey, CloudAlertNodeData data);
}
