# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an Umbraco Cloud CMS project built with .NET 9.0. The solution consists of:

- **Mikkelhm.Web** - Main web application with Umbraco CMS integration
- **Mikkelhm.Core** - Core library containing components, composers, and frontend helpers

The project uses Umbraco CMS v15.4.3 with Umbraco Cloud, Deploy, and Forms packages for content management and deployment.

## Common Development Commands

### Build and Run
```bash
# Build the solution
dotnet build src/Mikkelhm.Web/Mikkelhm.Web.sln

# Run the web application
dotnet run --project src/Mikkelhm.Web/Mikkelhm.Web.csproj

# Build in release mode
dotnet build src/Mikkelhm.Web/Mikkelhm.Web.sln -c Release
```

### Package Management
```bash
# Restore NuGet packages
dotnet restore src/Mikkelhm.Web/Mikkelhm.Web.sln

# Add package reference
dotnet add src/Mikkelhm.Web/Mikkelhm.Web.csproj package [PackageName]
dotnet add src/Mikkelhm.Core/Mikkelhm.Core.csproj package [PackageName]
```

## Architecture Overview

### Project Structure
- `src/Mikkelhm.Web/` - ASP.NET Core web application
  - Uses standard Umbraco startup pattern with `Program.cs` and `Startup.cs`
  - Contains Views, App_Plugins, and wwwroot for frontend assets
  - References Mikkelhm.Core for shared functionality

- `src/Mikkelhm.Core/` - Shared library
  - Contains Umbraco components and composers (currently commented out)
  - Frontend helpers and HTML extensions
  - Display models for weighted tags

### Key Components
- **ContentImporter** (commented out) - Component for importing content from XML packages
- **HtmlHelperExtensions** - Utility methods for tag weight calculations and Umbraco version detection
- **Umbraco Configuration** - Standard Umbraco CMS setup with backoffice, website, and delivery API

### Frontend Structure
The project includes multiple frontend themes:
- **Asta** theme in `wwwroot/asta/` with SCSS assets
- **Blog** theme in `wwwroot/blog/` 
- Custom App_Plugins for photo gallery list view and UmbracoId authentication

### Development Notes
- The project uses .NET 9.0 with nullable reference types enabled
- Razor compilation is disabled for development (`RazorCompileOnBuild: false`)
- Uses SQLite database for local development (`umbraco/Data/Umbraco.sqlite.db`)
- Umbraco Cloud integration with deployment artifacts in `umbraco/Deploy/`

### Configuration
- Main configuration in `appsettings.json` with Umbraco-specific settings
- Custom configuration section "Mikkel" with AwesomeSiteEnabled flag
- Serilog configured for structured logging
- HTTPS enforcement and content cleanup policies enabled