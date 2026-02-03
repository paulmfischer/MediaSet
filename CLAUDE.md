# MediaSet

Full-stack application for managing personal media collections.

## Tech Stack

- **Backend:** .NET 10 minimal API with MongoDB
- **Frontend:** Remix.js with TypeScript and Tailwind CSS
- **Testing:** NUnit (backend), Vitest + React Testing Library (frontend)

## Project Structure

- `MediaSet.Api/` — .NET backend API (Models, Services, Helpers, endpoints)
- `MediaSet.Api.Tests/` — Backend unit tests (NUnit)
- `MediaSet.Remix/` — Remix.js frontend UI
- `Development/` — Docker Compose and local development configuration
- `Setup/` — Production setup and deployment files
- `assets/` — Static assets

## Branching & Workflow (CRITICAL)

**⚠️ NEVER commit directly to `main` ⚠️**

Always use feature branches:
```bash
# Create a new feature branch
git checkout -b feature/description-issue-number

# Or for bug fixes
git checkout -b fix/description-issue-number
```

**Workflow:**
1. Sync with main: `git checkout main && git pull`
2. Create feature branch: `git checkout -b feature/your-feature`
3. Make changes and commit to feature branch
4. Push feature branch: `git push -u origin feature/your-feature`
5. Create Pull Request (user responsibility)
6. Merge to main via PR only (never direct push)

## Code Style & Conventions

Before making changes, read and follow these instruction files:
- `.github/copilot-instructions.md` — project-wide rules (branching, commits, workflow)
- `.github/instructions/backend.instructions.md` — .NET code style (applies to `*.cs` files)
- `.github/instructions/frontend.instructions.md` — Remix/TypeScript code style (applies to `*.ts`, `*.tsx` files)

## Development Commands

```bash
# Start entire dev environment
./dev.sh start

# Start individual components
./dev.sh start api           # Backend API only
./dev.sh start frontend      # Frontend UI only

# Restart / stop components
./dev.sh restart api
./dev.sh stop frontend
```

## Testing

```bash
# Backend tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Frontend tests
cd MediaSet.Remix && npm test

# Frontend build
cd MediaSet.Remix && npm run build
```

## Commit Conventions

- Follow Conventional Commits: `type(scope): description`
- Subject line must be ≤ 72 characters
- Reference issues: `closes #N` (final), `refs #N` (intermediate)
- Never commit directly to `main` — use feature branches
