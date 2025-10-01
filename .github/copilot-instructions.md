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

### Backend

- Use dependency injection for services
- Follow REST API conventions
- Use async/await for asynchronous operations
- Implement IEntity interface for new entity types

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