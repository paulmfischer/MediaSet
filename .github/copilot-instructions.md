---
description: MediaSet project guidelines for code generation and modifications
applyTo: "**"
---

# GitHub Copilot Instructions for MediaSet

MediaSet is a full-stack application for managing personal media collections. The application consists of a .NET 9.0 backend API and a Remix.js frontend UI.

## Critical Rules

### Branch Protection
- **NEVER commit to `main`**
- Always create a feature branch from updated `main`: `git checkout -b feature/description` or `fix/description`
- Push branches and open Pull Requests for code review

### Commit Requirements
- Follow Conventional Commits: `type(scope): description`
  - Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`, `ci`, `build`
  - Example: `feat(api): add barcode lookup closes #228`
- **Subject line MUST be ≤ 72 characters**
- Reference issues: use `closes #N` for final commits, `refs #N` for intermediate commits
- Add additional context in the body if needed
- Add `[AI-assisted]` tag or `Co-authored-by: GitHub Copilot <copilot@github.com>` trailer
- Always verify changes with user before committing

## Project Overview

MediaSet is a full-stack application managing personal media collections.

- Backend: .NET 9.0 Web API with MongoDB
- Frontend: Remix.js with TypeScript and Tailwind CSS

## Common Development Tasks

1. **Add Entity Properties**: Update model → TypeScript interface → form component
2. **Add API Endpoints**: Add controller route → data function in frontend
3. **Add UI Features**: Create components → update routes

## Development Commands

```bash
# Backend build
dotnet build MediaSet.Api/MediaSet.Api.csproj

# Backend tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Frontend build
cd MediaSet.Remix && npm run build
# Frontend tests
cd MediaSet.Remix && npm test

# Full Stack
./dev.sh start
```