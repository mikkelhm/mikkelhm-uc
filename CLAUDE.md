# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. Woohoo

## Project Overview

This is an Umbraco Cloud CMS project built with .NET 10.0. The solution consists of:

- **Mikkelhm.Web** - Main web application with Umbraco CMS integration
- **Mikkelhm.Core** - Core library containing components, composers, and frontend helpers
- **Mikkelhm.Models** - ModelsBuilder-generated strongly typed content models (`*.generated.cs`). Umbraco regenerates them on boot and when doc types change, so commit them together with the matching `.uda` changes
- **tests/Mikkelhm.Core.Tests** - xUnit + NSubstitute tests for Core (run `dotnet test src/Mikkelhm.sln`). `tests/Mikkelhm.Web.Scripts` holds `node --test` tests for frontend scripts (run `node --test tests/Mikkelhm.Web.Scripts`)

The project uses Umbraco CMS v18.2.0 with Umbraco Cloud v18.0.3, Deploy v18.1.1, and Forms v18.1.3 packages for content management and deployment.

## Common Development Commands

### Build and Run
```bash
# Build the entire solution (recommended - includes all projects)
dotnet build src/Mikkelhm.sln

# Build only the web project (also builds Core and Models as dependencies)
dotnet build src/Mikkelhm.Web/Mikkelhm.Web.csproj

# Run the web application
dotnet run --project src/Mikkelhm.Web/Mikkelhm.Web.csproj

# Build in release mode
dotnet build src/Mikkelhm.sln -c Release
```

### Package Management
```bash
# Restore NuGet packages for entire solution
dotnet restore src/Mikkelhm.sln

# Add package reference to specific projects
dotnet add src/Mikkelhm.Web/Mikkelhm.Web.csproj package [PackageName]
dotnet add src/Mikkelhm.Core/Mikkelhm.Core.csproj package [PackageName]
```

The solution uses **Central Package Management**: all package versions are defined in `Directory.Packages.props` at the repository root, and `PackageReference` items in the csproj files must not specify a `Version` attribute. `dotnet add package` handles this automatically; when editing csproj files by hand, add/update the corresponding `PackageVersion` entry in `Directory.Packages.props` instead.

## Architecture Overview

### Solution Structure
The solution has three projects with a clear separation of concerns:

**src/Mikkelhm.sln** - Root solution file containing all three projects

**src/Mikkelhm.Web/** - ASP.NET Core web application (SDK: Microsoft.NET.Sdk.Web)
  - Standard Umbraco startup: `Program.cs` uses `ConfigureUmbracoDefaults()` and `Startup.cs` configures services
  - `Startup.cs`: Registers Umbraco with `.AddBackOffice()`, `.AddWebsite()`, `.AddDeliveryApi()`, and `.AddComposers()`
  - Contains Views, App_Plugins, and wwwroot for frontend assets
  - Project references to Mikkelhm.Core and Mikkelhm.Models
  - Razor compilation disabled (`RazorCompileOnBuild: false`) for faster development

**src/Mikkelhm.Core/** - Shared class library (SDK: Microsoft.NET.Sdk)
  - Contains reusable Umbraco components, composers, and utilities
  - References: `Umbraco.Cms.Core`, `Microsoft.AspNetCore.Html.Abstractions`, `Microsoft.AspNetCore.Mvc.ViewFeatures`
  - Currently has commented-out ContentImporter component/composer for XML package imports
  - `Frontend/HtmlHelperExtensions.cs`: Utility methods for tag weight calculations and Umbraco version detection

### Key Architectural Patterns
- **Standard Umbraco Startup**: Uses `Program.cs` with `CreateHostBuilder()` pattern and separate `Startup.cs` for service/middleware configuration
- **Middleware Pipeline**: Developer exception page (dev only) → HTTPS redirection → Umbraco middleware (backoffice + website) → Umbraco endpoints
- **Component Registration**: Uses Umbraco's `IComponent` and `IComposer` pattern for extensibility (examples exist but are commented out)

### Frontend Assets
- `wwwroot/asta/` - Asta theme with SCSS assets
- `wwwroot/blog/` - Blog theme assets
- `wwwroot/ellabm/` - Ellabm party photo subsite (upload + gallery slideshow)
- `wwwroot/cloudalerts/` - Cloud Alerts listing page styles and script
- `wwwroot/media/` - Media storage
- `App_Plugins/AstaPhotoGalleryListView/` - Custom photo gallery list view plugin
- `App_Plugins/UmbracoId/` - UmbracoId authentication plugin

### Configuration (`appsettings.json`)
- **Custom Section**: `Mikkel.AwesomeSiteEnabled` boolean flag
- **Umbraco.CMS.Global**: `UseHttps: true`, custom NoNodes view, TinyMCE sanitization enabled
- **Umbraco.CMS.Content**: Allows editing invariant content from non-default languages, content version cleanup enabled
- **Umbraco.CMS.DeliveryApi**: Disabled by default (`Enabled: false`, `PublicAccess: false`)
- **Serilog**: Minimum level Information, overrides for Microsoft (Warning) and System (Warning)

### Important Technical Details
- **.NET 10.0** with nullable reference types enabled
- **Database**: SQLite for local development (`umbraco/Data/Umbraco.sqlite.db`)
- **Umbraco Cloud**: Deployment artifacts stored in `umbraco/Deploy/`
- **ICU Globalization**: Uses app-local ICU4C runtime (version 72.1.0.3) for consistent globalization across platforms
- **Razor Compilation**: Disabled for build but Razor files copied to publish directory for backoffice functionality

## Site Structure

Four independent subsites, each a root `Website` node (Generic doc type, no template) with its own home page. No hostnames are bound, so the root URL `/` returns 404 locally and on Cloud (known, accepted).

| Subsite | Tree | Document types | Templates | URL |
|---------|------|----------------|-----------|-----|
| Blog | Blog → Home → Archive → 81 posts | `blogHome`, `blogPostRepository` (collection), `blogPost` | `BlogMaster` (layout) → `BlogHome`, `BlogPost` | `/home/`, `/home/archive/<post>/` |
| Asta | Asta → Home → Photos → 7 photos | `astaHome`, `astaPhotoGallery` (collection), `astaPhoto` | `AstaHome` only | Not routable locally: its Home collides with Blog's `/home/` |
| Ellabm | Ellabm → Ellas Fest → Galleri, Admin | `ellabmHome`, `ellabmGallery`, `ellabmAdmin` | `EllabmHome`, `EllabmGallery`, `EllabmAdmin` | `/ellas-fest/`, `/ellas-fest/galleri/`, `/ellas-fest/admin/` |
| Cloud Alerts | CloudAlertsSite → Cloud Alerts → alert items | `cloudAlertsHome` (collection), `cloudAlert` | `CloudAlertsHome` | `/cloud-alerts/` (filters via query string) |

- **Shared**: `website` (root node) and `sEOSection` (element type used as a composition by the Blog doc types), both in the `Generic` doc type folder.
- **Ellabm API**: `src/Mikkelhm.Web/Controllers/EllabmApiController.cs` at `umbraco/api/ellabm`: `POST upload`, `GET photos`, `DELETE photos/{id}`. It backs the photo upload, slideshow and admin pages.
- **Cloud Alerts webhook**: `POST /umbraco/api/cloud-alerts/webhook` (`CloudAlertsWebhookController`), header `uc-webhook-auth` checked against config `CloudAlerts:WebhookSecret` (user-secrets locally, env var `CloudAlerts__WebhookSecret` on Cloud). Each alert becomes a published `cloudAlert` under the `Cloud Alerts` home. Logic lives in `src/Mikkelhm.Core/CloudAlerts/`. The listing page is a day-grouped timeline; its times, day groups and date filters always use Copenhagen time (`CloudAlertsTime`), and it shows only the project alias (no alert IDs, project URLs or payloads). The root node is named `CloudAlertsSite` because a root and home with the same name collide on URL.
- The schema lives in `.uda` files in `src/Mikkelhm.Web/umbraco/Deploy/Revision/`. Use the Umbraco MCP for content and publish state.

## Umbraco MCP Workflow Tips

The Umbraco MCP (`umbraco-mcp` in `.mcp.json`) runs `@umbraco-cms/mcp-dev@18.1` with credentials from the gitignored `.env`. Keep its major version in line with the CMS. It needs the site running on `https://localhost:44385` (`UmbracoProject` launch profile).

When using the Umbraco MCP tools to create document types and content, follow this order:

1. **Create document type folder** (if needed)
2. **Create document types** with properties
3. **Create templates** (`create-template`) — must exist before content is created
4. **Update document types** to set `allowedTemplates` and `defaultTemplate`
5. **Update document types** for allowed children relationships
6. **Create content nodes** — they inherit the default template at creation time
7. **Publish content nodes**

If content was created before templates were linked, the content node gets `template: null` and won't render. Fix by using `update-document` to set the template, then re-publish.

### Package Boundaries for Controllers
- **Mikkelhm.Core** only references `Umbraco.Cms.Core` — no media file upload extensions (`SetValue` with `MediaFileManager`), no `MediaUrlGeneratorCollection`
- **Mikkelhm.Web** references the full `Umbraco.Cms` meta-package — place API controllers that need media/infrastructure APIs here

### Deploy Artifacts
When document types, templates, or content structure changes are made via MCP tools or the backoffice, Umbraco Deploy auto-updates `.uda` files in `umbraco/Deploy/Revision/`. These must be committed alongside code changes.
