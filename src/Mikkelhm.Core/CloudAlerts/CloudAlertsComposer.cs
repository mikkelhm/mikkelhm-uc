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
