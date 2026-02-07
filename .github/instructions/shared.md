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
- Route groups: `/entities`, `/health`, `/stats`, `/metadata`, `/lookup`
- MongoDB for data persistence
- Strategy pattern for metadata lookup (books, movies, games, music)
- In-memory caching with MemoryCache
- File-based image storage
- Container-based development and deployment

## Branch Protection (CRITICAL)

**⚠️ NEVER commit directly to `main` ⚠️**

- Always create a feature branch: `git checkout -b feature/description` or `fix/description`
- All changes must go through feature branches
- Pull requests are required for merging to `main`

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

## Commit Signing

- **NEVER bypass GPG/SSH signing if signing is configured**
- If GPG signing fails, resolve the signing issue rather than using `--no-gpg-sign` or `--no-verify`
- Commits must be properly signed to maintain repository integrity and security

## AI Agent Workflow

When working on tasks that require code changes:

1. **Sync with main**: Switch to `main` branch and pull the latest changes
2. **Propose changes first**: Show the user the planned changes before implementation
3. **Wait for approval**: Confirm the user approves the approach
4. **Create and commit**: Only create commits once the user confirms the changes are correct, waiting for passphrase input if needed
5. **Push to feature branch**: Ask user before pushing to the feature branch
6. **Create pull request**: After pushing, offer to create a PR using `gh pr create` command
   - Use conventional commit format for PR title: `type(scope): description`
   - Include relevant description summarizing changes and motivation
   - Reference related issues with `closes #N` or `refs #N`

## Development Commands

```bash
# Start entire development environment
./dev.sh start

# Start specific components
./dev.sh start api           # Start backend API only
./dev.sh start frontend      # Start frontend UI only

# Restart components
./dev.sh restart api         # Restart backend API
./dev.sh restart frontend    # Restart frontend UI

# Stop components
./dev.sh stop api            # Stop backend API
./dev.sh stop frontend       # Stop frontend UI
```

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
