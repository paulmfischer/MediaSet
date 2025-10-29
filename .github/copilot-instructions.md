# GitHub Copilot Instructions for MediaSet

This file provides context and instructions for GitHub Copilot to better understand and assist with the MediaSet codebase.

## ⚠️ CRITICAL RULES

### 1. Branch Protection - MANDATORY WORKFLOW
**🚨 ABSOLUTELY NO COMMITS TO THE `main` BRANCH - NO EXCEPTIONS 🚨**

**BEFORE making ANY code changes, file edits, or commits, you MUST:**
1. **FIRST**: Check current branch with `git branch --show-current`
2. **IF on `main`**: IMMEDIATELY create and switch to a new branch:
   ```bash
   git checkout -b feature/<short-description>
   # OR
   git checkout -b fix/<short-description>
   # OR
   git checkout -b chore/<short-description>
   ```
3. **ONLY THEN**: Proceed with making changes
4. Commit all changes to the feature branch
5. Push the feature branch: `git push origin <branch-name>`
6. Open a Pull Request (PR) to merge into `main`

**Branch naming conventions:**
- `feature/<short-description>` for new features
- `fix/<short-description>` for bug fixes  
- `chore/<short-description>` for maintenance tasks

**⚠️ IF YOU ARE CURRENTLY ON `main`, STOP AND CREATE A BRANCH FIRST!**

### 2. Code Style Adherence
**ALL code changes MUST strictly adhere to the project's code style guidelines:**
- Backend API: See [code-style-api.md](code-style-api.md)
- Frontend UI: See [code-style-ui.md](code-style-ui.md)

### 3. Commit Message Attribution
**ALL commits that involve AI assistance or code generation MUST be attributed:**
- Include `Co-authored-by: GitHub Copilot <copilot@github.com>` in commit message
- OR add `[AI-assisted]` tag to commit subject line
- Example: `feat: add book filtering [AI-assisted]` or include co-author trailer

## Project Overview

MediaSet is a full-stack application for managing personal media collections (books and movies). The application consists of:

- Backend API (.NET 8.0) in `MediaSet.Api/`
- Frontend UI (Remix.js) in `MediaSet.Remix/`

## Architecture

### Backend (MediaSet.Api)

- **Framework**: .NET 8.0 Web API
- **Database**: MongoDB
- **Key Components**:
  - `EntityApi.cs`: Base API functionality for media entities
  - `EntityService.cs`: Core service for media entity operations
  - `MetadataService.cs`: Handles metadata operations
  - `StatsService.cs`: Manages statistics
  - `OpenLibraryClient.cs`: Integration with OpenLibrary API
  - Models:
    - `Book.cs`: Book entity model
    - `Movie.cs`: Movie entity model
    - `IEntity.cs`: Base interface for media entities

### Frontend (MediaSet.Remix)

- **Framework**: Remix.js with TypeScript
- **Styling**: Tailwind CSS
- **Key Components**:
  - `app/routes/`: Route components following Remix conventions
  - `app/components/`: Reusable UI components
  - `app/models.ts`: TypeScript interfaces and types
  - `app/*-data.ts`: Data fetching and mutation functions

## Development Commands

### Backend API (MediaSet.Api)

**Build:**
```bash
dotnet build MediaSet.Api/MediaSet.Api.csproj
```

**Run (Development):**
```bash
dotnet watch run --project MediaSet.Api/MediaSet.Api.csproj
```
Or use VS Code task: `watch-api`

**Run (Production):**
```bash
dotnet run --project MediaSet.Api/MediaSet.Api.csproj
```

**Test:**
```bash
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj
```

**Publish:**
```bash
dotnet publish MediaSet.Api/MediaSet.Api.csproj
```

**Docker (Development):**
```bash
./dev.sh start api
```

### Frontend UI (MediaSet.Remix)

**Install Dependencies:**
```bash
cd MediaSet.Remix && npm install
```

**Run (Development):**
```bash
cd MediaSet.Remix && npm run dev
```
Or use VS Code task: `dev-remix`

**Build:**
```bash
cd MediaSet.Remix && npm run build
```

**Test:**
```bash
cd MediaSet.Remix && npm test
```

**Type Check:**
```bash
cd MediaSet.Remix && npm run typecheck
```

**Lint:**
```bash
cd MediaSet.Remix && npm run lint
```

**Docker (Development):**
```bash
./dev.sh start frontend
```

### Full Stack (Both API and UI)

**Run Everything (Development):**
```bash
./dev.sh start all
```

## Common Tasks

When working with this codebase, you might need to:

1. **Add Entity Properties**:
   - Update model in `MediaSet.Api/Models/`
   - Update corresponding TypeScript interface in `MediaSet.Remix/app/models.ts`
   - Update form component in `MediaSet.Remix/app/components/`

2. **Add API Endpoints**:
   - Add route to appropriate controller in `MediaSet.Api/`
   - Add corresponding data function in `MediaSet.Remix/app/entity-data.ts`

3. **Add UI Features**:
   - Create/update components in `MediaSet.Remix/app/components/`
   - Update routes in `MediaSet.Remix/app/routes/`

## Code Style Guidelines

**⚠️ CRITICAL: All code must strictly adhere to the style guidelines below.**

### Backend API (.NET 8.0)
See detailed guidelines in [code-style-api.md](code-style-api.md)

Key points:
- Use file-scoped namespaces
- PascalCase for public members, camelCase for private
- Use `var` and collection initializers (`[]`)
- Constructor injection with `_fieldName` pattern
- Minimal APIs with typed results
- Async/await for all async operations
- Structured logging with named parameters

### Frontend UI (Remix.js/TypeScript)
See detailed guidelines in [code-style-ui.md](code-style-ui.md)

Key points:
- TypeScript for all code
- Follow Remix.js conventions
- PascalCase for components, camelCase for functions
- Use Tailwind CSS for styling
- Function components with hooks
- Proper error handling and loading states

## Testing

### Backend (MediaSet.Api.Tests)
- Uses xUnit test framework
- Consider in-memory MongoDB for integration tests
- Run tests with: `dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`
- Follow AAA pattern (Arrange, Act, Assert)
- Name tests: `MethodName_Scenario_ExpectedResult`

### Frontend (MediaSet.Remix)
- Uses Vitest for unit tests
- Uses React Testing Library for component tests
- Run tests with: `cd MediaSet.Remix && npm test`
- Name tests: `should [expected behavior] when [condition]`

## Git Workflow

### Creating a Branch
**⚠️ CRITICAL: NEVER work directly on `main`**

Before starting any work:
```bash
# Ensure you're on main and up to date
git checkout main
git pull origin main

# Create a new branch with descriptive name
git checkout -b feature/your-feature-name
# OR
git checkout -b fix/your-bug-fix
# OR
git checkout -b chore/your-maintenance-task
```

### Making Commits
All commits involving AI assistance must be attributed:

**Option 1: Add co-author trailer**
```bash
git commit -m "feat: add new feature

Some description of the changes.

Co-authored-by: GitHub Copilot <copilot@github.com>"
```

**Option 2: Add tag in subject line**
```bash
git commit -m "feat: add new feature [AI-assisted]"
```

### Opening a Pull Request
1. Push your branch: `git push origin feature/your-feature-name`
2. Open a PR from your branch to `main`
3. Ensure the PR description summarizes the change and any notable impacts
4. Keep branches focused and small; prefer incremental PRs over large ones
5. Update the branch with the latest `main` before merging (rebase or merge as appropriate)

## Additional Notes

- Docker configurations are available for both API and frontend
- The project uses OpenLibrary API for book metadata
- Stats are automatically calculated and cached
- MongoDB is used for data storage (see `docker-compose.dev.yml` for setup)