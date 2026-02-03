---
description: Code style and conventions for .NET 10 backend API
applyTo: "**/*.cs"
---

# .NET 10 Backend Code Style Guide

## File Organization

- Use file-scoped namespaces: `namespace MediaSet.Api.Models;`
- Group related functionality into folders (Models, Services, Helpers, etc.)
- Remove unused usings and outdated comments

## Naming Conventions

- **Files**: PascalCase | `BookController.cs`, `MovieEntity.cs`
- **Public members**: PascalCase (properties, methods, classes)
- **Private members**: camelCase for fields and parameters
- **Injected dependencies**: `_camelCase` prefix | `_httpClient`, `_logger`
- **Collections**: plural names | `Authors`, `Genres`, `Studios`

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
- Validate input parameters and return BadRequest for invalid data
- Log errors with sufficient context for debugging
- Use pattern matching for result handling where appropriate

## Testing

- Use NUnit for unit tests
- Test naming: `MethodName_Scenario_ExpectedResult`
- Consider in-memory MongoDB for integration tests
- Mock external dependencies appropriately