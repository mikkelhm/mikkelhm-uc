# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an Umbraco Cloud CMS project built with .NET 10.0. The solution consists of:

- **Mikkelhm.Web** - Main web application with Umbraco CMS integration
- **Mikkelhm.Core** - Core library containing components, composers, and frontend helpers

The project uses Umbraco CMS v17.2.2 with Umbraco Cloud v17.0.0, Deploy, and Forms packages for content management and deployment.

## Common Development Commands

### Build and Run
```bash
# Build the entire solution (recommended - includes both projects)
dotnet build src/Mikkelhm.sln

# Build only the web project (also builds Core as dependency)
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

## Architecture Overview

### Solution Structure
The solution has two primary projects with a clear separation of concerns:

**src/Mikkelhm.sln** - Root solution file containing both projects

**src/Mikkelhm.Web/** - ASP.NET Core web application (SDK: Microsoft.NET.Sdk.Web)
  - Standard Umbraco startup: `Program.cs` uses `ConfigureUmbracoDefaults()` and `Startup.cs` configures services
  - `Startup.cs`: Registers Umbraco with `.AddBackOffice()`, `.AddWebsite()`, `.AddDeliveryApi()`, and `.AddComposers()`
  - Contains Views, App_Plugins, and wwwroot for frontend assets
  - Project reference to Mikkelhm.Core
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

## Umbraco MCP Workflow Tips

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