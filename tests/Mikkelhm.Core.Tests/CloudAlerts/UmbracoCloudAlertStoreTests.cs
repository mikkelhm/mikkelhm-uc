using Mikkelhm.Core.CloudAlerts;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;

namespace Mikkelhm.Core.Tests.CloudAlerts;

public class UmbracoCloudAlertStoreTests
{
    private readonly IDocumentNavigationQueryService _navigation = Substitute.For<IDocumentNavigationQueryService>();
    private readonly IPublishedContentCache _cache = Substitute.For<IPublishedContentCache>();
    private readonly IContentService _contentService = Substitute.For<IContentService>();
    private readonly UmbracoCloudAlertStore _store;
    private readonly Guid _rootKey = Guid.NewGuid();
    private readonly Guid _homeKey = Guid.NewGuid();

    public UmbracoCloudAlertStoreTests()
    {
        _store = new UmbracoCloudAlertStore(_navigation, _cache, _contentService);

        _navigation.TryGetRootKeys(out Arg.Any<IEnumerable<Guid>>())
            .Returns(x => { x[0] = new[] { _rootKey }; return true; });
        _navigation.TryGetDescendantsKeysOfType(_rootKey, CloudAlertsConstants.HomeAlias, out Arg.Any<IEnumerable<Guid>>())
            .Returns(x => { x[2] = new[] { _homeKey }; return true; });
    }

    [Fact]
    public async Task FindContainerKeyAsync_PublishedContainer_ReturnsItsKey()
    {
        _cache.GetByIdAsync(_homeKey, Arg.Any<bool?>()).Returns(Substitute.For<IPublishedContent>());

        Assert.Equal(_homeKey, await _store.FindContainerKeyAsync());
    }

    [Fact]
    public async Task FindContainerKeyAsync_UnpublishedContainer_ReturnsNull()
    {
        _cache.GetByIdAsync(_homeKey, Arg.Any<bool?>()).Returns((IPublishedContent?)null);

        Assert.Null(await _store.FindContainerKeyAsync());
    }

    [Fact]
    public async Task CreateAsync_PublishFails_DeletesTheSavedDraftAndThrows()
    {
        var content = Substitute.For<IContent>();
        _contentService.Create(Arg.Any<string>(), _homeKey, CloudAlertsConstants.AlertAlias, Arg.Any<int>()).Returns(content);
        _contentService.Save(content, Arg.Any<int?>(), Arg.Any<ContentScheduleCollection?>())
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));
        _contentService.Publish(content, Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PublishResult(PublishResultType.FailedPublishPathNotPublished, new EventMessages(), content));

        var data = new CloudAlertNodeData("Name", "alert-1", "p", "", "Live", "Deployment", "", "", "", DateTime.UtcNow, false, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CreateAsync(_homeKey, data));
        _contentService.Received(1).Delete(content, Arg.Any<int>());
    }
}
