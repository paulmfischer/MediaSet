---
description: Code style and conventions for Remix.js/TypeScript frontend UI
applyTo: "**/*.ts,**/*.tsx"
---

# Frontend UI Code Style Guide (Remix.js/TypeScript)

## File Organization

- Follow Remix.js routing conventions
- Place route components in `app/routes/`
- Place reusable components in `app/components/`
- Place data functions in `app/*-data.ts` files
- Place type definitions in `app/models.ts` or co-locate with components
- Use `.tsx` for files with JSX, `.ts` for pure TypeScript

## TypeScript

- Always use TypeScript for type safety
- Define interfaces for all data structures
- Use type annotations for function parameters and return types
- Avoid `any` type; prefer `unknown` if type is truly unknown
- Use union types and type guards where appropriate

## Naming Conventions

- **Components**: PascalCase (e.g., `BookList.tsx`, `MovieCard.tsx`)
- **Functions/variables**: camelCase (e.g., `fetchBooks`, `formatTitle`)
- **Interfaces**: PascalCase (e.g., `IBook`, `MovieCardProps`)
- **Constants**: SCREAMING_SNAKE_CASE (e.g., `MAX_ITEMS`, `DEFAULT_SORT`)
- Use descriptive names that clearly indicate purpose

## Component Design

- Prefer function components over class components
- Use React hooks for state and side effects
- Keep components focused and single-purpose
- Extract complex logic into custom hooks
- Use proper prop typing with TypeScript interfaces
- Memoize expensive components with `React.memo()` when needed

## Remix.js Conventions

- Use `loader` functions for data fetching (GET requests)
- Use `action` functions for mutations (POST, PUT, DELETE)
- Follow Remix's nested routing patterns
- Use Remix `Form` component for forms that trigger actions
- Use `useLoaderData()` and `useActionData()` hooks appropriately
- Add `CatchBoundary` and `ErrorBoundary` for error handling

## Styling

- Use Tailwind CSS exclusively for styling
- Prefer utility classes over custom CSS
- Use consistent spacing and sizing tokens
- Extract repeated patterns into reusable components
- Follow mobile-first responsive design approach
- Utilize global styles in `app/tailwind.css` for common styles

## Code Quality

- Use meaningful variable and function names
- Keep functions small and focused
- Add comments only for complex logic, not obvious code
- Remove unused imports, variables, and code
- Remove outdated comments
- Use `const` for values that don't change, `let` for values that do

## Error Handling

- Handle errors gracefully with user-friendly messages
- Use Remix's error boundaries for route-level errors
- Validate form inputs on both client and server
- Provide loading states for async operations

## Accessibility

- Use semantic HTML elements
- Provide proper ARIA labels where needed
- Ensure keyboard navigation works correctly
- Maintain sufficient color contrast
- Test with screen readers when possible

## Testing

- Use Vitest for unit tests
- Use React Testing Library for component tests
- Test user interactions and edge cases
- Mock external dependencies appropriately
- Name tests: `should [expected behavior] when [condition]`
- Always include test updates with code changes
