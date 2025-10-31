# Backend API Tests Code Style Guide (.NET 9.0)

This document outlines the code style conventions for the MediaSet.Api.Tests project. **All test code changes must strictly adhere to these guidelines.**

## Testing Framework

This project uses **NUnit** as the testing framework along with:
- **Moq** for mocking dependencies
- **Bogus** for generating realistic test data
- **Microsoft.AspNetCore.Mvc.Testing** for integration tests

## File Organization

- Use file-scoped namespaces (e.g., `namespace MediaSet.Api.Tests.Services;`)
- Mirror the structure of `MediaSet.Api` (e.g., `Services/BookServiceTests.cs` tests `Services/BookService.cs`)
- Group test classes by the component they test
- One test class per class under test

## Naming Conventions

### Test Classes
- Name test classes after the class being tested with `Tests` suffix
- Example: `BookService` → `BookServiceTests`
- Use PascalCase for test class names

### Test Methods
- Follow the pattern: `MethodName_Scenario_ExpectedResult`
- Use underscores to separate the three parts
- Use descriptive scenario descriptions
- Examples:
  - `GetBookById_ExistingId_ReturnsBook`
  - `CreateBook_WithValidData_ReturnsCreatedBook`
  - `UpdateBook_WithInvalidId_ReturnsNotFound`
  - `SearchBooks_WithEmptyQuery_ReturnsAllBooks`
  - `DeleteBook_NonExistentId_ThrowsNotFoundException`

### Test Variables
- Use `sut` (System Under Test) for the instance being tested
- Use descriptive names for test data: `expectedBook`, `actualResult`, `validIsbn`
- Prefix mock objects with `mock`: `mockRepository`, `mockLogger`, `mockHttpClient`
- Use `faker` for Bogus faker instances

## Test Structure

### AAA Pattern (Arrange, Act, Assert)
All tests must follow the AAA pattern with clear sections:

```csharp
[Test]
public async Task GetBookById_ExistingId_ReturnsBook()
{
    // Arrange
    var expectedBook = new Book { Id = "123", Title = "Test Book" };
    var mockRepository = new Mock<IBookRepository>();
    mockRepository.Setup(x => x.GetByIdAsync("123")).ReturnsAsync(expectedBook);
    var sut = new BookService(mockRepository.Object);

    // Act
    var result = await sut.GetBookByIdAsync("123");

    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Id, Is.EqualTo(expectedBook.Id));
    Assert.That(result.Title, Is.EqualTo(expectedBook.Title));
}
```

### Test Class Structure
```csharp
[TestFixture]
public class BookServiceTests
{
    // Private fields for commonly used mocks
    private Mock<IBookRepository> _mockRepository;
    private Mock<ILogger<BookService>> _mockLogger;
    
    // Setup method runs before each test
    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IBookRepository>();
        _mockLogger = new Mock<ILogger<BookService>>();
    }
    
    // Teardown method runs after each test (if cleanup needed)
    [TearDown]
    public void TearDown()
    {
        // Clean up resources if necessary
    }
    
    // Helper methods for creating test data
    private Book CreateValidBook() => new()
    {
        Id = "123",
        Title = "Test Book",
        Authors = ["Author Name"]
    };
    
    // Test methods grouped by functionality
    [Test]
    public async Task GetBookById_ExistingId_ReturnsBook()
    {
        // Test implementation
    }
    
    [Test]
    public async Task GetBookById_NonExistentId_ReturnsNull()
    {
        // Test implementation
    }
}
```

## NUnit Attributes

### Test Attributes
- Use `[Test]` for individual test methods
- Use `[TestCase]` for parameterized tests with inline data
- Use `[TestCaseSource]` for parameterized tests with complex data
- Use `[Ignore("Reason")]` for temporarily disabled tests (with clear reason)
- Use `[TestFixture]` to mark a class as containing tests

```csharp
[TestCase("978-0-123456-47-2", true)]
[TestCase("invalid-isbn", false)]
[TestCase("", false)]
[TestCase(null, false)]
public void IsValidIsbn_VariousInputs_ReturnsExpectedResult(string isbn, bool expected)
{
    // Arrange
    var validator = new IsbnValidator();

    // Act
    var result = validator.IsValid(isbn);

    // Assert
    Assert.That(result, Is.EqualTo(expected));
}
```

### Test Fixtures and Setup
- Use `[SetUp]` for code that runs before each test method
- Use `[TearDown]` for code that runs after each test method
- Use `[OneTimeSetUp]` for code that runs once before all tests in the fixture
- Use `[OneTimeTearDown]` for code that runs once after all tests in the fixture

```csharp
[TestFixture]
public class BookApiTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [SetUp]
    public void SetUp()
    {
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
    }
}
```

## Mocking with Moq

### Setup Conventions
- Use `Setup()` for configuring mock behavior
- Use `Returns()` or `ReturnsAsync()` for return values
- Use `Throws()` or `ThrowsAsync()` for exception scenarios
- Use `It.IsAny<T>()` for flexible parameter matching
- Use specific values when testing exact parameter matching

```csharp
// Return specific value
mockRepository.Setup(x => x.GetByIdAsync("123"))
    .ReturnsAsync(expectedBook);

// Accept any parameter
mockClient.Setup(x => x.SearchAsync(It.IsAny<string>()))
    .ReturnsAsync(searchResults);

// Throw exception
mockRepository.Setup(x => x.DeleteAsync("invalid"))
    .ThrowsAsync(new NotFoundException());
```

### Verification
- Use `Verify()` to assert method calls
- Use `Times` to specify call count expectations
- Only verify when the call itself is important to test

```csharp
// Verify method was called once
mockRepository.Verify(x => x.SaveAsync(It.IsAny<Book>()), Times.Once);

// Verify method was never called
mockLogger.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Never);
```

## Test Data Generation with Bogus

### Faker Setup
- Create reusable faker instances for consistent test data
- Use Bogus rules for realistic data generation
- Customize fakers for specific test scenarios

```csharp
private Faker<Book> CreateBookFaker() => new Faker<Book>()
    .RuleFor(b => b.Id, f => f.Random.String2(24, "0123456789abcdef"))
    .RuleFor(b => b.Title, f => f.Lorem.Sentence(3, 5))
    .RuleFor(b => b.Authors, f => new[] { f.Name.FullName() })
    .RuleFor(b => b.ISBN, f => f.Commerce.Ean13())
    .RuleFor(b => b.Publisher, f => f.Company.CompanyName())
    .RuleFor(b => b.PublicationDate, f => f.Date.Past(50));

// Generate single instance
var book = CreateBookFaker().Generate();

// Generate multiple instances
var books = CreateBookFaker().Generate(10);
```

## Assertions

### NUnit Assertions
- Use the constraint-based assertion model (`Assert.That`)
- Use descriptive assertion messages when helpful
- Test one logical concept per test method
- Use specific constraints over generic ones

```csharp
// Good - specific constraint-based assertions
Assert.That(result, Is.Not.Null);
Assert.That(actual, Is.EqualTo(expected));
Assert.That(condition, Is.True);
Assert.That(collection, Does.Contain(item));
Assert.That(collection, Is.Empty);
Assert.That(value, Is.GreaterThan(0));
Assert.That(text, Does.StartWith("prefix"));
Assert.That(exception, Is.InstanceOf<ArgumentException>());

// Good - with message for clarity
Assert.That(actualCount, Is.EqualTo(expectedCount), "Book count should match after deletion");

// Avoid - too generic or old assertion style
Assert.IsTrue(result != null); // Use Assert.That(result, Is.Not.Null) instead
Assert.IsTrue(collection.Count == 0); // Use Assert.That(collection, Is.Empty) instead
```

### Async Assertions
- Always await async test methods
- Use `async Task` for async tests
- Don't use `.Result` or `.Wait()`

```csharp
[Test]
public async Task GetBooksAsync_ReturnsAllBooks()
{
    // Arrange
    var expectedBooks = CreateBookFaker().Generate(5);
    _mockRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(expectedBooks);
    var sut = new BookService(_mockRepository.Object);

    // Act
    var result = await sut.GetBooksAsync();

    // Assert
    Assert.That(result.Count, Is.EqualTo(expectedBooks.Count));
}
```

## Testing Different Scenarios

### Happy Path Tests
Test the expected successful behavior:
```csharp
[Test]
public async Task CreateBook_WithValidData_ReturnsCreatedBook()
{
    // Test successful creation
}
```

### Error Path Tests
Test error handling and edge cases:
```csharp
[Test]
public async Task CreateBook_WithNullTitle_ThrowsArgumentException()
{
    // Arrange
    var invalidBook = new Book { Title = null };
    var sut = new BookService(_mockRepository.Object);

    // Act & Assert
    Assert.ThrowsAsync<ArgumentException>(async () => await sut.CreateBookAsync(invalidBook));
}

[Test]
public async Task GetBookById_NonExistentId_ReturnsNull()
{
    // Test not found scenario
}
```

### Boundary Tests
Test edge cases and limits:
```csharp
[TestCase("")]
[TestCase(null)]
[TestCase("   ")]
public async Task SearchBooks_EmptyOrWhitespaceQuery_ReturnsAllBooks(string query)
{
    // Test boundary conditions
}
```

## Integration Tests

### WebApplicationFactory Usage
```csharp
[TestFixture]
public class BooksApiTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [SetUp]
    public void SetUp()
    {
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task GetBooks_ReturnsSuccessAndBooks()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/books");

        // Assert
        response.EnsureSuccessStatusCode();
        var books = await response.Content.ReadFromJsonAsync<List<Book>>();
        Assert.That(books, Is.Not.Null);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
    }
}
```

## Code Style

### General Guidelines
- Use `var` for local variables when type is obvious
- Initialize collections with `[]` syntax
- Use collection expressions where appropriate
- Place attributes on separate lines above test methods
- Use target-typed `new()` expressions

### Readability
- Keep test methods focused and short
- Extract complex setup into helper methods
- Use meaningful variable names
- Add comments only when testing behavior is not obvious from the code

### Organization
- Group related tests together
- Order tests logically (happy path, then error cases)
- Keep test classes manageable in size (split if needed)

## Testing External API Clients

### Mock HTTP Responses
```csharp
[TestFixture]
public class OpenLibraryClientTests
{
    private Mock<HttpMessageHandler> _mockHandler;
    private HttpClient _httpClient;
    private OpenLibraryClient _sut;

    [SetUp]
    public void SetUp()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://openlibrary.org/")
        };
        _sut = new OpenLibraryClient(_httpClient);
    }

    [Test]
    public async Task SearchByIsbn_ValidIsbn_ReturnsBookMetadata()
    {
        // Arrange
        var expectedJson = """{"title":"Test Book","authors":[{"name":"Author"}]}""";
        _mockHandler.SetupRequest(HttpMethod.Get, "isbn/1234567890.json")
            .ReturnsResponse(HttpStatusCode.OK, expectedJson);

        // Act
        var result = await _sut.SearchByIsbnAsync("1234567890");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("Test Book"));
    }
}
```

## Performance Tests

### Measure Execution Time
```csharp
[Test]
public async Task GetBooks_WithCache_CompletesQuickly()
{
    // Arrange
    var sut = new CachedBookService(_mockRepository.Object, _mockCache.Object);
    var stopwatch = Stopwatch.StartNew();

    // Act
    await sut.GetBooksAsync();
    stopwatch.Stop();

    // Assert
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100), "Cached operation should be fast");
}
```

## Test Data Management

### Constants
```csharp
public class BookServiceTests
{
    private const string ValidIsbn = "978-0-123456-47-2";
    private const string InvalidIsbn = "invalid";
    private static readonly string[] SampleAuthors = ["Author One", "Author Two"];
}
```

### Factories
```csharp
private static class TestDataFactory
{
    public static Book CreateBook(string? id = null, string? title = null)
    {
        return new Book
        {
            Id = id ?? "123",
            Title = title ?? "Default Title",
            Authors = ["Default Author"]
        };
    }
}
```

## Best Practices

1. **Independence**: Tests should not depend on each other or execution order
2. **Repeatability**: Tests should produce the same result every time
3. **Fast Execution**: Keep tests fast; use mocks instead of real dependencies
4. **Clear Failures**: Test failures should clearly indicate what went wrong
5. **Maintainability**: Keep tests simple and easy to understand
6. **Coverage**: Test both happy paths and error scenarios
7. **Single Responsibility**: Each test should verify one logical concept
8. **No Logic**: Avoid complex logic in tests (no loops, conditions, etc.)

## What NOT to Test

- Third-party library internals
- Framework behavior (e.g., ASP.NET Core routing)
- Simple property getters/setters without logic
- Auto-generated code
- Private methods (test through public API)

## Code Review Checklist

- [ ] Test names follow `MethodName_Scenario_ExpectedResult` convention
- [ ] AAA pattern is clearly visible
- [ ] Mocks are properly configured and verified when necessary
- [ ] Test data is realistic and relevant
- [ ] Both happy path and error cases are covered
- [ ] Tests are independent and can run in any order
- [ ] No magic numbers or strings (use constants)
- [ ] Async methods are properly awaited
- [ ] Test class mirrors structure of code under test
