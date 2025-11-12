# MediaSet UI Testing Guide

This guide covers testing practices, patterns, and conventions for the MediaSet.Remix frontend application.

## Overview

The MediaSet.Remix frontend uses **Vitest** for unit and component testing with **React Testing Library** for component testing. Tests are located alongside source files with `.test.ts` or `.test.tsx` extensions.

### Testing Stack

- **Vitest**: Fast, modern unit test runner with Vite integration
- **React Testing Library**: Component testing focusing on user interactions
- **Happy DOM**: Lightweight DOM implementation for testing
- **@testing-library/user-event**: User-centric testing utilities
- **@testing-library/jest-dom**: Extended matchers for DOM assertions

## Quick Start

### Running Tests

```bash
# Run all tests once
npm test

# Run tests in watch mode (rerun on file changes)
npm test -- --watch

# Run tests with coverage report
npm test -- --coverage

# Run specific test file
npm test -- app/helpers.test.ts

# Run tests matching a pattern
npm test -- --grep "toTitleCase"
```

### Test Coverage

Coverage reports can be generated locally in the `coverage/` directory:

```bash
# Generate coverage report
npm test -- --coverage

# View HTML coverage report
open coverage/index.html
```

Coverage configuration in `vitest.config.ts`:

- **Provider**: v8
- **Reporters**: text, json, html
- **Excluded**: node_modules, test files, type definitions, build output

**Note**: Coverage reports are generated locally for viewing but are not currently tracked in CI/CD. You can view coverage metrics by running tests with the `--coverage` flag.

## Test Structure

### Basic Test Template

```typescript
import { describe, it, expect } from 'vitest';
import { functionToTest } from './module';

describe('module.ts', () => {
  describe('functionToTest', () => {
    it('should do something specific', () => {
      // Arrange: Set up test data
      const input = 'test value';

      // Act: Call the function
      const result = functionToTest(input);

      // Assert: Verify the result
      expect(result).toBe('expected value');
    });
  });
});
```

### Organization

- Group related tests with `describe` blocks
- Use descriptive test names that explain the behavior
- Follow the Arrange-Act-Assert (AAA) pattern
- One assertion focus per test when possible

## Testing Patterns

### 1. Unit Tests (Pure Functions)

Test utility functions and helper functions in isolation.

**File**: `app/helpers.test.ts`

```typescript
describe('toTitleCase', () => {
  it('should convert lowercase string to title case', () => {
    const result = toTitleCase('hello world');
    expect(result).toBe('Hello World');
  });

  it('should handle empty string', () => {
    const result = toTitleCase('');
    expect(result).toBe('');
  });
});
```

**Best Practices**:
- Test edge cases (empty, null, undefined)
- Test normal cases
- Test error conditions
- Use descriptive test names

### 2. Component Tests

Test React components focusing on user interactions and rendered output.

**Example Pattern**:

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen } from '~/test/test-utils';
import { MyComponent } from './MyComponent';

describe('MyComponent', () => {
  it('should render button with correct text', () => {
    // Arrange & Act
    render(<MyComponent />);

    // Assert
    expect(screen.getByRole('button', { name: /click me/i })).toBeInTheDocument();
  });

  it('should call onClick handler when button is clicked', async () => {
    const user = userEvent.setup();
    const handleClick = vi.fn();

    render(<MyComponent onClick={handleClick} />);
    
    await user.click(screen.getByRole('button'));
    
    expect(handleClick).toHaveBeenCalledOnce();
  });
});
```

**Best Practices**:
- Use semantic queries: `getByRole()`, `getByLabelText()`, `getByPlaceholderText()`
- Avoid implementation details: Don't query by `className` or `testId` unless necessary
- Use `user-event` for realistic user interactions
- Test user behavior, not component internals

### 3. Data Transformation Tests

Test functions that transform or validate data.

**File**: `app/helpers.test.ts` - `formToDto` tests

```typescript
describe('formToDto', () => {
  it('should convert FormData to BookEntity', () => {
    const formData = new FormData();
    formData.append('title', 'Test Book');
    formData.append('author', 'Test Author');

    const result = formToDto(Entity.Books, formData);

    expect(result.title).toBe('Test Book');
    expect(result.author).toBe('Test Author');
  });
});
```

**Best Practices**:
- Test with various input combinations
- Verify all required fields are present
- Test type conversions (string to number, etc.)
- Test default values

### 4. Mock Testing

Use mocks for API calls and external dependencies.

**Mock Utilities** in `app/test/mocks.ts`:

```typescript
import { mockApiResponse, createMockFetch } from '~/test/mocks';

it('should fetch and display data', async () => {
  const mockData = { books: [{ id: 1, title: 'Test' }] };
  global.fetch = createMockFetch({
    '/books': mockApiResponse(mockData),
  });

  // Test code here
});
```

**Best Practices**:
- Use provided mock utilities in `app/test/mocks.ts`
- Mock at the integration boundary (API calls)
- Don't mock internal functions
- Use fixtures for test data

## Testing Conventions

### Naming Conventions

- **Test files**: `[name].test.ts` or `[name].test.tsx`
- **Describe blocks**: Match the file name (e.g., `'helpers.ts'`)
- **Test names**: Use "should" pattern describing the behavior
  - ✅ `should convert lowercase string to title case`
  - ❌ `test string conversion`
  - ❌ `toTitleCase works`

### File Organization

```
app/
├── helpers.ts
├── helpers.test.ts           # Tests adjacent to source
├── components/
│   ├── Button.tsx
│   └── Button.test.tsx       # Component tests adjacent to component
├── routes/
│   ├── _index.tsx
│   └── _index.test.tsx
└── test/                     # Shared testing utilities
    ├── setup.ts              # Test environment setup
    ├── test-utils.tsx        # Custom render and utilities
    ├── mocks.ts              # Mock utilities
    └── fixtures.ts           # Test data fixtures
```

### Test Data

Use fixtures from `app/test/fixtures.ts` for consistent test data:

```typescript
import { mockBook, mockBooks, createMockStats } from '~/test/fixtures';

describe('BookList', () => {
  it('should display books', () => {
    render(<BookList books={mockBooks} />);
    expect(screen.getByText(mockBook.title)).toBeInTheDocument();
  });
});
```

## Entity Testing

### Entity-Specific Patterns

The MediaSet entities (Books, Movies, Games, Music) follow common patterns for testing:

#### 1. Entity Enum Conversion

```typescript
import { Entity, singular, getEntityFromParams } from '~/helpers';

describe('Entity Enum Conversion', () => {
  it('should convert Books enum to "Book" singular', () => {
    expect(singular(Entity.Books)).toBe('Book');
  });

  it('should extract entity from route params', () => {
    const entity = getEntityFromParams({ entity: 'books' });
    expect(entity).toBe(Entity.Books);
  });
});
```

#### 2. Form Data to DTO Conversion

Each entity (Book, Movie, Game, Music) has form-to-DTO conversion:

```typescript
describe('formToDto - Game Entity', () => {
  it('should convert FormData to GameEntity with all fields', () => {
    const formData = new FormData();
    formData.append('title', 'Test Game');
    formData.append('developer', 'Test Developer');
    formData.append('platform', 'PS5');
    formData.append('rating', '4.5');

    const result = formToDto(Entity.Games, formData);

    expect(result).toEqual({
      title: 'Test Game',
      developer: 'Test Developer',
      platform: 'PS5',
      rating: 4.5,
    });
  });
});
```

#### 3. Entity Property Mapping

Test that properties map correctly between API responses and UI models:

```typescript
describe('Entity Data Mapping', () => {
  it('should map API Book response to BookEntity', () => {
    const apiResponse = {
      id: '123',
      title: 'API Book',
      author: 'Test Author',
      isbn: '978-0000000000',
    };

    const entity = mapApiResponseToEntity(apiResponse);

    expect(entity.id).toBe('123');
    expect(entity.title).toBe('API Book');
  });
});
```

## Component Testing Best Practices

### Querying Elements

**Prefer** (in order of preference):
1. `getByRole()` - Most accessible and resilient
2. `getByLabelText()` - For form inputs
3. `getByPlaceholderText()` - For placeholder text
4. `getByText()` - For visible content
5. `getByTestId()` - Last resort for unique identifiers

**Avoid**:
- Querying by `className`
- Testing implementation details
- Accessing component state directly

### User Interactions

Use `@testing-library/user-event` for realistic interactions:

```typescript
import { userEvent } from '@testing-library/user-event';

it('should handle form submission', async () => {
  const user = userEvent.setup();
  const handleSubmit = vi.fn();

  render(<Form onSubmit={handleSubmit} />);

  await user.type(screen.getByLabelText('Title'), 'New Book');
  await user.click(screen.getByRole('button', { name: /submit/i }));

  expect(handleSubmit).toHaveBeenCalled();
});
```

### Async Testing

Always use `async/await` for asynchronous operations:

```typescript
it('should load and display data', async () => {
  render(<DataComponent />);

  // Wait for element to appear
  const element = await screen.findByText('Loaded');
  expect(element).toBeInTheDocument();
});
```

## Testing Best Practices

### ✅ Do's

- Write tests as you write code
- Test user-facing behavior, not implementation
- Use descriptive test names
- Keep tests focused and isolated
- Use fixtures for consistent test data
- Test edge cases and error conditions
- Use mocks at integration boundaries
- Verify accessibility in component tests

### ❌ Don'ts

- Don't test implementation details
- Don't create interdependent tests
- Don't use vague test names
- Don't skip error case testing
- Don't mock internal functions
- Don't test third-party libraries
- Don't hardcode test data
- Don't ignore accessibility concerns

## Coverage Goals

Target the following coverage metrics:

- **Statements**: 80%+
- **Branches**: 75%+
- **Functions**: 80%+
- **Lines**: 80%+

Focus coverage effort on:
- Business logic functions
- User-facing components
- Data transformation logic
- Form handling

Lower priority for coverage:
- Configuration files
- Type definition files
- Generated code
- Entry point files

## Debugging Tests

### Debugging in VS Code

1. Add breakpoint in test file
2. Run debug configuration or use: `npm test -- --inspect-brk`
3. Open DevTools (chrome://inspect)
4. Step through code

### Logging in Tests

```typescript
it('should debug test', () => {
  const result = complexFunction();
  console.log('Result:', result); // Will show in terminal
  expect(result).toBe(expected);
});
```

### Using screen.debug()

```typescript
import { screen } from '~/test/test-utils';

it('should show component structure', () => {
  render(<MyComponent />);
  screen.debug(); // Prints DOM tree to console
});
```

## Common Testing Scenarios

### Testing API Integration

```typescript
import { createMockFetch, mockApiResponse } from '~/test/mocks';

it('should fetch and display books', async () => {
  global.fetch = createMockFetch({
    '/books': mockApiResponse({ data: [{ id: 1, title: 'Test' }] }),
  });

  render(<BookList />);

  const book = await screen.findByText('Test');
  expect(book).toBeInTheDocument();
});
```

### Testing Error States

```typescript
it('should display error when API fails', async () => {
  global.fetch = createMockFetch({
    '/books': mockApiError('Failed to load', 500),
  });

  render(<BookList />);

  const error = await screen.findByText(/failed to load/i);
  expect(error).toBeInTheDocument();
});
```

### Testing Form Validation

```typescript
it('should show validation error for empty title', async () => {
  const user = userEvent.setup();
  render(<BookForm onSubmit={vi.fn()} />);

  await user.click(screen.getByRole('button', { name: /submit/i }));

  expect(screen.getByText(/title is required/i)).toBeInTheDocument();
});
```

### Testing Conditional Rendering

```typescript
it('should show content when data is loaded', () => {
  render(<DataComponent isLoaded={false} />);
  expect(screen.queryByText('Content')).not.toBeInTheDocument();

  render(<DataComponent isLoaded={true} />);
  expect(screen.getByText('Content')).toBeInTheDocument();
});
```

## Running Tests Locally

### Prerequisites

```bash
# Install dependencies
npm install

# From MediaSet.Remix directory
cd MediaSet.Remix
```

### Test Commands

```bash
# Run all tests once
npm test

# Run tests in watch mode (development)
npm test -- --watch

# Run specific test file
npm test -- app/helpers.test.ts

# Run tests matching pattern
npm test -- --grep "toTitleCase"

# Run with coverage report
npm test -- --coverage

# Update snapshots (if using snapshot testing)
npm test -- --update
```

### Continuous Integration

Tests run automatically on:
- Pull requests
- Commits to main branch
- Tagged releases

Configuration: `.github/workflows/` (CI pipeline)

## Test Maintenance

### Updating Tests

When modifying functionality:

1. Run affected tests: `npm test -- [filename]`
2. Update tests to match new behavior
3. Verify coverage doesn't decrease
4. Run full test suite before committing

### Debugging Test Failures

1. Run test in watch mode: `npm test -- --watch`
2. Check error message and stack trace
3. Use `screen.debug()` to inspect DOM
4. Verify mock data matches expectations
5. Check if test is isolating properly (previous test pollution?)

### Removing Tests

- Only remove tests when removing related functionality
- Update or merge tests if consolidating features
- Don't remove tests to pass coverage checks

## Resources

- **Vitest Documentation**: https://vitest.dev/
- **React Testing Library**: https://testing-library.com/react
- **Testing Best Practices**: https://kentcdodds.com/blog/common-mistakes-with-react-testing-library
- **Accessible Queries**: https://testing-library.com/docs/queries/about

## Contributing Tests

When contributing to MediaSet:

1. **Write tests with your code**
   - Follow patterns in existing tests
   - Maintain consistent naming conventions

2. **Run tests locally before submitting PR**
   ```bash
   npm test
   npm test -- --coverage
   ```

3. **Ensure coverage doesn't decrease**
   - Check coverage report for new code
   - Aim for 80%+ coverage on new functionality

4. **Follow testing conventions**
   - Use provided test utilities and fixtures
   - Group related tests with describe blocks
   - Use semantic queries for components

5. **Document complex test scenarios**
   - Add comments for non-obvious test setup
   - Explain mocking strategies if unusual
   - Reference related tests when appropriate

## Questions?

For questions about testing:
- Check existing test files for patterns
- Review the [Vitest Documentation](https://vitest.dev/)
- Check [React Testing Library docs](https://testing-library.com/react)
- Open an issue with test-related questions
