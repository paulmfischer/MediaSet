---
description: MediaSet project guidelines for code generation and modifications
applyTo: "**"
---

# GitHub Copilot Instructions for MediaSet

MediaSet is a full-stack application for managing personal media collections. The application consists of a .NET 9.0 backend API and a Remix.js frontend UI.

## Critical Rules

### Branch Protection
- **NEVER commit to `main`**
- Always create a feature branch: `git checkout -b feature/description` or `fix/description`

### Commit Requirements
- Follow Conventional Commits: `type(scope): description`
  - Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`, `ci`, `build`
  - Example: `feat(api): add barcode lookup closes #228`
- **Subject line MUST be ≤ 72 characters**
- Reference issues: use `closes #N` for final commits, `refs #N` for intermediate commits
- Add additional context in the body if needed
- Add `[AI-assisted]` tag or `Co-authored-by: GitHub Copilot <copilot@github.com>` trailer

### Commit Signing
- **NEVER bypass GPG/SSH signing if signing is configured**
- If GPG signing fails, resolve the signing issue rather than using `--no-gpg-sign` or `--no-verify`
- Commits must be properly signed to maintain repository integrity and security

### Agent Workflow
- **Sync with main**: Switch to `main` branch and pull the latest changes
- **Propose changes first**: Show the user the planned changes before implementation
- **Wait for approval**: Confirm the user approves the approach
- **Create and commit**: Only create commits once the user confirms the changes are correct, waiting for passphrase input if needed
- **Push to feature branch**: Agent pushes to the feature branch only
- **User responsibility**: User creates PR and handles merge to `main`

## Instruction Files (Consult before editing) ✅

Agents must consult the project code-style and conventions documents before proposing or making changes:

- `.github/instructions/backend.instructions.md` — Backend guidelines (applies to `**/*.cs`)
- `.github/instructions/frontend.instructions.md` — Frontend guidelines (applies to `**/*.ts, **/*.tsx`)

These files contain file- and language-specific rules (naming, formatting, testing, commit rules). Always follow them when proposing and implementing changes.

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Classes/Components | PascalCase | `BookController`, `MovieCard` |
| Functions/Variables | camelCase | `fetchBooks`, `formatTitle` |
| Constants | SCREAMING_SNAKE_CASE | `MAX_ITEMS`, `DEFAULT_SORT` |
| Interfaces | PascalCase | `IBook`, `MovieCardProps` |

## Code Quality

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

# Backend tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Frontend tests
cd MediaSet.Remix && npm test

# Frontend build
cd MediaSet.Remix && npm run build
```