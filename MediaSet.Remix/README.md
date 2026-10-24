# MediaSet.Remix

Frontend UI for MediaSet, built with Remix.js and TypeScript.

## Overview

The MediaSet frontend provides a modern, responsive interface for managing your personal media library. Built with Remix.js, it offers server-side rendering, optimistic UI updates, and seamless integration with the MediaSet API.

**Technologies:**

- **Remix.js**: Full-stack React framework with server-side rendering
- **TypeScript**: Type-safe development
- **Tailwind CSS**: Utility-first CSS framework
- **Vite**: Fast build tool and dev server

## Getting Started

### Prerequisites and setup

Install Node.js 22/npm. Complete [first setup](../Development/DEVELOPMENT.md#first-setup)
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
│   ├── routes/           # Remix route modules
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

Remix uses file-based routing for automatic route generation:

**Key routes:**

- `/` - Home dashboard with statistics
- `/{entityType}` - Entity list view (books, movies, games, music)
- `/{entityType}/$id` - Entity detail view
- `/{entityType}/$id/edit` - Edit entity
- `/{entityType}/add` - Add new entity

**Route features:**

- Server-side data loading with loaders
- Form handling with actions
- Optimistic UI updates
- Progressive enhancement

## Key Features

### Data Loading

Server-side data fetching with Remix loaders:

```typescript
export async function loader({ params }: LoaderFunctionArgs) {
  const books = await getBooks();
  return json({ books });
}
```

### Form Handling

Form submissions with Remix actions:

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
- [Remix Documentation](https://remix.run/docs)
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [Vite Documentation](https://vitejs.dev/guide/)
