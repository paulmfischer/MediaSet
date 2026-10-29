# MediaSet.Remix

Frontend UI for MediaSet, built with Remix 3 and TypeScript.

## Overview

The MediaSet frontend provides a responsive interface for managing your personal media library. It is a Remix 3 server that renders pages to HTML and talks to the MediaSet API.

**Technologies:**

- **Remix 3** (the `remix` package): router, server-rendered components, asset server and test runner. It is not React.
- **TypeScript**: Type-safe development; Node runs the sources directly and `tsc` only type checks
- **Tailwind CSS**: Utility-first CSS, compiled to `public/app.css`
- **Playwright**: Browser and end-to-end tests through `remix test`

## Getting Started

### Prerequisites and setup

Install Node.js 24.22.1 LTS and its bundled npm (see [`.nvmrc`](../.nvmrc)). Complete
[first setup](../Development/DEVELOPMENT.md#first-setup) for the host API and containerized MongoDB.

### Development

From MediaSet.Remix:

```bash
npm ci
npm run dev
```

The frontend listens at http://localhost:3000 and supports LAN browsing.
The API must be running for database-backed routes. The dev server forwards browser
API/image requests through its development-only `/dev-api` proxy.
Stop with Ctrl+C. VS Code provides **Launch UI** and **Launch API + UI**.

For optional HTTPS and camera barcode scanning, use `npm run dev:mobile` after
the [certificate setup](../Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning).
No certificates are needed for ordinary development.

Optional overrides go in `.env.local`; see [`.env.example`](.env.example).

### Running Tests

```bash
cd MediaSet.Remix

# Unit (retained Vitest suites) and router tests
npm test

# Browser and end-to-end tests (needs `npx playwright install chromium` once)
npm run test:e2e
```

See [Local development](../Development/DEVELOPMENT.md#checks) for the full list of checks.

## Project Structure

```
MediaSet.Remix/
├── app/
│   ├── actions/          # Controllers per page area, with helpers and router tests
│   │   ├── controller.tsx       # Root routes (home, config)
│   │   ├── entity/              # Collection list controller
│   │   └── public/entry.ts      # Browser entry that starts the Remix runtime
│   ├── ui/               # Page components, mirroring actions/
│   │   ├── document.tsx         # Shared HTML document
│   │   ├── entity/              # Collection list page
│   │   └── public/              # Client entries (menus, navigation)
│   ├── api/              # Server-side MediaSet API access
│   ├── e2e/              # End-to-end tests
│   ├── utils/            # Shared utilities
│   ├── assets.ts         # Asset server: what the browser may load
│   ├── models.ts         # TypeScript interfaces
│   ├── router.ts         # Router and controller registration
│   ├── routes.ts         # Route map
│   └── tailwind.css      # Tailwind base styles
├── public/               # Static assets; app.css is generated
├── dev.ts                # Development launcher (Tailwind watch + server)
├── remix.json            # remix test configuration
└── server.ts             # Node HTTP entry point
```

## Routing

Routes are declared in `app/routes.ts` and handled by controllers in `app/actions/`.

**Key routes:**

- `/` - Home dashboard
- `/books`, `/movies`, `/games`, `/musics` - Collection lists

Further pages are being added on this branch.

```ts
// app/routes.ts
export const routes = route({
  home: get('/'),
  entityLists: route({
    books: get('/books'),
  }),
});
```

```tsx
// app/actions/entity/controller.tsx
export default createController(routes.entityLists, {
  actions: {
    books: (context) => renderList(context, booksList),
  },
});
```

An action fetches data, then renders a component with `context.render()`.
Mutations are plain HTML forms handled by POST actions that redirect.

## Components

Components come from `remix/component` and receive a `Handle`:

```tsx
export function ListPage(handle: Handle<ListPageProps>) {
  return () => <h1 class="text-2xl">{handle.props.config.entity}</h1>;
}
```

Interactive pieces are client entries (`clientEntry(...)`) in `public/` folders; only
`app/**/public/**` and the shared files listed in `app/assets.ts` are served to the browser.

## Development Resources

- **[Development/DEVELOPMENT.md](../Development/DEVELOPMENT.md)** - Complete development setup and debugging
- **[Frontend instructions](../.github/instructions/frontend.instructions.md)** - Remix 3 conventions
- [Remix 3 guides](node_modules/remix/guides/) - Shipped with the installed `remix` package
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
