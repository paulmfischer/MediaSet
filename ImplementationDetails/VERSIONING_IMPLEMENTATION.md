# Versioning Implementation Plan

## Overview

This feature introduces consistent semantic versioning across the repository for both the API (.NET 9) and UI (Remix). The goal is to:
- Establish a clear source of truth for versions (Git tags) starting at v0.1.0 with a path to 1.0.
- Automatically calculate and bump versions from commit history using Conventional Commits.
- Surface the running version in the API and UI for diagnostics and transparency.

Why this matters:
- Enables predictable releases and rollback.
- Makes applications traceable to source and commits.
- Reduces manual work and errors by automating release notes and tag management.
- Provides transparency to users about which version they are running.

## Related Existing Functionality

### Backend Components
- API project: `MediaSet.Api/`
- Dockerfiles: `MediaSet.Api/Dockerfile`, `MediaSet.Api/Dockerfile.dev`
- Health endpoint: `MediaSet.Api/HealthApi.cs` (no version surfaced yet)
- CI: `.github/workflows/pr-checks.yml` (builds/tests but does not version or publish)

### Frontend Components
- UI project: `MediaSet.Remix/`
- Dockerfiles: `MediaSet.Remix/Dockerfile`, `MediaSet.Remix/Dockerfile.dev`
- Build: `npm run build` (no version stamping yet)
- CI: `.github/workflows/pr-checks.yml`

### Infrastructure/Configuration
- Repository uses GitHub Actions
- No existing image publishing workflow
- Uses Conventional Commits for automated versioning

## Requirements

### Functional Requirements
1. Version numbers follow SemVer and are derived from Git tags; start at v0.1.0.
2. Automated release process proposes version bumps and changelog via PR; merging creates a Git tag and GitHub Release.
3. API exposes version information via the `/health` endpoint.
4. UI displays build version in a footer component.
5. Single repo-level version used across both API and UI.

### Non-Functional Requirements
- Reproducible builds with version metadata embedded in assemblies and UI build.
- Minimal manual steps; fully automated once merged.
- Clear rollback path: deploy prior tag.
- Version information is easily discoverable for debugging and support.

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models
- None required.

#### New/Modified Services
- Add a lightweight version provider service: resolves version from AssemblyInformationalVersion set by MinVer.

#### New/Modified API Endpoints
- Extend `/health` to include a `version` field.

Behavior:
- `version`: Semantic version from MinVer (based on Git tags) or `0.0.0-local` when not tagged.
- `commit`: short SHA from AssemblyInformationalVersion metadata.
- `buildTime`: UTC timestamp embedded at build.

#### Database Changes
- None.

### Frontend Changes (MediaSet.Remix)

#### New/Modified Routes
- None required.

#### New/Modified Components
- Add or enhance Footer component to display app version from `VITE_APP_VERSION` environment variable.
- Footer should be displayed on all pages.
- Version should display in all environments (development and production).

#### New/Modified Data Functions
- None.

#### Type Definitions
- None required.

### Infrastructure/Configuration

#### Version Source of Truth
- Use GitHub tags + Conventional Commits to drive SemVer.
- Introduce Release automation using `release-please` for multi-component versioning.
- Start at v0.1.0 with path to 1.0.
- Use single repo-level version applied to both API and UI.

#### New/Modified Workflows
1. `.github/workflows/release-please.yml`
   - Triggers on pushes to `main`.
   - Scans commits using Conventional Commits format, opens/updates a release PR with the next version and changelog.
   - On merge, creates Git tag `vX.Y.Z` and a GitHub Release.

#### Build-time Metadata
- .NET: Use MinVer NuGet package to automatically set `AssemblyInformationalVersion` based on Git tags and commits.
- UI: Set `VITE_APP_VERSION` env var during build from Git tag or MinVer output.

#### .NET Version Stamping
- Use MinVer to infer version from Git tags, starting at v0.1.0.
- Add `MinVer` package to `MediaSet.Api.csproj`.
- MinVer automatically calculates version from tags and annotates assemblies with full version including commit SHA.

## Testing Changes

### Backend Tests (MediaSet.Api.Tests)
- Add tests for version provider (if implemented as a service):
  - returns semantic version when provided via env
  - falls back to assembly informational version
- Integration test for `/health` to include `version` field.

### Frontend Tests (MediaSet.Remix)
- Footer component renders version when `VITE_APP_VERSION` is set.
- Footer component handles missing version gracefully.

### Integration Tests
- Validate that version information is correctly populated in local builds and CI environments.

## Implementation Steps

1. Add release automation
   - Create `release-please.yml` workflow to manage versions and changelog via PR.
   - Configure to start at v0.1.0 and use Conventional Commits.
2. Add .NET version stamping
   - Add MinVer package to `MediaSet.Api.csproj`.
   - Verify `AssemblyInformationalVersion` reflects Git tag and commit SHA.
3. Add version provider service
   - Implement service to read version from assembly metadata.
   - Extend `/health` endpoint to include version information.
4. Add UI version display
   - Create or enhance Footer component to display version.
   - Wire `VITE_APP_VERSION` environment variable into build process.
5. Tests
   - Add backend tests for version provider and health endpoint.
   - Add UI tests for footer component version display.
6. Documentation
   - Update `README.md` with versioning policy, tag scheme, and latest version.
   - Update `.github/copilot-instructions.md` to require Conventional Commits.
   - Document Conventional Commits and release flow for contributors.

## Acceptance Criteria

- [ ] Merges to main open/refresh a release PR proposing the next version and changelog.
- [ ] Tagging a release (v0.1.0+) creates a GitHub Release with changelog.
- [ ] API `/health` response includes `version` field sourced from MinVer assembly metadata.
- [ ] UI Footer component displays `VITE_APP_VERSION` in production builds.
- [ ] `README.md` is updated with versioning policy and latest version information.
- [ ] `.github/copilot-instructions.md` is updated to require Conventional Commits.
- [ ] CI remains green (build, tests) and workflows are documented.

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Incorrect version bumps | Medium | Medium | Use Release Please; enforce Conventional Commits on PRs; manual review of release PR. |
| Breaking changes during v0 | Medium | Medium | Document v0 stability expectations; use patch versions for fixes. |
| Version sync issues between API and UI | Low | Low | Single repo-level version applied to both via Git tags. |

## Open Questions

None - all decisions have been made.

## Dependencies
- GitHub Actions runners
- MinVer NuGet package

## References
- Release Please: https://github.com/google-github-actions/release-please-action
- MinVer: https://github.com/adamralph/minver
- Conventional Commits: https://www.conventionalcommits.org/
