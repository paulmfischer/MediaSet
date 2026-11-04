# Frontend conventions (Remix 3 / TypeScript)

Node runs the TypeScript sources directly; `tsc` only type checks. Import with the explicit
`.ts` / `.tsx` extension.

## Structure

- `app/routes.ts` is the route map. Each page area has its own nested map
  (`routes.entityLists`); link with `routes.*.href()`.
- `app/actions/<area>/controller.tsx` holds the area's actions, helpers and server tests.
  `app/ui/<area>/` holds its page components. The two trees mirror each other, with
  sub-pages in matching sub-folders (`detail`, `add`, `edit`). Register a controller in
  `app/router.ts` with `router.map(routes.<area>, <area>Controller)`.
- Layout shared by every page stays at the `app/ui/` root. `app/api/` holds data functions
  that call the API, `app/models.ts` shared types, `app/utils/` shared helpers.

## Naming and types

- Components: PascalCase function in a `kebab-case.tsx` file. Files are `kebab-case`.
  Functions and variables are camelCase, constants SCREAMING_SNAKE_CASE, interfaces
  PascalCase.
- Annotate parameters and return types. No `any`; use `unknown` and type guards.

## Actions

- Declare a route once in `routes.ts`; handle it with
  `createController(routes.<area>, { actions: { ... } })`.
- An action reads and validates URL and form input (a helper such as `parseListParams` next
  to it is fine), fetches data, then renders with `context.render(<Document>...</Document>)`
  or returns `redirect()`. Components never fetch and receive plain, parsed values.
- Return a user-friendly page with the right status for not-found and failed requests.

## Forms

- Declare an editable page with `form(pattern)`: the controller's `index` renders the form
  and `action` handles the submit. Use a native `<form method="post">` with named inputs
  that works without JavaScript.
- `formData()` middleware (in `router.ts`) provides `context.formData`. Validate it with a
  `remix/data-schema` schema (`s.parseSafe`) using the helpers in
  `app/actions/forms/form-schema.ts`, and save `parsed.value`, never the raw form data.
- On a validation failure, re-render from the same action with status `400`, passing the
  submitted `values` and `issues` so input is kept and each error is linked to its field
  with `aria-describedby`. Do not redirect back to the GET page.
- On success return `redirect(..., 303)`.
- Use `Field` / `errorProps` (`app/ui/forms/field.tsx`) for labels and errors, and the
  select and image islands in `app/ui/forms/public/`.
- Hydrate only what needs it, such as the submit button's pending state, never the whole
  form.
- File inputs use `enctype="multipart/form-data"` and `boundedFormData()` for size limits;
  validate the file in the action.
- Extra form actions (metadata lookup, applying a result, closing a dialog) are submit
  buttons with `name="intent"`; the action re-renders with status `200` and never saves.
  Set a new `formKey` on the render that should replace what the user typed.
- Browsers only send GET and POST. For PUT, PATCH or DELETE use a hidden `_method` field
  with `methodOverride()` installed right after `formData()`.

## Components

- Components come from `remix/component`:
  `function Name(handle: Handle<Props>) { return () => ... }`, with props read from
  `handle.props`. Use `class`, not `className`.
- Server components render once to HTML with no state or effects; keep most UI this way.
- Interactivity lives only in client entries under a `public/` folder, declared with
  `clientEntry(import.meta.url, function Name(handle) { ... })`. State is a closure
  variable; call `handle.update()` to re-render, `handle.queueTask()` for browser-only
  work and `handle.signal` for cleanup.
- Keep components small and single-purpose; put behavior in plain functions.

## Browser boundary

- Code reaches the browser only through the asset server in `app/assets.ts`, which serves
  `app/**/public/**` plus the files in `allowFiles`. Put every client entry and everything
  it imports in a `public/` folder.
- Never import server code (API data functions, config, environment) from a `public/`
  module; pass what it needs as props.
- Browser requests use `clientApiUrl`; server-side calls use `apiUrl`.

## Styling

- Tailwind only, mobile-first, with utility classes and shared tokens. Common styles live
  in `app/tailwind.css`; buttons use the global button styles, not per-button color or
  size classes.
- Use semantic HTML, labels and keyboard-reachable controls.

## Tests

- Router (server) tests use `remix/test` and `remix/assert`: `describe` / `it` with
  `router.fetch(new URL(path, 'http://localhost'))`, asserting on status, headers and HTML.
  They are `*.test.ts(x)` next to the code or controller they cover.
- Browser tests are `*.test.browser.tsx`; end-to-end tests are `*.test.e2e.ts`
  (`t.serve(...)` with `createTestServer(router.fetch)`).
- Module tests (`app/api/`, `app/utils/`, `app/models.ts`) are `*.test.ts` files that also use
  `remix/test` and `remix/assert`. Data functions in `app/api/` take their `apiFetch` and
  `serverLogger` as a trailing `ApiDeps` argument (`app/api/api-deps.ts`); tests pass the fakes
  from `app/test/api-fakes.ts` instead of mocking modules.
- Write new tests with `remix test`. Name them
  `should [expected behavior] when [condition]` and test user-visible behavior.
