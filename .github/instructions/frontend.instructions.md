---
description: Code style and conventions for the Remix 3/TypeScript frontend UI
applyTo: "**/*.ts,**/*.tsx"
---

# Frontend UI Code Style Guide (Remix 3/TypeScript)

The frontend in `MediaSet.Remix/` is Remix 3 (the `remix` package). It is not React, and
there is no Vite or bundler: Node runs the TypeScript sources directly
(`node --import remix/node-tsx server.ts`) and `tsc` only type checks. Do not add React,
React DOM, Vite, or React-era libraries. The `remix` package ships its own guides in
`MediaSet.Remix/node_modules/remix/guides/`; read them before inventing a pattern.

## File Organization

- `app/routes.ts` holds the route map. Give each page area its own nested route map
  (for example `routes.entityLists`) and use `routes.*.href()` for links.
- `app/actions/` and `app/ui/` mirror each other. The first folder is the page area's root path
  (`actions/entity` and `ui/entity` are the root for the entity pages). Sub-pages go in
  sub-folders under it in both trees (for example `entity/detail`, `entity/add`, `entity/edit`).
- Each page area's actions live in a `controller.tsx` in its `actions/` folder, together with
  its helpers and server tests. Register it in `app/router.ts` with
  `router.map(routes.<area>, <area>Controller)`; the root controller only owns root-level routes.
- `app/ui/` holds the area's page components. Layout shared by every page (`document`, `header`,
  `footer`, `icon`, `nav`) stays at the `ui/` root.
- `app/api/` holds the data functions that call the MediaSet API; `app/models.ts` holds shared
  types; `app/utils/` holds shared helpers.
- Use `.tsx` for files with JSX, `.ts` for pure TypeScript. Import with the explicit `.ts`/`.tsx`
  extension; the TypeScript config requires it.

## Naming Conventions

- **Components**: PascalCase function in `kebab-case.tsx` file | `ListPage` in `list-page.tsx`
- **Functions/variables**: camelCase | `fetchBooks`, `formatTitle`
- **Interfaces**: PascalCase (optionally `IPascalCase` prefix) | `IBook`, `ListPageProps`
- **Constants**: SCREAMING_SNAKE_CASE | `MAX_ITEMS`, `DEFAULT_SORT`
- **Files**: `kebab-case.ts` or `kebab-case.tsx` | `list-page.tsx`, `list-params.ts`

## TypeScript

- Always use TypeScript for type safety
- Define interfaces for all data structures
- Use type annotations for function parameters and return types
- Avoid `any` type; prefer `unknown` if type is truly unknown
- Use union types and type guards where appropriate

## Routing and Controllers

- A route is declared once in `app/routes.ts` (`get`, `post`, `route`, ...) and handled by a
  controller action created with `createController(routes.<area>, { actions: { ... } })`.
- An action receives the request `context` (`context.url`, `context.request`, ...) and returns a
  `Response`. Render pages with `context.render(<Document ...>...</Document>)` and redirect with
  `redirect()` from `remix/response/redirect`.
- Read and validate URL and form input inside the action or a helper next to it (for example
  `parseListParams`); pass plain, already-parsed values to the page component.
- Fetch data in the action, before rendering. Components do not fetch.
- There are no `loader`/`action` exports, route modules, `useLoaderData`, `useActionData`, or
  `Form`. Use plain HTML `<form method="post">` handled by an action, and `redirect()` after a
  mutation.
- Handle errors in the action and return a user-friendly response with the right status code.

## Component Design

- Components come from `remix/component`, not React. A component is a function that receives a
  `Handle` and returns a render function: `function Name(handle: Handle<Props>) { return () => ... }`.
  Read props from `handle.props` inside the render function.
- Use `class` (not `className`) and the standard HTML attribute names.
- Server components render once to HTML and have no state or effects. Keep most UI this way.
- Interactivity lives only in client entries under a `public/` folder, declared with
  `clientEntry(import.meta.url, function Name(handle) { ... })`. State is a plain variable in the
  component closure; call `handle.update()` to re-render. Use `handle.queueTask()` for
  browser-only work after commit and `handle.signal` for cleanup. Use `mix={ref(...)}` and
  `on(...)` for element behavior instead of effects.
- Keep components focused and single-purpose; extract repeated markup into components and
  behavior into plain functions (for example `dismiss.ts`).
- Type props with an interface and `Handle<Props>`.

## Browser Code Boundary

- Code reaches the browser only through the asset server in `app/assets.ts`, which serves
  `app/**/public/**` plus the few shared files listed in `allowFiles`. Anything else is
  server-only by construction.
- Put every client entry and every module it imports in a `public/` folder, or add the shared
  file to `allowFiles` deliberately (as `ui/icon.tsx` and `ui/icons.ts` are).
- Never import server code (API data functions, config reading, environment access) from a
  `public/` module. Pass the values a client entry needs as props.
- The browser entry is `app/actions/public/entry.ts`, which starts the Remix runtime once.
- Browser API and image requests use `clientApiUrl` (`/dev-api` in development); server-side
  calls use `apiUrl`.

## Styling

- Use Tailwind CSS exclusively for styling
- Prefer utility classes over custom CSS
- Use consistent spacing and sizing tokens
- Extract repeated patterns into reusable components
- Follow mobile-first responsive design approach
- Utilize global styles in `app/tailwind.css` for common styles; buttons use the global styles
  rather than per-button color or size classes
- The stylesheet is compiled to `public/app.css` (`npm run css`); new class names appear after
  the CSS build runs (`npm run dev` watches it)

## Accessibility

- Use semantic HTML elements
- Provide proper ARIA labels where needed
- Ensure keyboard navigation works correctly
- Maintain sufficient color contrast
- Test with screen readers when possible

## Error Handling

- Handle errors gracefully with user-friendly messages
- Validate form inputs on the server
- Provide pending states for navigation and async operations
- Return the correct HTTP status for not-found and failed requests

## Logging

- Log errors with sufficient context for debugging
- Use console methods appropriately (console.error for errors, console.info for important info)
- Avoid verbose logging in production

## Testing

Run tests with the commands in [local development](../../Development/DEVELOPMENT.md#checks).

- **Router (server) tests** use `remix/test` and `remix/assert`: `describe`/`it` plus
  `router.fetch(new URL(path, 'http://localhost'))`, asserting on status, headers, and HTML.
  Files are `*.test.ts` or `*.test.tsx`, next to the code or the controller they cover
  (`remix test --type server`).
- **Browser tests** are `*.test.browser.tsx` and **end-to-end tests** are `*.test.e2e.ts`, run in
  Playwright Chromium through `remix test` (`npm run test:e2e`). Use `t.serve(...)` with
  `createTestServer(router.fetch)` for e2e.
- **Vitest is temporary.** Nine module suites (`app/api/**`, `app/utils/**`, `app/models.test.ts`,
  `app/config.server.test.ts`) still run under Vitest (`npm run test:unit`) and are excluded from
  `remix test` in `remix.json`. They will be ported and Vitest removed (#686). Do not add new
  Vitest suites; write new tests with `remix test`.
- Test naming: `should [expected behavior] when [condition]`
- Test user-visible behavior over implementation details
- Mock external dependencies appropriately

## Code Quality Checks

After making changes to the frontend, always verify compliance before committing:

- **ESLint**: `npm run lint` — checks for code quality issues and enforces code style rules
- **ESLint (auto-fix)**: `npm run lint:fix` — automatically fixes ESLint violations where possible
- **Prettier**: `npm run lint:format` — checks that all files conform to the project's formatting rules
- **Prettier (auto-fix)**: `npm run lint:format:fix` — automatically reformats files to conform to Prettier rules
- **Typecheck**: `npm run typecheck` — runs `tsc`

All check commands must pass without errors. Run them from the `MediaSet.Remix/` directory.
