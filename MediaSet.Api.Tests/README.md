# MediaSet.Api.Tests

Unit and integration tests for MediaSet.Api.

## Overview

This project contains comprehensive tests for the MediaSet API, including unit tests for services, helpers, and converters, as well as integration tests for API endpoints.

## Technologies

- **xUnit**: Test framework
- **Moq**: Mocking library for dependencies
- **Bogus**: Realistic fake data generation
- **FluentAssertions**: Expressive assertions (if applicable)

## Running Tests

### From Project Root

```bash
# Run all tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Run with detailed output
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --verbosity normal

# Run with coverage (if configured)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --collect:"XPlat Code Coverage"
```

### From MediaSet.Api.Tests Directory

```bash
cd MediaSet.Api.Tests

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~ServiceName.MethodName"

# Run tests in a specific class
dotnet test --filter "FullyQualifiedName~ClassName"
```

### Using VS Code

- Press `Ctrl+Shift+P` and run "Test: Run All Tests"
- Or use the Testing sidebar to run individual tests

## Project Structure

Tests mirror the structure of `MediaSet.Api`:

```
MediaSet.Api.Tests/
├── Clients/          # Tests for external API clients
├── Converters/       # Tests for JSON converters
├── Entities/         # Tests for entity endpoints
├── Helpers/          # Tests for utility functions
├── Lookup/           # Tests for metadata lookup
├── Metadata/         # Tests for metadata services
├── Models/           # Tests for models and validation
├── Services/         # Tests for core services
└── Stats/            # Tests for statistics services
```

## Test Naming Convention

Follow the **AAA pattern** (Arrange, Act, Assert) and name tests descriptively:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var sut = new SystemUnderTest();
    
    // Act
    var result = sut.Method();
    
    // Assert
    Assert.Equal(expected, result);
}
```

Examples:
- `GetBooks_WhenNoBooksExist_ReturnsEmptyList`
- `CreateBook_WithValidData_ReturnsCreatedBook`
- `UpdateBook_WithInvalidId_ReturnsNotFound`

## Writing Tests

### Unit Tests

Focus on testing individual components in isolation:

```csharp
public class BookServiceTests
{
    private readonly Mock<IMongoCollection<Book>> _mockCollection;
    private readonly BookService _sut;

    public BookServiceTests()
    {
        _mockCollection = new Mock<IMongoCollection<Book>>();
        _sut = new BookService(_mockCollection.Object);
    }

    [Fact]
    public async Task GetBookById_ExistingId_ReturnsBook()
    {
        // Test implementation
    }
}
```

### Integration Tests

Test API endpoints with realistic scenarios:

```csharp
public class BookApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBooks_ReturnsSuccessStatusCode()
    {
        // Test implementation
    }
}
```

## Mocking External Dependencies

Use Moq to mock external API clients and database collections:

```csharp
var mockClient = new Mock<IOpenLibraryClient>();
mockClient.Setup(x => x.SearchByIsbnAsync(It.IsAny<string>()))
    .ReturnsAsync(new BookMetadata { Title = "Test Book" });
```

## Test Data Generation

Use Bogus for generating realistic test data:

```csharp
var faker = new Faker<Book>()
    .RuleFor(b => b.Title, f => f.Lorem.Sentence())
    .RuleFor(b => b.Authors, f => new[] { f.Name.FullName() })
    .RuleFor(b => b.ISBN, f => f.Random.String2(13, "0123456789"));

var books = faker.Generate(10);
```

## Code Coverage

To generate code coverage reports:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports will be generated in the `TestResults` directory.

## Code Style

Follow the testing code style guidelines in [../.github/code-style-api-tests.md](../.github/code-style-api-tests.md).

Key conventions:
- One test class per class under test
- Test naming: `MethodName_Scenario_ExpectedResult`
- AAA pattern (Arrange, Act, Assert)
- Use `sut` for System Under Test
- Prefix mocks with `mock`
- Mock external dependencies
- Test both happy paths and error cases
