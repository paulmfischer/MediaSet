# .NET 10 Upgrade Implementation Plan

**Issue**: #412  
**Date**: January 22, 2026  
**Status**: Completed

## Overview

This document outlines the detailed plan and execution summary for upgrading the MediaSet project from .NET 9.0 to .NET 10.0. The upgrade affects the backend API, test projects, Docker images, and CI/CD workflows.

## Completion Summary

**Date Completed**: January 22, 2026

The .NET 10 upgrade has been successfully completed. All components have been updated, tests pass, and the application builds without errors.

### Changes Implemented

1. ✅ **SDK Version**: Updated from 9.0.112 to 10.0.101
2. ✅ **Target Frameworks**: Updated from net9.0 to net10.0 in all projects
3. ✅ **ASP.NET Core Packages**: Updated to version 10.0.2
  - Microsoft.AspNetCore.OpenApi: 9.0.9 → 10.0.2
  - Microsoft.AspNetCore.Mvc.Testing: 9.0.0 → 10.0.2
4. ✅ **Swashbuckle.AspNetCore**: Updated from 6.5.0 to 10.1.0
5. ✅ **Docker Images**: Updated all Dockerfiles to use .NET 10 base images
6. ✅ **Code Migrations**: Fixed breaking changes in Swashbuckle/OpenAPI
7. ✅ **Documentation**: Updated README.md references

### Breaking Changes Addressed

**Swashbuckle.AspNetCore v10 / Microsoft.OpenApi v2 Migration**:
- Updated `ParameterSchemaFilter` to use the new OpenAPI v2 model
- Changed method signature from `OpenApiSchema` to `IOpenApiSchema`
- Updated enum handling from `OpenApiString` to `JsonNode`
- Changed type assignment from string to `JsonSchemaType` enum
- Added type casting pattern for property mutations

### Test Results

All tests pass successfully:
- ✅ Build: Successful
- ✅ Unit Tests: All passing
- ✅ Integration Tests: All passing
- ✅ No new warnings introduced

### Commits Made

1. `7b801c6` - chore(config): update SDK to .NET 10.0.101 refs #412
2. `5a31211` - chore(api): update target framework to net10.0 refs #412
3. `f00c5cb` - chore(deps): update ASP.NET Core packages to 10.0 refs #412
4. `69190e5` - chore(deps): update Swashbuckle to 10.1.0 refs #412
5. `af4266c` - chore(docker): update Dockerfiles to .NET 10 refs #412
6. `9fa52d6` - fix(api): update ParameterSchemaFilter for Swashbuckle v10 refs #412
7. `080154f` - docs: update references to .NET 10 refs #412

## Current State Analysis

### .NET Version
- **Current SDK Version**: 9.0.112 (specified in `global.json`)
- **Current Target Framework**: net9.0 (in all `.csproj` files)
- **Current Docker Images**: 
  - `mcr.microsoft.com/dotnet/sdk:9.0`
  - `mcr.microsoft.com/dotnet/aspnet:9.0`

### Projects Using .NET
1. **MediaSet.Api** - Main backend API (ASP.NET Core Web API)
2. **MediaSet.Api.Tests** - Unit and integration tests (NUnit)

### NuGet Dependencies (Current Versions)
- **Microsoft.AspNetCore.OpenApi**: 9.0.9
- **Microsoft.AspNetCore.Mvc.Testing**: 9.0.0
- **Swashbuckle.AspNetCore**: 6.5.0 (not framework-specific)
- **MongoDB.Driver**: 3.0.0 (not framework-specific)
- **SixLabors.ImageSharp**: 3.1.12 (not framework-specific)
- **MinVer**: 6.0.0 (not framework-specific)
- **NUnit**: 4.2.2 (not framework-specific)
- **NUnit3TestAdapter**: 4.6.0 (not framework-specific)
- **Microsoft.NET.Test.Sdk**: 17.11.1 (not framework-specific)
- **Moq**: 4.20.72 (not framework-specific)
- **Bogus**: 35.6.1 (not framework-specific)

### Files to Update

#### Configuration Files
1. `global.json` - SDK version
2. `MediaSet.Api/MediaSet.Api.csproj` - Target framework and dependencies
3. `MediaSet.Api.Tests/MediaSet.Api.Tests.csproj` - Target framework and dependencies

#### Docker Files
4. `MediaSet.Api/Dockerfile` - Production image base
5. `MediaSet.Api/Dockerfile.dev` - Development image base
6. `MediaSet.Api/Dockerfile.bak` - Backup dockerfile

#### CI/CD Workflows
7. `.github/workflows/pr-checks.yml` - Build and test workflow
8. `.github/workflows/docker-api.yml` - Docker image build workflow
9. `.github/workflows/docker-ui.yml` - May reference .NET version indirectly
10. `.github/workflows/release-please.yml` - May reference .NET version

#### Documentation
11. `README.md` - Update references to .NET 9.0 → .NET 10.0
12. Any other documentation mentioning .NET 9

## .NET 10 Information

### Expected Release
.NET 10 is expected to be released in November 2025. As of January 2026, .NET 10 should be Generally Available (GA).

### SDK Version
The specific SDK version will need to be determined based on available releases. Typically:
- Initial GA release: 10.0.100
- Current stable version should be checked at: https://dotnet.microsoft.com/download/dotnet/10.0

### Breaking Changes
Need to review the official migration guide:
- https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0

Common areas to check:
- ASP.NET Core changes
- C# language version changes (likely C# 13)
- API changes in framework libraries
- Obsolete API removals
- Dependency injection changes
- Middleware changes

## Upgrade Steps (Task Breakdown)

### Task 1: Research and Verify .NET 10 Availability
**Description**: Verify that .NET 10 is released and identify the specific SDK version to use.
- Check official .NET website for latest stable SDK version
- Review breaking changes documentation
- Identify any known issues with dependencies
- **Commit Message**: `chore: research .NET 10 upgrade requirements refs #412`

### Task 2: Update SDK Version in global.json
**Description**: Update the SDK version in `global.json` to .NET 10.
- Change `"version"` from `"9.0.112"` to the appropriate 10.0.x version
- Keep `"rollForward": "latestFeature"` setting
- **Commit Message**: `chore(config): update SDK to .NET 10 refs #412`
- **Files**:
  - `global.json`

### Task 3: Update Target Framework in Project Files
**Description**: Update the target framework in all `.csproj` files.
- Change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`
- **Commit Message**: `chore(api): update target framework to net10.0 refs #412`
- **Files**:
  - `MediaSet.Api/MediaSet.Api.csproj`
  - `MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`

### Task 4: Update Microsoft.AspNetCore Dependencies
**Description**: Update Microsoft framework packages to version 10.0.x.
- Update `Microsoft.AspNetCore.OpenApi` from 9.0.9 to 10.0.x
- Update `Microsoft.AspNetCore.Mvc.Testing` from 9.0.0 to 10.0.x
- **Commit Message**: `chore(deps): update ASP.NET Core packages to 10.0 refs #412`
- **Files**:
  - `MediaSet.Api/MediaSet.Api.csproj`
  - `MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`

### Task 5: Update Third-Party NuGet Packages
**Description**: Update third-party packages to versions compatible with .NET 10.
- Check for updates to:
  - `Swashbuckle.AspNetCore` (currently 6.5.0)
  - `Microsoft.NET.Test.Sdk` (currently 17.11.1)
  - Other packages as needed
- Only update if necessary for compatibility or if breaking changes require it
- **Commit Message**: `chore(deps): update third-party packages for .NET 10 refs #412`
- **Files**:
  - `MediaSet.Api/MediaSet.Api.csproj`
  - `MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`

### Task 6: Update Production Dockerfile
**Description**: Update Docker base images for production builds.
- Change `FROM mcr.microsoft.com/dotnet/sdk:9.0` to `mcr.microsoft.com/dotnet/sdk:10.0`
- Change `FROM mcr.microsoft.com/dotnet/aspnet:9.0` to `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Commit Message**: `chore(docker): update production Dockerfile to .NET 10 refs #412`
- **Files**:
  - `MediaSet.Api/Dockerfile`

### Task 7: Update Development Dockerfile
**Description**: Update Docker base images for development environment.
- Change `FROM mcr.microsoft.com/dotnet/sdk:9.0` to `mcr.microsoft.com/dotnet/sdk:10.0`
- **Commit Message**: `chore(docker): update dev Dockerfile to .NET 10 refs #412`
- **Files**:
  - `MediaSet.Api/Dockerfile.dev`
  - `MediaSet.Api/Dockerfile.bak` (if still needed)

### Task 8: Build and Test Locally
**Description**: Verify the application builds and all tests pass locally.
- Run `dotnet restore`
- Run `dotnet build`
- Run `dotnet test`
- Test development environment: `./dev.sh start`
- **Commit Message**: N/A (verification step)

### Task 9: Handle Breaking Changes
**Description**: Fix any code issues caused by .NET 10 breaking changes.
- Address any compilation errors
- Fix deprecated API usage
- Update code patterns if required
- Update middleware configuration if needed
- **Commit Message**: `fix(api): resolve .NET 10 breaking changes refs #412`
- **Files**: TBD based on breaking changes encountered

### Task 10: Update CI/CD Workflows
**Description**: Ensure CI/CD pipelines use .NET 10.
- Workflows use `global.json` via `setup-dotnet@v4` action, so no changes should be needed
- Verify this is working correctly
- **Commit Message**: `chore(ci): verify CI/CD for .NET 10 refs #412`
- **Files**:
  - `.github/workflows/pr-checks.yml` (verification only)
  - `.github/workflows/docker-api.yml` (verification only)

### Task 11: Update Documentation
**Description**: Update all documentation references from .NET 9 to .NET 10.
- Update README.md introduction
- Update Development/DEVELOPMENT.md if needed
- Review other markdown files for version references
- **Commit Message**: `docs: update references to .NET 10 refs #412`
- **Files**:
  - `README.md`
  - Other documentation as needed

### Task 12: Final Integration Testing
**Description**: Run comprehensive tests to ensure everything works.
- Test development environment startup
- Test API endpoints
- Test database connectivity
- Test metadata lookup services
- Run full test suite
- Test Docker production builds
- **Commit Message**: N/A (verification step)

### Task 13: Update Implementation Document
**Description**: Mark this document as completed and summarize outcomes.
- Update status to "Completed"
- Document any issues encountered and resolutions
- Note any code changes made due to breaking changes
- **Commit Message**: `docs: complete .NET 10 upgrade implementation closes #412`
- **Files**:
  - `ImplementationDetails/DOTNET_10_UPGRADE_IMPLEMENTATION.md`

## Dependencies to Update

### Required Updates (Framework-Specific)
| Package | Current Version | Target Version | Notes |
|---------|----------------|----------------|-------|
| Microsoft.AspNetCore.OpenApi | 9.0.9 | 10.0.x | Framework package |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0 | 10.0.x | Framework package |

### Optional Updates (Third-Party)
| Package | Current Version | Latest Compatible | Update? |
|---------|----------------|-------------------|---------|
| Swashbuckle.AspNetCore | 6.5.0 | Check for updates | If needed |
| Microsoft.NET.Test.Sdk | 17.11.1 | Check for updates | If needed |
| MongoDB.Driver | 3.0.0 | Check for updates | Only if issues |
| SixLabors.ImageSharp | 3.1.12 | Check for updates | Only if issues |
| MinVer | 6.0.0 | Check for updates | Only if issues |
| NUnit | 4.2.2 | Check for updates | Only if issues |
| Moq | 4.20.72 | Check for updates | Only if issues |

## Expected Code Changes

### Potential Breaking Changes
Based on typical .NET major version upgrades, we may need to address:

1. **Obsolete API Removals**
   - Check for any obsolete APIs that were marked for removal in .NET 10
   - Update to use replacement APIs

2. **Middleware Registration**
   - Review `Program.cs` for any changes in middleware registration patterns
   - Check for changes in authentication/authorization setup

3. **Dependency Injection**
   - Review service registration patterns
   - Check for changes in DI container behavior

4. **HTTP Client Factory**
   - Review any `HttpClient` usage patterns
   - Check for changes in `IHttpClientFactory` usage

5. **Minimal APIs**
   - Review route registration if using minimal APIs
   - Check for changes in endpoint routing

6. **Testing**
   - Review `IntegrationTestBase.cs` for any test host setup changes
   - Update `WebApplicationFactory` usage if needed

### Known Safe Areas
These areas likely won't require changes:
- MongoDB integration (using official driver)
- Image processing (using third-party library)
- Business logic code
- Entity models
- DTOs and request/response models

## Rollback Plan

If the upgrade causes unforeseen issues:

1. **Immediate Rollback**:
   ```bash
   git revert <commit-hash>
   ```

2. **Revert Multiple Commits**:
   ```bash
   git revert --no-commit <first-commit>..<last-commit>
   git commit -m "revert: rollback .NET 10 upgrade refs #412"
   ```

3. **Branch Protection**: 
   - All changes made on feature branch `feature/dotnet-10-upgrade`
   - Main branch remains on .NET 9 until PR is approved and merged
   - Can abandon feature branch if needed

## Success Criteria

The upgrade is considered successful when:

- ✅ Project builds without errors
- ✅ All unit tests pass
- ✅ All integration tests pass
- ✅ Development environment starts successfully (`./dev.sh start`)
- ✅ API endpoints respond correctly
- ✅ Database connectivity works
- ✅ Metadata lookup services function properly
- ✅ Docker production images build successfully
- ✅ CI/CD pipeline passes (PR checks)
- ✅ Documentation is updated
- ✅ No new warnings introduced (or documented if unavoidable)

## Timeline Estimate

- **Task 1-2**: 30 minutes (research and global.json update)
- **Task 3-5**: 1 hour (project file and dependency updates)
- **Task 6-7**: 30 minutes (Docker updates)
- **Task 8**: 1 hour (local build and test)
- **Task 9**: 0-4 hours (depends on breaking changes encountered)
- **Task 10-11**: 30 minutes (CI/CD and documentation)
- **Task 12**: 1 hour (final testing)
- **Task 13**: 15 minutes (documentation update)

**Total Estimated Time**: 4-8 hours (depending on breaking changes)

## Notes

- This upgrade should be performed on a feature branch
- Each task should be a separate commit when possible
- All commits should reference issue #412
- Final commit should close the issue
- PR should be reviewed before merging to main
- Consider testing in staging environment if available

## References

- [.NET 10 Release Notes](https://github.com/dotnet/core/tree/main/release-notes/10.0)
- [.NET 10 Breaking Changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0)
- [ASP.NET Core 10.0 Migration Guide](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-10)
- [C# 13 Language Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)
