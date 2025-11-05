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


### 3. Commit Message Attribution & Issue Reference
**ALL commits that involve AI assistance or code generation MUST be attributed:**
- Include `Co-authored-by: GitHub Copilot <copilot@github.com>` in commit message
- OR add `[AI-assisted]` tag to commit subject line

**If your commit addresses a GitHub issue or task, you MUST reference the issue number:**
- Use keywords like `closes #123`, `fixes #456`, or `refs #789` in the commit message
- Place the reference at the end of the subject line or in the body
- Use `closes` or `fixes` for commits that complete an issue
- Use `refs` for commits that partially address or relate to an issue

**⚠️ CRITICAL: Multi-commit workflow for a single issue:**
- When making MULTIPLE commits for ONE issue, use `refs #N` for all intermediate commits
- ONLY use `closes #N` or `fixes #N` on the FINAL commit that completes the issue
- This ensures the issue stays open until all related work is done
- Examples:
  - First commit: `feat(api): add entity service [AI-assisted] refs #123`
  - Second commit: `test(api): add tests for entity service [AI-assisted] refs #123`
  - Final commit: `docs(api): update README with entity usage [AI-assisted] closes #123`

**Examples:**
- `feat: add book filtering [AI-assisted] closes #228`
- `fix: correct form validation [AI-assisted] fixes #123`
- `refactor: improve service structure [AI-assisted] refs #456`
- Or with co-author trailer in body:
  ```
  feat: add book filtering closes #228
  
  Implements filtering functionality for the book list.
  
  Co-authored-by: GitHub Copilot <copilot@github.com>
  ```


### 4. Conventional Commits & Release-Please Compatibility
**ALL commits MUST follow the Conventional Commits specification for release-please compatibility:**
- Use the format: `type(scope): description`
- Common types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`, `ci`, `build`
- Scope is optional but recommended (e.g., `api`, `ui`, `db`)

**⚠️ CRITICAL: Commit Message Length Limits:**
- **Subject line MUST be 72 characters or less** (release-please requirement)
- If description is too long, use a shorter subject and add details in the body
- Format with body:
  ```
  type(scope): short description closes #N
  
  Longer explanation of the change, why it was needed,
  and any important details or context.
  
  Co-authored-by: GitHub Copilot <copilot@github.com>
  ```

**If your commit is related to a GitHub issue or task:**
- Include the issue reference using keywords: `closes #N`, `fixes #N`, or `refs #N`
- Place at the end of the subject line (if space permits) or in the commit body
- Use `closes`/`fixes` when the commit completes the issue
- Use `refs` when the commit partially addresses or relates to the issue

**Examples:**
- `feat(api): add barcode lookup [AI-assisted] closes #228`
- `fix(ui): correct form validation fixes #123`
- `docs: update README refs #99`
- `test(api): add health tests [AI-assisted] closes #228`
- `chore: update dependencies`
- With body for longer description:
  ```
  feat(api): add filtering closes #228
  
  Implements comprehensive filtering functionality
  for the book list with multiple filter criteria.
  
  Co-authored-by: GitHub Copilot <copilot@github.com>
  ```

**Conventional Commit Types:**
- `feat`: New feature (correlates to SemVer MINOR)
- `fix`: Bug fix (correlates to SemVer PATCH)
- `feat!` or `fix!`: Breaking change (correlates to SemVer MAJOR)
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring without feature changes
- `chore`: Maintenance tasks, dependency updates
- `style`: Code style changes (formatting, missing semicolons, etc.)
- `perf`: Performance improvements
- `ci`: CI/CD configuration changes
- `build`: Build system changes

**Scope examples:** `api`, `ui`, `db`, `workflows`, `deps`

### 5. Testing Requirements
**⚠️ CRITICAL: All code changes MUST include appropriate test updates:**
- When adding new features, add corresponding tests
- When fixing bugs, add tests that verify the fix
- When refactoring, ensure existing tests still pass and update as needed
- Run tests before committing to verify changes work correctly

**Testing commands:**
- Backend: `dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`
- Frontend: `cd MediaSet.Remix && npm test`

**If a commit includes code changes without tests:**
- Either include tests in the same commit, OR
- Make a separate commit for tests with `refs #N` (followed by final commit with `closes #N`)

### 6. User Verification Before Commits
**⚠️ CRITICAL: Always ask user for verification before making commits:**
- After making code changes, summarize what was changed
- Show the proposed commit message(s)
- Ask user: "Are you ready for me to commit these changes?"
- Wait for explicit user confirmation before running `git commit`
- This gives the user a chance to review changes and commit messages

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
**⚠️ CRITICAL: Always ask user for verification before making commits (see section 6 above)**

All commits involving AI assistance must be attributed and follow Conventional Commits:

**Format: `type(scope): description [AI-assisted]`**
- Subject line MUST be 72 characters or less (see section 4 above)
- Include issue references: `closes #N` for final commit, `refs #N` for intermediate commits (see section 3 above)
- Include test updates with code changes (see section 5 above)

**Option 1: Add tag in subject line**
```bash
git commit -m "feat(api): add new feature [AI-assisted] refs #123"
```

**Option 2: Add co-author trailer (useful for longer descriptions)**
```bash
git commit -m "feat(api): add new feature refs #123

Some description of the changes.

Co-authored-by: GitHub Copilot <copilot@github.com>"
```

**Conventional Commit Types:**
- `feat`: New feature (correlates to SemVer MINOR)
- `fix`: Bug fix (correlates to SemVer PATCH)
- `feat!` or `fix!`: Breaking change (correlates to SemVer MAJOR)
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring without feature changes
- `chore`: Maintenance tasks, dependency updates
- `style`: Code style changes (formatting, missing semicolons, etc.)
- `perf`: Performance improvements
- `ci`: CI/CD configuration changes
- `build`: Build system changes

**Scope examples:** `api`, `ui`, `db`, `workflows`, `deps`

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