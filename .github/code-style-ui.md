# Frontend UI Code Style Guide (Remix.js/TypeScript)

This document outlines the code style conventions for the MediaSet.Remix project. **All code changes must strictly adhere to these guidelines.**

## File Organization

- Follow Remix.js conventions for routing and file structure
- Place route components in `app/routes/`
- Place reusable components in `app/components/`
- Place data fetching functions in `app/*-data.ts` files
- Place type definitions in `app/models.ts` or co-located with components

## TypeScript

- Always use TypeScript for type safety
- Define interfaces for all data structures
- Use type annotations for function parameters and return types
- Avoid using `any` type; prefer `unknown` if type is truly unknown
- Use union types and type guards where appropriate

## Naming Conventions

- Use PascalCase for component names and interfaces
- Use camelCase for functions, variables, and file names (except components)
- Use descriptive names that clearly indicate purpose
- Prefix interfaces with `I` only when necessary for clarity
- Use `.tsx` extension for files containing JSX, `.ts` for pure TypeScript

## Component Design

- Prefer function components over class components
- Use React hooks for state and side effects
- Keep components focused and single-purpose
- Extract complex logic into custom hooks
- Use proper prop typing with TypeScript interfaces

## Remix.js Conventions

- Use loader functions for data fetching (GET requests)
- Use action functions for mutations (POST, PUT, DELETE)
- Follow Remix's nested routing patterns
- Use `Form` component for forms that trigger actions
- Leverage `useLoaderData` and `useActionData` hooks appropriately

## Styling

- Use Tailwind CSS for all styling
- Prefer utility classes over custom CSS
- Use consistent spacing and sizing tokens
- Extract repeated patterns into reusable components
- Follow mobile-first responsive design approach

## Code Quality

- Use meaningful variable and function names
- Keep functions small and focused
- Add comments for complex logic, not obvious code
- Remove unused imports, variables, and code
- Remove outdated comments
- Use const for values that don't change, let for values that do

## Error Handling

- Handle errors gracefully with user-friendly messages
- Use Remix's error boundaries for route-level errors
- Validate form inputs on both client and server
- Provide loading states for async operations

## Testing

- Use Vitest for unit tests
- Use React Testing Library for component tests
- Test user interactions and edge cases
- Mock external dependencies appropriately
- Name tests descriptively: `should [expected behavior] when [condition]`

## Accessibility

- Use semantic HTML elements
- Provide proper ARIA labels where needed
- Ensure keyboard navigation works correctly
- Maintain sufficient color contrast
- Test with screen readers when possible
