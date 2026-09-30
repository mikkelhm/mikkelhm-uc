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
        content.SetValue(CloudAlertsConstants.Properties.TimeFired, CloudAlertMapper.ToDateTimeEditorValue(data.TimeFiredUtc));
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
