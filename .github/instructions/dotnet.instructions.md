---
description: Code style and conventions for .NET 9.0 backend API
applyTo: "**/*.cs"
---

# .NET 9.0 Backend Code Style Guide

## File Organization

- Use file-scoped namespaces: `namespace MediaSet.Api.Models;`
- Group related functionality into folders (Models, Services, Helpers, etc.)
- Remove unused usings and outdated comments

## Naming Conventions

- **Public**: PascalCase (properties, methods, classes)
- **Private**: camelCase for fields and parameters
- **Injected dependencies**: Prefix underscore (e.g., `_httpClient`, `_logger`)
- **Collections**: Use plural names (e.g., `Authors`, `Genres`, `Studios`)

## Code Style

- Use `var` for local variables when type is obvious
- Initialize collections with `[]` syntax: `new List<T> { }` → `[]`
- Use `string.Empty` instead of `""`
- Place attributes on separate lines above properties/methods
- Use target-typed `new()` expressions where applicable
- Maximum line length: 120 characters

## Async/Await

- Suffix async methods with `Async`
- Use `Task<T>` for value returns, `Task` for void
- Always await async operations (no `.Result` or `.Wait()`)
- Utilize `CancellationToken` parameters in async methods

## Dependency Injection

- Use constructor injection for services
- Store injected dependencies in private readonly fields
- Pattern: `private readonly ServiceType _serviceName;`

## API Design

- Use minimal APIs with route groups for organization
- Return typed results: `Results<Ok<T>, NotFound>()`
- Use descriptive route parameter names
- Group related endpoints with `MapGroup()`
- Add appropriate tags for Swagger documentation

## Entity Design

- Implement `IEntity` interface for new entity types
- Use `[BsonId]` and `[BsonRepresentation(BsonType.ObjectId)]` for MongoDB IDs
- Use `[BsonIgnore]` for computed or non-persisted properties
- Use `[Required]` for mandatory fields
- Use custom attributes like `[Upload]` for metadata

## Logging

- Use structured logging: `logger.LogInformation("Message: {value}", value)`
- Include relevant context in log messages
- Use appropriate log levels (Information, Error, Debug, Trace)
- Pass logger via dependency injection

## Error Handling

- Return appropriate HTTP status codes using TypedResults
- Log errors with sufficient context for debugging
- Validate input parameters and return BadRequest for invalid data
- Use pattern matching for result handling where appropriate

## Testing

- Use NUnit for unit tests
- Consider in-memory MongoDB for integration tests
- Follow AAA pattern (Arrange, Act, Assert)
- Name tests: `MethodName_Scenario_ExpectedResult`
- Always include test updates with code changes

## Development Commands

```bash
./dev.sh start api # Start backend API

./dev.sh restart api # Restart backend API

./dev.sh stop api # Stop backend API

dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj # Run backend tests
```