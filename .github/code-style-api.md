# Backend API Code Style Guide (.NET 8.0)

This document outlines the code style conventions for the MediaSet.Api project. **All code changes must strictly adhere to these guidelines.**

## File Organization

- Use file-scoped namespaces (e.g., `namespace MediaSet.Api.Models;`)
- Group related functionality into folders (Models, Services, Helpers, etc.)
- Use descriptive folder names that reflect their purpose

## Naming Conventions

- Use PascalCase for public properties, methods, and classes
- Use camelCase for private fields and parameters
- Use descriptive names (e.g., `entityCollection`, `searchText`, `orderByField`)
- Prefix private fields with underscore when injected via constructor (e.g., `_httpClient`, `_logger`)
- Use plural names for collections (e.g., `Authors`, `Genres`, `Studios`)

## Code Style

- Use `var` for local variables when type is obvious from assignment
- Initialize collections with `[]` syntax instead of `new List<T>()`
- Use structured/semantic logging: `logger.LogInformation("Message: {value}", value)`
- Use `string.Empty` instead of `""` for empty string initialization
- Place attributes on separate lines above properties/methods
- Use target-typed `new()` expressions where applicable

## Code Quality

- Remove unused usings and code
- Remove outdated comments

## Dependency Injection

- Use constructor injection for services
- Store injected dependencies in private readonly fields
- Follow the pattern: `private readonly ServiceType _serviceName;`

## API Design

- Use minimal APIs with route groups for organization
- Return typed results: `Results<Ok<T>, NotFound>`, `TypedResults.Ok()`, etc.
- Use descriptive route parameter names that match method parameters
- Group related endpoints using `MapGroup()`
- Add appropriate tags for Swagger documentation

## Async/Await

- Use async/await for all asynchronous operations
- Suffix async methods with `Async`
- Use `Task<T>` for methods returning values, `Task` for void methods
- Always await async operations, don't use `.Result` or `.Wait()`
- Utilize `CancellationToken` parameters in async methods where appropriate

## Entity Design

- Implement `IEntity` interface for new entity types
- Use `[BsonId]` and `[BsonRepresentation(BsonType.ObjectId)]` for MongoDB IDs
- Use `[BsonIgnore]` for computed or non-persisted properties
- Use `[Required]` for mandatory fields
- Use custom attributes like `[Upload]` for metadata

## Logging

- Use structured logging with named parameters
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
- Name tests descriptively: `MethodName_Scenario_ExpectedResult`
