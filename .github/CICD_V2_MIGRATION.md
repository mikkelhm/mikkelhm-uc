# Umbraco Cloud CI/CD Flow V2 Migration Guide

This document describes the migration from Umbraco Cloud CI/CD API v1 to v2 that has been applied to this repository.

## Migration Date
Feature branch created: 2025-11-14

## What Changed

### API Version
- **From**: Umbraco Cloud CI/CD Flow API v1
- **To**: Umbraco Cloud CI/CD Flow API v2

### Key Improvements in V2
1. **Multi-environment targeting**: Can now deploy to specific environments by alias
2. **Decoupled artifact upload**: Artifacts are uploaded separately and can be reused across deployments
3. **Better artifact management**: Can query and track uploaded artifacts via API
4. **More flexible deployment options**: Additional configuration options available

## Files Modified

### PowerShell Scripts
**Deleted (v1 scripts):**
- `.github/workflows/powershell/Add-DeploymentPackage.ps1`
- `.github/workflows/powershell/Get-ChangesById.ps1`
- `.github/workflows/powershell/Get-LatestDeployment.ps1`
- `.github/workflows/powershell/New-Deployment.ps1`
- `.github/workflows/powershell/Start-Deployment.ps1`
- `.github/workflows/powershell/Test-DeploymentStatus.ps1`

**Added (v2 scripts):**
- `.github/workflows/powershell/Add-DeploymentArtifact.ps1` - Upload artifact to Umbraco Cloud
- `.github/workflows/powershell/Apply-Patch.ps1` - Apply git patches from Cloud
- `.github/workflows/powershell/Get-ChangesById.ps1` - Get changes from Cloud (v2)
- `.github/workflows/powershell/Get-LatestDeployment.ps1` - Get latest deployment (v2)
- `.github/workflows/powershell/Start-Deployment.ps1` - Start deployment with artifact (v2)
- `.github/workflows/powershell/Test-DeploymentStatus.ps1` - Poll deployment status (v2)

### GitHub Workflows
**Modified:**
- `.github/workflows/main.yml` - Updated to v2 flow with all workflows re-enabled
- `.github/workflows/cloud-sync.yml` - Replaced with v2 version (custom git config preserved)
- `.github/workflows/cloud-deployment.yml` - Replaced with v2 version

**Added:**
- `.github/workflows/cloud-artifact.yml` - New workflow for artifact upload

### Workflow Flow Changes

**V1 Flow:**
```
Build → Cloud Sync → Cloud Deployment
```

**V2 Flow:**
```
Build → Cloud Sync → Cloud Artifact → Cloud Deployment
```

The artifact upload is now a separate step that produces an `artifactId` which is then passed to the deployment step.

## Configuration Required

### GitHub Repository Variables
A new repository variable must be configured:

**Variable Name**: `TARGET_ENVIRONMENT_ALIAS`
**Value**: `live`
**Location**: GitHub Repository → Settings → Security → Secrets and variables → Actions → Variables tab

This variable specifies which Umbraco Cloud environment to deploy to.

### Existing GitHub Secrets (unchanged)
These secrets remain the same:
- `UMBRACO_CLOUD_PROJECT_ID`
- `UMBRACO_CLOUD_CICD_API_KEY`

## Custom Configuration Preserved

### Git Configuration for Cloud Sync
The cloud-sync workflow has been customized to use specific git user information for commits coming from Umbraco Cloud:

```yaml
gitName: Umbraco Cloud Github Sync
gitEmail: github-actions@h-madsen.dk
```

This ensures commits synced from Umbraco Cloud are clearly identified in the git history.

## Deployment Options

The deployment workflow now supports additional configuration options in `main.yml`:

- `noBuildAndRestore: 0` - Set to 1 to skip build and restore in Umbraco Cloud
- `skipVersionCheck: 0` - Set to 1 to skip version checking

Currently both are set to 0 (false) to maintain default behavior.

## Testing Recommendations

Before merging to main, test the following:

1. **Build Workflow**: Ensure the solution builds successfully
2. **Cloud Sync**: Verify changes from Umbraco Cloud are synchronized correctly
3. **Artifact Upload**: Check that artifacts are uploaded with correct metadata
4. **Deployment**: Confirm deployment completes successfully to the 'live' environment
5. **Application Functionality**: Test the deployed application in Umbraco Cloud

## Rollback Procedure

If issues arise, the v1 API is still supported:

1. Revert this branch/commit
2. V1 endpoints will continue to work without deprecation timeline
3. All v1 scripts are preserved in git history

## Additional Resources

- **Umbraco CI/CD Documentation**: https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd
- **Migration Guide**: https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd/samplecicdpipeline/migrate
- **Sample Repository**: https://github.com/umbraco/Umbraco.Cloud.CICDFlow.Samples

## Notes

- All workflows are now enabled (build, sync, artifact upload, deployment)
- Workflows only run on the `main` branch (except build which runs on all branches)
- Manual workflow dispatch is available for testing
- The migration maintains backward compatibility with existing secrets and configurations
