---
description: Shared rules for all AI agents working on MediaSet project
---

# MediaSet Shared Guidelines

This document contains rules and conventions shared across all AI agents (GitHub Copilot, Claude, etc.) working on the MediaSet project.

## Project Overview

MediaSet is a full-stack application for managing personal media collections.

**Tech Stack:**
- **Backend:** .NET 10 minimal API with MongoDB
- **Frontend:** Remix.js with TypeScript and Tailwind CSS
- **Testing:** NUnit (backend), Vitest + React Testing Library (frontend)

**Architecture:**
- Vertical slice architecture with minimal API route groups (organized by feature in `/Features`)
- Entity groups: `/Books`, `/Movies`, `/Games`, `/Musics`; additional groups include `/health`, `/stats`, `/metadata`, `/lookup`, `/config`, and `/images`
- MongoDB for data persistence
- Strategy pattern for metadata lookup (books, movies, games, music)
- In-memory caching with MemoryCache
- File-based image storage
- Host-run API/frontend development with containerized MongoDB; container-based production deployment

## Branch Protection (CRITICAL)

**⚠️ NEVER commit directly to `main` ⚠️**

- Branch from latest `main`, preserving unrelated changes. Use a change-type prefix
  such as `feat/`, `fix/`, `docs/`, `chore/`, or `refactor/`; never `codex/`.
- All changes must go through feature branches
- Pull requests are required for merging to `main`
- Do not rewrite published history without explicit authorization; use
  `--force-with-lease` for an authorized rewrite.

## Commit Conventions

**Format:**
- Follow Conventional Commits: `type(scope): description`
- Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`, `ci`, `build`
- Example: `feat(api): add barcode lookup closes #228`

**Requirements:**
- **Subject line MUST be ≤ 72 characters**
- Reference issues: use `closes #N` for final commits, `refs #N` for intermediate commits
- Add additional context in the body if needed
- Add `[AI-assisted]` trailer
- Do not include a `Codex-Session:` trailer in commits or PR descriptions.

## Commit Signing

- **NEVER bypass GPG/SSH signing if signing is configured**
- If GPG signing fails, resolve the signing issue rather than using `--no-gpg-sign` or `--no-verify`
- Commits must be properly signed to maintain repository integrity and security
- Preserve the user's configured Git name, email, and signing key for both author
  and committer. API/bot identities do not replace the user's commit identity.

## AI Agent Workflow

When working on tasks that require code changes:

An explicitly assigned implementation task authorizes inspection, branching,
editing, formatting, and testing without another design-approval prompt. Ask
about genuinely unresolved scope, and preserve unrelated user changes.

By default, present the verified changes for approval before committing and
pushing. Wait for signing/passphrase input when needed. Explicit authorization
for a broader workflow (including an end-to-end ticket workflow) takes precedence;
do not ask again for authorization already given. Conversational approval is a
workflow rule, not a mechanical hook gate on ordinary feature-branch operations.

PR titles use `type(scope): description`; describe the resulting behavior and
validation and reference issues with `closes #N` or `refs #N` as appropriate.

## Agent onboarding and production safety

Read [Linux agent onboarding](../../Development/AGENT_SETUP.md) for passive diagnosis,
explicit dependency bootstrap, native hook activation, and current migration
limits. Personal integrations, context-mode, and personal skills are optional.
Project startup must not install dependencies, format, test, or start services.

Use only infrastructure within the app's authorized scope. Shared production
containers, volumes, images, compose files, proxies, and host configuration need
explicit authorization before changes. Do not restart/update existing services,
perform broad cleanup, expose new public interfaces, or escalate host privileges
without explicit authorization. Keep private hosts, SSH identities, credentials,
personal services, and account-specific details in personal instructions, outside
public configuration. Development runtime provisioning follows the existing
development guide; agent setup does not provision it.

## Project reference

Read [project architecture](../../Development/ARCHITECTURE.md) for the directory
map, route/data patterns, optional integrations, and image storage. Before editing
C# read [backend instructions](backend.instructions.md); before editing TypeScript
or TSX read [frontend instructions](frontend.instructions.md).

## Local Configuration (User Secrets)

Sensitive settings (API keys, internal URLs, credentials) are never stored in committed config files. Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to override settings locally.

```bash
# Set a user secret (nested keys use ":" as separator)
dotnet user-secrets set "SectionName:Key" "value" --project MediaSet.Api/

# List all configured secrets
dotnet user-secrets list --project MediaSet.Api/

# Remove a secret
dotnet user-secrets remove "SectionName:Key" --project MediaSet.Api/
```

**Example — enabling Seq logging locally:**
```bash
dotnet user-secrets set "ExternalLogging:Enabled" "true" --project MediaSet.Api/
dotnet user-secrets set "ExternalLogging:SeqUrl" "http://<your-seq-host>:5341" --project MediaSet.Api/
```

User secrets override `appsettings.json` and `appsettings.Development.json` at runtime and are stored outside the repo at `~/.microsoft/usersecrets/<id>/secrets.json`. They are never committed.

## Development Commands

For deliberate scoped frontend ESLint/Prettier fixes, run
`python3 -B scripts/agents/format.py`; see
[formatting modes and migration](../../Development/AGENT_FORMATTING.md).
Review the resulting diff and preserve unrelated user edits before staging.
Use explicit file selection when the default change set includes unrelated work.

Complete first setup in [Development/DEVELOPMENT.md](../../Development/DEVELOPMENT.md)
(database credentials via .env and .NET User Secrets; restore/npm ci).

```bash
# Database only; API and frontend run on the host
podman-compose -f docker-compose.dev.yml up -d
dotnet watch --project MediaSet.Api run
# In another terminal, from MediaSet.Remix:
npm run dev
# Optional mobile HTTPS after certificate setup:
npm run dev:mobile
# Stop applications with Ctrl+C, then optionally stop MongoDB:
podman-compose -f docker-compose.dev.yml stop mongodb
```

VS Code Run and Debug provides Launch API, Launch UI, and Launch API + UI,
plus mobile HTTPS launches. Run Task provides Watch API, Start UI, and
Start API + UI. Do not reintroduce the retired dev.sh orchestration or application
development containers. Never prune images or clear caches during startup.

## Testing Commands

**Backend (NUnit):**
```bash
# Run all tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Run specific test file
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName"

# Run specific test method
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

**Frontend (Vitest + React Testing Library):**
```bash
# Run all tests
cd MediaSet.Remix && npm test

# Run tests in watch mode
cd MediaSet.Remix && npm test -- --watch

# Run specific test file
cd MediaSet.Remix && npm test -- path/to/test-file.test.ts

# Build for production
cd MediaSet.Remix && npm run build
```

## General Code Quality

- Use meaningful variable and function names that clearly indicate purpose
- Keep functions small and focused on a single responsibility
- Add comments only for complex logic, not obvious code
- Remove unused imports, variables, and code
- Remove outdated comments
- Use const/final for values that don't change, let/var for values that do

## Type Safety

- Always use TypeScript/C# type annotations
- Avoid `any` type in TypeScript; prefer `unknown` if type is truly unknown
- Define interfaces for all data structures
- Use union types and type guards where appropriate
- Prefer specific types over generic types

## Testing Practices

- Follow AAA pattern: **Arrange** (setup), **Act** (execute), **Assert** (verify)
- Test edge cases and error scenarios
- Keep tests focused and single-purpose
- Write test names that describe behavior clearly
- Always include test updates with code changes
