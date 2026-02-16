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

## Naming Conventions

- **Components**: PascalCase class in `kebab-case.tsx` file | `MovieCard` class in `movie-card.tsx`
- **Functions/variables**: camelCase | `fetchBooks`, `formatTitle`
- **Interfaces**: PascalCase (optionally `IPascalCase` prefix) | `IBook`, `MovieCardProps`
- **Constants**: SCREAMING_SNAKE_CASE | `MAX_ITEMS`, `DEFAULT_SORT`
- **Files**: `kebab-case.ts` or `kebab-case.tsx` | `movie-card.tsx`, `data-functions.ts`

## TypeScript

- Always use TypeScript for type safety
- Define interfaces for all data structures
- Use type annotations for function parameters and return types
- Avoid `any` type; prefer `unknown` if type is truly unknown
- Use union types and type guards where appropriate

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

## Accessibility

- Use semantic HTML elements
- Provide proper ARIA labels where needed
- Ensure keyboard navigation works correctly
- Maintain sufficient color contrast
- Test with screen readers when possible

## Error Handling

- Handle errors gracefully with user-friendly messages
- Use error boundaries for component-level error handling
- Validate form inputs on both client and server
- Provide loading states for async operations
- Use Remix error boundaries for route-level errors

## Logging

- Log errors with sufficient context for debugging
- Use console methods appropriately (console.error for errors, console.info for important info)
- Avoid verbose logging in production

## Testing

- Use Vitest for unit tests
- Use React Testing Library for component tests
- Test naming: `should [expected behavior] when [condition]`
- Test user interactions over implementation details
- Mock external dependencies appropriately

## Code Quality Checks

After making changes to the frontend, always verify compliance before committing:

- **ESLint**: `npm run lint` — checks for code quality issues and enforces code style rules
- **ESLint (auto-fix)**: `npm run lint:fix` — automatically fixes ESLint violations where possible
- **Prettier**: `npm run lint:format` — checks that all files conform to the project's formatting rules
- **Prettier (auto-fix)**: `npm run lint:format:fix` — automatically reformats files to conform to Prettier rules

Both check commands must pass without errors. Run them from the `MediaSet.Remix/` directory.