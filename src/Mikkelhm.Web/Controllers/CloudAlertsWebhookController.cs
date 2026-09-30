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
