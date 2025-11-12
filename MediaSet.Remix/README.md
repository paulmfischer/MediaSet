# MediaSet.Remix

Frontend UI for MediaSet, built with Remix.js and TypeScript.

## Overview

This is the frontend application for MediaSet's media library management system. Built with Remix.js, it provides a modern, responsive interface for managing your books, movies, and games collection.

## Technologies

- **Remix.js**: Full-stack React framework with server-side rendering
- **TypeScript**: Type-safe development
- **Tailwind CSS**: Utility-first CSS framework
- **Vite**: Fast build tool and dev server

## Development

### Prerequisites

- Node.js 20+
- npm or yarn

### Running Locally

```bash
# From the MediaSet.Remix directory

# Install dependencies
npm install

# Start the dev server (with hot-reload)
npm run dev

# Frontend will be available at http://localhost:3000
```

### Using Docker (Recommended)

**From the project root:**

```bash
# Start frontend only
./dev.sh start frontend

# Start everything (API + Frontend + MongoDB)
./dev.sh start all
```

**Manual Docker commands (from MediaSet.Remix directory):**

```bash
# Build the Docker image
docker build -t mediaset-remix .

# Run the container
docker run -it --rm -p 3000:3000 --name mediaset-remix \
  -e "API_URL=http://localhost:5000" \
  mediaset-remix
```

## Building

### Development Build

```bash
npm run build
```

### Production Deployment

```bash
# Build for production
npm run build

# Start production server
npm start
```

The build outputs are:
- `build/server` - Server-side code
- `build/client` - Client-side assets

## Available Scripts

- `npm run dev` - Start development server with hot-reload
- `npm run build` - Build for production
- `npm start` - Run production server
- `npm run typecheck` - Run TypeScript type checking
- `npm run lint` - Run ESLint
- `npm test` - Run tests with Vitest

## Project Structure

```
MediaSet.Remix/
├── app/
│   ├── routes/           # Remix route components
│   │   ├── _index.tsx    # Home page
│   │   ├── books.tsx     # Books routes
│   │   ├── movies.tsx    # Movies routes
│   │   └── games.tsx     # Games routes
│   ├── components/       # Reusable UI components
│   ├── models.ts         # TypeScript interfaces
│   ├── entity-data.ts    # Data fetching functions
│   ├── root.tsx          # Root component
│   └── entry.*.tsx       # Entry points
├── public/               # Static assets
└── build/                # Build output (generated)
```

## Routing

Remix uses file-based routing. Key routes:

- `/` - Home dashboard
- `/books` - Books collection
- `/books/$id` - Book detail
- `/books/$id/edit` - Edit book
- `/movies` - Movies collection
- `/games` - Games collection

## Data Loading

Use Remix loaders for server-side data fetching:

```typescript
export async function loader({ params }: LoaderFunctionArgs) {
  const books = await getBooks();
  return json({ books });
}
```

## Forms and Mutations

Use Remix actions for form submissions:

```typescript
export async function action({ request }: ActionFunctionArgs) {
  const formData = await request.formData();
  const book = await createBook(formData);
  return redirect(`/books/${book.id}`);
}
```

## Styling

This project uses **Tailwind CSS** for styling:

- Utility-first approach
- Responsive design built-in
- Custom configuration in `tailwind.config.ts`
- PostCSS setup in `postcss.config.js`

Example:
```tsx
<div className="container mx-auto px-4 py-8">
  <h1 className="text-3xl font-bold text-gray-900">Books</h1>
</div>
```

## API Integration

The frontend communicates with the MediaSet.Api backend:

```typescript
// In app/entity-data.ts
const API_BASE_URL = process.env.API_URL || 'http://localhost:5000';

export async function getBooks() {
  const response = await fetch(`${API_BASE_URL}/books`);
  return response.json();
}
```

## Type Safety

TypeScript interfaces are defined in `app/models.ts`:

```typescript
export interface Book {
  id: string;
  title: string;
  authors: string[];
  // ... other properties
}
```

## Testing

The MediaSet.Remix frontend includes comprehensive test coverage using Vitest and React Testing Library. For detailed testing guidance, patterns, and best practices, see the [Testing Guide](../Development/TESTING.md).

### Running Tests

```bash
# Run tests once
npm test

# Run tests in watch mode (recommended for development)
npm test -- --watch

# Run tests with coverage report
npm test -- --coverage

# Run specific test file
npm test -- app/helpers.test.ts

# Run tests matching a pattern
npm test -- --grep "toTitleCase"
```

### Test Coverage

- Generate coverage reports locally: `npm test -- --coverage`
- View detailed HTML report: `open coverage/index.html`
- Target coverage: 80%+ for statements, branches, functions, and lines

### Testing Stack

- **Vitest**: Fast unit test runner with Vite integration
- **React Testing Library**: Component testing focusing on user interactions
- **Happy DOM**: Lightweight DOM implementation for testing
- **@testing-library/user-event**: User-centric testing utilities

### Test Organization

Tests are located alongside source code:
- `app/helpers.test.ts` - Utility function tests
- `app/entity-data.test.ts` - Data fetching tests
- `app/test/` - Shared testing utilities, fixtures, and mocks

For comprehensive testing documentation, see [Development/TESTING.md](../Development/TESTING.md).

## Code Style

Follow the frontend code style guidelines in [../.github/code-style-ui.md](../.github/code-style-ui.md).

Key conventions:
- PascalCase for components
- camelCase for functions and variables
- TypeScript for all code
- Functional components with hooks
- Tailwind CSS for styling

## Environment Variables

Create a `.env` file in the MediaSet.Remix directory:

```env
API_URL=http://localhost:5000
```

## Deployment

For deployment options, see the [Remix deployment documentation](https://remix.run/docs/en/main/guides/deployment).

Popular options:
- Vercel
- Netlify
- Fly.io
- Docker container (production-ready Dockerfile included)

## Resources

- [Remix Documentation](https://remix.run/docs)
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [Vite Documentation](https://vitejs.dev/guide/)
