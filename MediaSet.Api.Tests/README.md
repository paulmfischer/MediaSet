# MediaSet.Api.Tests

Unit and integration tests for the MediaSet API.

## Overview

This project contains comprehensive tests for the MediaSet API, including unit tests for services, helpers, and converters, as well as integration tests for API endpoints.

**Technologies:**
- **NUnit**: Test framework
- **Moq**: Mocking library for dependencies
- **Bogus**: Realistic fake data generation
- **Microsoft.AspNetCore.Mvc.Testing**: Integration testing support

## Running Tests

```bash
# Run all tests from project root
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Run with detailed output
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~ClassName"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Writing Tests

### Test Structure

Follow the **AAA pattern** (Arrange, Act, Assert) and use descriptive names:

```csharp
[Test]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data and dependencies
    var sut = new SystemUnderTest();
    
    // Act - Execute the method being tested
    var result = sut.Method();
    
    // Assert - Verify the expected outcome
    Assert.That(result, Is.EqualTo(expected));
}
```

**Examples:**
- `GetBooks_WhenNoBooksExist_ReturnsEmptyList`
- `CreateBook_WithValidData_ReturnsCreatedBook`
- `UpdateBook_WithInvalidId_ReturnsNotFound`

### Mocking with Moq

Use Moq to mock external dependencies and database collections:

```csharp
[TestFixture]
public class BookServiceTests
{
    private Mock<IMongoCollection<Book>> _mockCollection;
    private Mock<IOpenLibraryClient> _mockClient;
    private BookService _sut;

    [SetUp]
    public void SetUp()
    {
        _mockCollection = new Mock<IMongoCollection<Book>>();
        _mockClient = new Mock<IOpenLibraryClient>();
        _sut = new BookService(_mockCollection.Object, _mockClient.Object);
    }

    [Test]
    public async Task GetBookById_ExistingId_ReturnsBook()
    {
        // Arrange
        var expectedBook = new Book { Id = "123", Title = "Test Book" };
        _mockCollection.Setup(x => x.FindAsync(It.IsAny<FilterDefinition<Book>>(), null, default))
            .ReturnsAsync(expectedBook);
        
        // Act
        var result = await _sut.GetBookByIdAsync("123");
        
        // Assert
        Assert.That(result, Is.EqualTo(expectedBook));
    }
}
```

**Mocking External API Clients:**

```csharp
var mockClient = new Mock<IOpenLibraryClient>();
mockClient.Setup(x => x.SearchByIsbnAsync(It.IsAny<string>()))
    .ReturnsAsync(new BookMetadata { Title = "Test Book", Authors = ["Author Name"] });
```

### Test Data Generation with Bogus

Use Bogus to generate realistic, randomized test data:

```csharp
var faker = new Faker<Book>()
    .RuleFor(b => b.Id, f => ObjectId.GenerateNewId().ToString())
    .RuleFor(b => b.Title, f => f.Lorem.Sentence())
    .RuleFor(b => b.Authors, f => new[] { f.Name.FullName() })
    .RuleFor(b => b.ISBN, f => f.Random.String2(13, "0123456789"))
    .RuleFor(b => b.PublicationDate, f => f.Date.Past(10));

var books = faker.Generate(10); // Generate 10 test books
```

**Why Bogus?**
- Creates realistic test data with variety
- Reduces manual test data setup
- Makes tests more maintainable
- Helps catch edge cases with randomized data

### Integration Tests

Test API endpoints end-to-end with `WebApplicationFactory`:

```csharp
[TestFixture]
public class BookApiTests : IntegrationTestBase
{
    [Test]
    public async Task GetBooks_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/books");
        
        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}
```

## Best Practices

- **One test class per production class** - Test classes mirror the structure of `MediaSet.Api`
- **Use descriptive test names** - Clearly describe the scenario and expected outcome
- **Follow AAA pattern** - Arrange, Act, Assert for clarity
- **Mock external dependencies** - Don't call real APIs or databases in unit tests
- **Test both success and failure paths** - Happy path and error cases
- **Use `sut` for System Under Test** - Standard convention for the class being tested
- **Prefix mocks with `mock`** - e.g., `mockClient`, `mockRepository`

## Project Structure

```
MediaSet.Api.Tests/
├── Clients/          # External API client tests
├── Converters/       # JSON converter tests
├── Entities/         # Entity endpoint tests
├── Helpers/          # Utility function tests
├── Lookup/           # Metadata lookup tests
├── Models/           # Model and validation tests
├── Services/         # Core service tests
└── Stats/            # Statistics service tests
```

## Additional Resources

- [Development/TESTING.md](../Development/TESTING.md) - Testing guidelines and strategies
- [NUnit Documentation](https://docs.nunit.org/)
- [Moq Quickstart](https://github.com/moq/moq4)
- [Bogus Documentation](https://github.com/bchavez/Bogus)
