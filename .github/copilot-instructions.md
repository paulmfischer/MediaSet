# GitHub Copilot Instructions for MediaSet

This file provides context and instructions for GitHub Copilot to better understand and assist with the MediaSet codebase.

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

## Code Conventions

### Backend (.NET 8.0)

**File Organization:**
- Use file-scoped namespaces (e.g., `namespace MediaSet.Api.Models;`)
- Group related functionality into folders (Models, Services, Helpers, etc.)
- Use descriptive folder names that reflect their purpose

**Naming Conventions:**
- Use PascalCase for public properties, methods, and classes
- Use camelCase for private fields and parameters
- Use descriptive names (e.g., `entityCollection`, `searchText`, `orderByField`)
- Prefix private fields with underscore when injected via constructor (e.g., `_httpClient`, `_logger`)
- Use plural names for collections (e.g., `Authors`, `Genres`, `Studios`)

**Code Style:**
- Use `var` for local variables when type is obvious from assignment
- Initialize collections with `[]` syntax instead of `new List<T>()`
- Use string interpolation for logging: `logger.LogInformation("Message: {value}", value)`
- Use `string.Empty` instead of `""` for empty string initialization
- Place attributes on separate lines above properties/methods
- Use target-typed `new()` expressions where applicable

**Dependency Injection:**
- Use constructor injection for services
- Store injected dependencies in private readonly fields
- Follow the pattern: `private readonly ServiceType serviceName;`

**API Design:**
- Use minimal APIs with route groups for organization
- Return typed results: `Results<Ok<T>, NotFound>`, `TypedResults.Ok()`, etc.
- Use descriptive route parameter names that match method parameters
- Group related endpoints using `MapGroup()`
- Add appropriate tags for Swagger documentation

**Async/Await:**
- Use async/await for all asynchronous operations
- Suffix async methods with `Async`
- Use `Task<T>` for methods returning values, `Task` for void methods
- Always await async operations, don't use `.Result` or `.Wait()`

**Entity Design:**
- Implement `IEntity` interface for new entity types
- Use `[BsonId]` and `[BsonRepresentation(BsonType.ObjectId)]` for MongoDB IDs
- Use `[BsonIgnore]` for computed or non-persisted properties
- Use `[Required]` for mandatory fields
- Use custom attributes like `[Upload]` for metadata

**Logging:**
- Use structured logging with named parameters
- Include relevant context in log messages
- Use appropriate log levels (Information, Error, Debug, Trace)
- Pass logger via dependency injection

**Error Handling:**
- Return appropriate HTTP status codes using TypedResults
- Log errors with sufficient context for debugging
- Validate input parameters and return BadRequest for invalid data
- Use pattern matching for result handling where appropriate

### Frontend

- Use TypeScript for type safety
- Follow Remix.js conventions for routing and data handling
- Use Tailwind CSS for styling
- Create reusable components when possible

## Testing

Currently, the project does not have automated tests. When suggesting test implementations:

- For backend: Consider xUnit with in-memory MongoDB
- For frontend: Consider Vitest and React Testing Library

## Additional Notes

- Docker configurations are available for both API and frontend
- The project uses OpenLibrary API for book metadata
- Stats are automatically calculated and cached

## Branching and Git Workflow

- Never commit directly to the `main` branch.
- For any change, create a new branch from `main` and work there. Use a descriptive naming convention, for example:
   - `feature/<short-description>` for new features
   - `fix/<short-description>` for bug fixes
   - `chore/<short-description>` for maintenance and non-functional changes
- Open a Pull Request (PR) from your branch into `main` when ready. Ensure the PR description summarizes the change and any notable impacts.
- Keep branches focused and small; prefer incremental PRs over large ones.
- Update the branch with the latest `main` before merging (rebase or merge as appropriate).