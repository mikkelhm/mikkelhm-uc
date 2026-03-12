using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Mikkelhm.Web.Controllers;

[ApiController]
[Route("umbraco/api/ellabm")]
public class EllabmApiController : ControllerBase
{
    private const string Password = "2026";
    private const string MediaFolderName = "Ellabm Photos";

    private readonly IMediaService _mediaService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

    public EllabmApiController(
        IMediaService mediaService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider)
    {
        _mediaService = mediaService;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
    }

    [HttpPost("upload")]
    [IgnoreAntiforgeryToken]
    public IActionResult Upload([FromForm] IFormFile photo, [FromForm] string? uploaderName)
    {
        var password = Request.Headers["X-Ellabm-Password"].FirstOrDefault() ?? "";
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(password),
                Encoding.UTF8.GetBytes(Password)))
        {
            return Unauthorized(new { error = "Invalid password" });
        }

        if (photo == null || photo.Length == 0)
        {
            return BadRequest(new { error = "No photo provided" });
        }

        var folder = FindOrCreateMediaFolder();
        if (folder == null)
        {
            return StatusCode(500, new { error = "Could not find or create media folder" });
        }

        var displayName = !string.IsNullOrWhiteSpace(uploaderName)
            ? $"{uploaderName} - {DateTime.Now:HH-mm-ss}"
            : $"Photo - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";

        var media = _mediaService.CreateMedia(displayName, folder.Id, "Image");

        using (var stream = photo.OpenReadStream())
        {
            media.SetValue(
                _mediaFileManager,
                _mediaUrlGenerators,
                _shortStringHelper,
                _contentTypeBaseServiceProvider,
                "umbracoFile",
                photo.FileName,
                stream);
        }

        var result = _mediaService.Save(media);
        if (!result.Success)
        {
            return StatusCode(500, new { error = "Failed to save media" });
        }

        return Ok(new { success = true, name = displayName });
    }

    [HttpGet("photos")]
    public IActionResult GetPhotos()
    {
        var folder = FindMediaFolder();
        if (folder == null)
        {
            return Ok(Array.Empty<object>());
        }

        var children = _mediaService.GetPagedChildren(folder.Id, 0, int.MaxValue, out _);

        var photos = children
            .Where(m => m.ContentType.Alias == "Image")
            .OrderByDescending(m => m.CreateDate)
            .Select(m =>
            {
                var fileValue = m.GetValue<string>("umbracoFile") ?? "";
                var url = fileValue;
                if (fileValue.StartsWith("{"))
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(fileValue);
                        url = doc.RootElement.GetProperty("src").GetString() ?? "";
                    }
                    catch
                    {
                        url = fileValue;
                    }
                }

                return new
                {
                    url,
                    name = m.Name,
                    date = m.CreateDate.ToString("O")
                };
            })
            .ToList();

        return Ok(photos);
    }

    private IMedia? FindMediaFolder()
    {
        var rootMedia = _mediaService.GetPagedChildren(-1, 0, 100, out _);
        return rootMedia.FirstOrDefault(m => m.Name == MediaFolderName && m.ContentType.Alias == "Folder");
    }

    private IMedia FindOrCreateMediaFolder()
    {
        var existing = FindMediaFolder();
        if (existing != null) return existing;

        var folder = _mediaService.CreateMedia(MediaFolderName, -1, "Folder");
        _mediaService.Save(folder);
        return folder;
    }
}
