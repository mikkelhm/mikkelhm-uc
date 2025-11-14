# Pull Request: Migrate Umbraco Cloud CI/CD from v1 to v2 API

## Summary

This PR migrates the CI/CD pipeline from Umbraco Cloud API v1 to v2, bringing improved artifact management, multi-environment support, and better deployment flexibility.

## Key Changes

### 🔄 API Migration
- **From**: Umbraco Cloud CI/CD Flow API v1
- **To**: Umbraco Cloud CI/CD Flow API v2

### 📁 Files Changed (14 files, +719/-329 lines)

**PowerShell Scripts:**
- ❌ Deleted 2 v1 scripts (`Add-DeploymentPackage.ps1`, `New-Deployment.ps1`)
- ✅ Added 2 new v2 scripts (`Add-DeploymentArtifact.ps1`, `Apply-Patch.ps1`)
- 🔄 Updated 4 existing scripts to v2 versions

**GitHub Workflows:**
- ✅ Added: `cloud-artifact.yml` - New workflow for decoupled artifact upload
- 🔄 Updated: `cloud-deployment.yml` - Now requires `artifactId` input
- 🔄 Updated: `cloud-sync.yml` - V2 version with preserved custom git config
- 🔄 Updated: `main.yml` - Re-enabled all workflows (build, sync, artifact, deploy)

**Documentation:**
- 📝 Added: `.github/CICD_V2_MIGRATION.md` - Comprehensive migration guide

### ✨ New Features in V2

1. **Multi-environment targeting**: Explicit deployment to 'live' environment via `TARGET_ENVIRONMENT_ALIAS`
2. **Decoupled artifact management**: Upload artifact once, deploy multiple times if needed
3. **Better deployment control**: New options for `noBuildAndRestore` and `skipVersionCheck`
4. **Improved API structure**: More RESTful design with better separation of concerns

### 🔧 Configuration Required

**⚠️ IMPORTANT: Before merging, add this GitHub repository variable:**

1. Navigate to: Settings → Security → Secrets and variables → Actions → **Variables** tab
2. Click "New repository variable"
3. Set:
   - **Name**: `TARGET_ENVIRONMENT_ALIAS`
   - **Value**: `live`

**Existing secrets remain unchanged:**
- ✅ `UMBRACO_CLOUD_PROJECT_ID`
- ✅ `UMBRACO_CLOUD_CICD_API_KEY`

### 🎯 Workflow Changes

**Old v1 flow:**
```
Build → Cloud Sync → Cloud Deployment
```

**New v2 flow:**
```
Build → Cloud Sync → Upload Artifact → Deploy to Cloud
          ↓              ↓                 ↓
       (checks)    (get artifactId)  (deploy to 'live')
```

### ✅ Preserved Customizations

- Custom git user configuration for cloud sync commits:
  - Name: `Umbraco Cloud Github Sync`
  - Email: `github-actions@h-madsen.dk`

### 🧪 Testing Plan

- [x] All v1 scripts replaced with v2 versions
- [x] Custom git configuration preserved
- [x] All workflows re-enabled in main.yml
- [x] Documentation created
- [ ] Add `TARGET_ENVIRONMENT_ALIAS` variable
- [ ] Test build workflow execution
- [ ] Test cloud sync functionality
- [ ] Test artifact upload
- [ ] Test deployment to 'live' environment
- [ ] Verify application in Umbraco Cloud

### 📚 Documentation

Full migration details are available in `.github/CICD_V2_MIGRATION.md` including:
- Complete file-by-file breakdown
- Configuration requirements
- Testing recommendations
- Rollback procedure (v1 API still works!)

### 🔙 Rollback

If issues arise, the v1 API remains supported with no deprecation timeline. Simply revert this PR to restore v1 functionality.

### 📖 References

- [Umbraco CI/CD Documentation](https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd)
- [Migration Guide](https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd/samplecicdpipeline/migrate)
- [Sample Repository](https://github.com/umbraco/Umbraco.Cloud.CICDFlow.Samples)

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
