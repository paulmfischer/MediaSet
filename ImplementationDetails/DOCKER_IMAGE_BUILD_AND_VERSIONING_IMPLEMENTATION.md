# Docker Image Build & Versioning — Implementation Plan

This document describes how MediaSet will build, version, and publish production-ready Docker images for both the API (.NET) and the UI (Remix) automatically on merges and releases.

## Goals

- Publish production-ready images for both components:
  - API: `MediaSet.Api`
  - UI: `MediaSet.Remix`
- Push to a public registry so users can run MediaSet without building locally.
- Implement clear, predictable versioning and tagging:
  - Support SemVer releases (starting at v0.x, aiming for 1.0).
  - Always include a commit SHA tag for traceability.
  - Provide a rolling tag for the default branch (edge/nightly).
- Keep CI fast and reproducible using BuildKit cache.

## Registries and Repositories

Primary: GitHub Container Registry (GHCR) — works with Docker and Podman

- GHCR namespace: `<github_owner>` (the GitHub org/user that owns this repo)
- Repositories (auto-created on first push):
  - `ghcr.io/<github_owner>/mediaset-api`
  - `ghcr.io/<github_owner>/mediaset-ui` (or `mediaset-remix`; choose one and keep consistent)

## Versioning & Tagging Strategy

We’ll support both SemVer and SHA-based tagging, plus a rolling tag for the default branch. Tags are generated automatically by the CI using repository state and Git refs.

- On pull_request (no push to registry):
  - Build for validation only (no push).
- On push to `main` (merge):
  - Tags: `edge`, `sha-<short>` (e.g., `sha-a1b2c3d`).
  - Purpose: provide a fast-moving image for testers and early adopters.
- On push of a Git tag matching `v*` (e.g., `v0.1.0`):
  - Tags: `v0.1.0`, `0.1`, `0`, `latest`, and `sha-<short>`.
  - Rationale:
    - Full version: exact reproducibility.
    - Minor and major: convenient pinning strategy.
    - `latest`: points to the most recent stable release.
    - `sha-*`: maps back to a precise commit.

Notes
- Start at `v0.x` until 1.0 is ready.
- Optionally adopt Conventional Commits and automate version bumps with a release bot later.

## Build Targets

- Platform: `linux/amd64` only (multi-arch support may be added in the future)
- OCI labels: Provide provenance metadata (title, description, version, revision, source, authors, url, created) via `docker/metadata-action`.
- Image names (examples):
  - API: `ghcr.io/<github_owner>/mediaset-api`
  - UI: `ghcr.io/<github_owner>/mediaset-ui`

## Source Layout & Contexts

- API
  - Context: repository root `.`
  - Dockerfile: `MediaSet.Api/Dockerfile`
  - Expectation: multi-stage Dockerfile producing a minimal runtime image.
- UI (Remix)
  - Context: `MediaSet.Remix`
  - Dockerfile: `MediaSet.Remix/Dockerfile`
  - Expectation: multi-stage build to compile assets and serve with a production server (Node or adapter).

If needed later, we can add build args (e.g., `NODE_ENV=production`, `VITE_*` env), but initial rollout uses existing Dockerfiles.

## Required GitHub Secrets

- None required for GHCR when using the built-in `GITHUB_TOKEN`.
- Ensure workflow permissions include `packages: write` and `contents: read` (set in workflow `permissions:` block).

Optional future:
- `CR_PAT`: a Personal Access Token if pushing to GHCR from outside GitHub Actions or from other automation

## GitHub Actions — API Workflow (example)

File: `.github/workflows/docker-api.yml`

```yaml
name: Docker - API

on:
  pull_request:
    paths:
      - 'MediaSet.Api/**'
      - '.github/workflows/docker-api.yml'
  push:
    branches:
      - main
    tags:
      - 'v*'

permissions:
  contents: read
  packages: write

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Docker meta
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: |
            ghcr.io/${{ github.repository_owner }}/mediaset-api
          tags: |
            type=ref,event=tag
            type=raw,value=edge,enable=${{ github.ref == 'refs/heads/main' }}
            type=sha,format=short

      - name: Login to GHCR
        if: github.event_name != 'pull_request'
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and (conditionally) push
        uses: docker/build-push-action@v6
        with:
          context: .
          file: MediaSet.Api/Dockerfile
          platforms: linux/amd64
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Add semver convenience tags on release
        if: startsWith(github.ref, 'refs/tags/v')
        run: |
          VERSION="${GITHUB_REF#refs/tags/v}"
          MAJOR=$(echo "$VERSION" | cut -d. -f1)
          MINOR=$(echo "$VERSION" | cut -d. -f1-2)
          IMAGE="ghcr.io/${{ github.repository_owner }}/mediaset-api"
          docker buildx imagetools create -t "$IMAGE:$MINOR" -t "$IMAGE:$MAJOR" -t "$IMAGE:latest" $(echo "${{ steps.meta.outputs.tags }}" | tr '\n' ' ')
```

Notes
- The `docker/metadata-action` will emit the `vX.Y.Z` tag automatically on tag events, `edge` on `main`, and a `sha-<short>` tag.
- The imagetools step adds `X.Y`, `X`, and `latest` pointers when a `v*` tag is pushed.
- Only `linux/amd64` platform is supported initially.

## GitHub Actions — UI Workflow (example)

File: `.github/workflows/docker-ui.yml`

```yaml
name: Docker - UI

on:
  pull_request:
    paths:
      - 'MediaSet.Remix/**'
      - '.github/workflows/docker-ui.yml'
  push:
    branches:
      - main
    tags:
      - 'v*'

permissions:
  contents: read
  packages: write

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Docker meta
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: |
            ghcr.io/${{ github.repository_owner }}/mediaset-ui
          tags: |
            type=ref,event=tag
            type=raw,value=edge,enable=${{ github.ref == 'refs/heads/main' }}
            type=sha,format=short

      - name: Login to GHCR
        if: github.event_name != 'pull_request'
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and (conditionally) push
        uses: docker/build-push-action@v6
        with:
          context: MediaSet.Remix
          file: MediaSet.Remix/Dockerfile
          platforms: linux/amd64
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Add semver convenience tags on release
        if: startsWith(github.ref, 'refs/tags/v')
        run: |
          VERSION="${GITHUB_REF#refs/tags/v}"
          MAJOR=$(echo "$VERSION" | cut -d. -f1)
          MINOR=$(echo "$VERSION" | cut -d. -f1-2)
          IMAGE="ghcr.io/${{ github.repository_owner }}/mediaset-ui"
          docker buildx imagetools create -t "$IMAGE:$MINOR" -t "$IMAGE:$MAJOR" -t "$IMAGE:latest" $(echo "${{ steps.meta.outputs.tags }}" | tr '\n' ' ')
```

## Example docker-compose (production)

Add a production compose to show how to run the published images. File: `docker-compose.prod.yml` (to be added in a later PR once images exist).

```yaml
services:
  api:
    image: ghcr.io/<github_owner>/mediaset-api:v0.1.0 # or :latest / :edge
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - ASPNETCORE_ENVIRONMENT=Production
      # Mongo connection, etc.
      - MONGO__CONNECTIONSTRING=mongodb://mongo:27017
      - MONGO__DATABASE=mediaset
    ports:
      - "8080:8080"
    depends_on:
      - mongo

  ui:
    image: ghcr.io/<github_owner>/mediaset-ui:v0.1.0 # or :latest / :edge
    environment:
      # Example if the UI needs API URL at runtime or build time
      - API_BASE_URL=http://api:8080
    ports:
      - "3000:3000"
    depends_on:
      - api

  mongo:
    image: docker.io/library/mongo:7
    volumes:
      - ./data/mongodb:/data/db
    ports:
      - "27017:27017"
```

## Rollout Steps

1. No manual repo creation needed: GHCR repositories are created on first push.
2. Ensure workflow `permissions` include `packages: write` and `contents: read` (already specified in examples).
3. Commit the two workflow files under `.github/workflows/`.
4. Open a PR; ensure PR builds run successfully (no push to registry).
5. Merge to `main`; verify `edge` and `sha-*` images appear in GHCR under the repo Packages tab.
6. After first push, set the GHCR packages (`mediaset-api` and `mediaset-ui`) visibility to Public in the GitHub UI.
7. Cut the first release tag `v0.1.0`; verify `v0.1.0`, `0.1`, `0`, `latest`, and `sha-*` tags exist for both images.
8. Add `docker-compose.prod.yml` and update docs with usage instructions.

## Acceptance Criteria

- CI builds both API and UI Docker images on PR (no push), on merges to main (push `edge` + `sha-*`), and on version tags (push SemVer + `latest` + `sha-*`).
- Images are built for `linux/amd64` platform.
- Images carry useful OCI labels (version, source, revision, etc.).
- Public documentation shows how to pull and run the images.

## Risks & Mitigations

- Registry rate limits or auth issues: use a bot account/token; set retries on push.
- Tag drift: tags are generated from Git refs to avoid manual inconsistencies.
- Dockerfile mismatches: if runtime images aren't minimal/secure, tighten base images and scan (Trivy) in a follow-up.

## Future Enhancements

- Multi-architecture support (linux/arm64).
- Mirror to Docker Hub or Quay.
- SBOM and image signing (cosign/Provenance/SLSA).
- Automated versioning with Conventional Commits (release-please/semantic-release).
- Registry retention policy and cleanup of `edge` history.
