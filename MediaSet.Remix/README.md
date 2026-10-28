# MediaSet.Remix

Frontend UI for MediaSet, built with React Router 7 and TypeScript.

The existing application is migrated in place; `MediaSet.Remix` remains its directory
to preserve development, Docker, and deployment paths. The `feat/react-router-7`
branch provides isolation until cutover. There is no parallel frontend.
The migration follows the [official Remix v2 upgrade guide](https://reactrouter.com/7.18.4/upgrading/remix).
`app/routes.ts` retains the existing file conventions through `@react-router/fs-routes`;
loaders, actions, components, and tests remain in their original locations.
Router packages are aligned at 7.18.4 and React remains on 18.

The only additional major dependency change is the test-only `happy-dom` 21.0.4
security fix for [GHSA-834v-8h66-3h79](https://github.com/advisories/GHSA-834v-8h66-3h79).
Other package modernization remains separate work.

The existing upload preview still reads build-time `VITE_API_URL`; setting only
runtime `clientApiUrl` does not configure that preview in production. Image
configuration parity remains part of #677. Existing runtime variable names and
deployment paths are retained throughout this framework migration.

## Overview

The MediaSet frontend provides a modern, responsive interface for managing your personal media library. Built with React Router 7, it offers server-side rendering, optimistic UI updates, and seamless integration with the MediaSet API.

**Technologies:**

- **React Router 7**: Full-stack React framework with server-side rendering
- **TypeScript**: Type-safe development
- **Tailwind CSS**: Utility-first CSS framework
- **Vite**: Fast build tool and dev server

## Getting Started

### Prerequisites and setup

Install Node.js 24.22.1 and its bundled npm. Complete [first setup](../Development/DEVELOPMENT.md#first-setup)
for the host API and containerized MongoDB.

### Development

From MediaSet.Remix:

```bash
npm ci
npm run dev
```

The frontend listens at http://localhost:3000 and supports LAN browsing.
The API must be running for database-backed routes. Vite forwards browser
API/image requests through its development-only /dev-api proxy.
Stop with Ctrl+C. VS Code provides **Launch UI** and **Launch API + UI**.

For optional HTTPS and camera barcode scanning, use `npm run dev:mobile` after
the [certificate setup](../Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning).
No certificates are needed for ordinary development.

### Running Tests

```bash
# From MediaSet.Remix directory
cd MediaSet.Remix

# Run tests once
npm test

# Run tests in watch mode
npm test -- --watch

# Run with coverage
npm test -- --coverage
```

For comprehensive testing documentation, see [Development/TESTING.md](../Development/TESTING.md).

## Project Structure

```
MediaSet.Remix/
├── app/
│   ├── components/       # Reusable UI components
│   ├── hooks/            # Shared React hooks
│   ├── routes/           # React Router route modules
│   │   ├── _index.tsx           # Home dashboard
│   │   ├── _index.test.tsx      # Home route tests
│   │   ├── config.json.ts       # Runtime UI configuration
│   │   ├── $entity/             # Entity list routes
│   │   ├── $entity._index/      # Entity list route module
│   │   ├── $entity.add/         # Add entity route module
│   │   ├── $entity.$entityId/   # Entity detail route module
│   │   ├── $entity.$entityId_.edit/   # Edit entity route module
│   │   └── $entity.$entityId_.delete/ # Delete entity route module
│   ├── test/             # Testing utilities and fixtures
│   ├── utils/            # Shared utilities
│   ├── config.server.ts  # Server-side configuration
│   ├── constants.server.ts # Server-side constants
│   ├── entity-data.ts    # Entity API data access
│   ├── helpers.ts        # Shared helper functions
│   ├── integrations-data.ts # Integrations data access
│   ├── lookup-data.server.ts # Lookup helpers (server)
│   ├── metadata-data.ts  # Metadata API data access
│   ├── models.ts         # TypeScript interfaces
│   ├── root.tsx          # Root layout component
│   ├── stats-data.ts     # Stats API data access
│   └── tailwind.css      # Tailwind base styles
├── public/               # Static assets
└── build/                # Build output (generated)
```

## Routing

React Router uses the existing file-based routing conventions for automatic route generation:

**Key routes:**

- `/` - Home dashboard with statistics
- `/{entityType}` - Entity list view (`books`, `movies`, `games`, `musics`)
- `/{entityType}/:entityId` - Entity detail view
- `/{entityType}/:entityId/edit` - Edit entity
- `/{entityType}/add` - Add new entity

### Collection contracts

`app/routes.ts` uses `flatRoutes` from `@react-router/fs-routes`, excluding test
files. The existing `$entity` layout shares the list/detail route modules across
all four collections; `$entityId` is the existing API identifier. Entity enums
remain `Books`, `Movies`, `Games`, and `Musics`, including the plural `/musics` URL
for the Music navigation label.

List loaders accept `searchText`, `orderBy` (default `title:asc`), and `page`
(default `1`). `pagedSearchEntities` calls `/{Entity}/pagedsearch` with these
values and `pageSize=75`, returning `{ items, page, totalPages, totalCount }`.
The UI loader exposes `{ entities, pagination, entityType, searchText, orderBy,
apiUrl }`. Pagination preserves search and ordering; changing ordering resets
the page, and submitting the search form resets ordering/page to their defaults.
Repeated search submissions revalidate the loader. Direct URLs and browser
history restore this query state.

Detail loaders return `{ entity, apiUrl }` and select the existing Book, Movie,
Game, or Music component by `entity.type`. Browser cover URLs remain
`${apiUrl}/static/images/${coverImage.filePath}`. Server requests use private
`apiUrl`; browser/image configuration uses `clientApiUrl` (`/dev-api` through Vite
in development). These variable names and the API DTOs remain unchanged.

Editing uses `/{entityType}/add` and `/{entityType}/:entityId/edit`; both submit
multipart forms through their existing actions and redirect to the detail URL.
The delete action remains `POST /{entityType}/:entityId/delete`. The shared
pending overlay follows Router navigation loading/submission state. Empty lists
retain their existing message; invalid collections and missing records return
404 with recovery UI. Malformed IDs and failed upstream API calls retain the
existing 500 error boundary and retry/home controls. This migration preserves
these behaviors; it does not redefine API error status policy.

**Route features:**

- Server-side data loading with loaders
- Form handling with actions
- Optimistic UI updates
- Progressive enhancement

## Key Features

### Data Loading

Server-side data fetching with React Router loaders:

```typescript
export async function loader({ params }: LoaderFunctionArgs) {
  const books = await getBooks();
  return { books };
}
```

### Form Handling

Form submissions with React Router actions:

```typescript
export async function action({ request }: ActionFunctionArgs) {
  const formData = await request.formData();
  const book = await createBook(formData);
  return redirect(`/books/${book.id}`);
}
```

### Styling with Tailwind CSS

Utility-first CSS approach:

```tsx
<div className="container mx-auto px-4 py-8">
  <h1 className="text-3xl font-bold text-gray-900">Books</h1>
</div>
```

### TypeScript Type Safety

All data models are typed for safety and IntelliSense:

```typescript
export interface Book {
  id: string;
  title: string;
  authors: string[];
  isbn?: string;
  coverImage?: CoverImage;
}
```

## Development Resources

- **[Development/DEVELOPMENT.md](../Development/DEVELOPMENT.md)** - Complete development setup and debugging
- **[Development/TESTING.md](../Development/TESTING.md)** - Testing guidelines and patterns
- [React Router Documentation](https://reactrouter.com/7.18.4)
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [Vite Documentation](https://vitejs.dev/guide/)
