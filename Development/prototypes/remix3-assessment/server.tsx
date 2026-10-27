import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { createAssetServer } from 'remix/assets';
import { type RemixNode } from 'remix/component';
import { ImportMap } from 'remix/component/server';
import { render } from 'remix/middleware/render';
import { formData } from 'remix/middleware/form-data';
import {
  MaxFileSizeExceededError,
  MaxTotalSizeExceededError,
  MaxPartsExceededError,
  MaxFilesExceededError,
} from 'remix/form-data-parser';
import { createRouter } from 'remix/router';
import { createRequestListener } from 'remix/node-fetch-server';
import { Counter } from './app/public/counter.tsx';

interface Book {
  id?: string;
  title: string;
  authors?: string[];
  isbn?: string;
}

// Refuse accidental production writes. The API must also be configured with a disposable DB.
const apiUrl: string = process.env.ASSESSMENT_API_URL ?? 'http://127.0.0.1:51654';
if (new URL(apiUrl).hostname !== '127.0.0.1') throw new Error('Assessment API must use loopback');
const assets = createAssetServer({
  rootDir: import.meta.dirname,
  basePath: '/assets',
  allowFiles: ['app/public/**'],
  allowPackages: ['remix', '@zxing/library'],
  watch: false,
});
const router = createRouter({
  middleware: [
    formData({
      maxFiles: 1,
      maxFileSize: 5 * 1024 * 1024,
      maxParts: 8,
      maxTotalSize: 6 * 1024 * 1024,
    }),
    render({ assets }),
  ],
});

async function api(path: string, init?: RequestInit): Promise<Response> {
  return fetch(`${apiUrl}${path}`, {
    ...init,
    signal: AbortSignal.timeout(10000),
  });
}

async function document(
  title: string,
  children: RemixNode,
  scriptEntry: string = 'app/public/entry.ts'
): Promise<RemixNode> {
  const entry = await assets.getScriptEntry(scriptEntry);
  return (
    <html lang="en" class="dark">
      <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>{title} | MediaSet assessment</title>
        <link rel="stylesheet" href="/style.css" />
        <ImportMap value={entry.importMap} />
        <script type="module" src={entry.href} />
      </head>
      <body class="bg-zinc-900 text-slate-200 p-4 md:p-8">
        <main class="max-w-4xl mx-auto space-y-4">
          <header class="flex justify-between gap-4">
            <a href="/books">MediaSet / Books</a>
            <a href="/books/add">Add book</a>
          </header>
          <p>Isolated Remix 3.1.0 assessment — disposable collection</p>
          <h1 class="text-2xl font-bold">{title}</h1>
          {children}
          <Counter />
        </main>
      </body>
    </html>
  );
}

function form(book: Book = { title: '' }): RemixNode {
  return (
    <form method="post" enctype="multipart/form-data" class="space-y-4">
      <label class="block">
        Title <input name="title" required value={book.title} />
      </label>
      <label class="block">
        Authors <input name="authors" value={book.authors?.join(', ') ?? ''} />
      </label>
      <label class="block">
        ISBN <input name="isbn" value={book.isbn ?? ''} />
      </label>
      <label class="block">
        Cover image <input name="coverImage" type="file" accept="image/png,image/jpeg" />
      </label>
      <button type="submit">Save</button>
    </form>
  );
}

router.get(
  '/assets/*path',
  async ({ request }) => (await assets.fetch(request)) ?? new Response(null, { status: 404 })
);
router.get(
  '/style.css',
  async () =>
    new Response(await readFile(new URL('./public/style.css', import.meta.url)), {
      headers: { 'Content-Type': 'text/css' },
    })
);
router.get('/', () => Response.redirect('http://127.0.0.1:31654/books', 302));
router.get('/books', async (context) => {
  const search: string = new URL(context.request.url).searchParams.get('searchText') ?? '';
  const response: Response = await api(`/Books/search?searchText=${encodeURIComponent(search)}&orderBy=title:asc`);
  if (!response.ok)
    return context.render(await document('API error', <p role="alert">Books API returned {response.status}</p>), {
      status: 502,
    });
  const books: Book[] = await response.json();
  return context.render(
    await document(
      'Books',
      <>
        <form method="get">
          <label>
            Search <input name="searchText" value={search} />
          </label>
          <button>Search</button>
        </form>
        <ul class="space-y-2">
          {books.map((book) => (
            <li class="bg-zinc-800 rounded p-4">
              <a href={`/books/${book.id}`}>{book.title}</a>
              <p>{book.authors?.join(', ')}</p>
            </li>
          ))}
        </ul>
      </>
    )
  );
});
router.get('/books/add', async (context) => context.render(await document('Add book', form())));
router.get('/scan-probe', async (context) => {
  return context.render(
    await document(
      'Bounded scan probe',
      <>
        <p id="scan-result">Loading decoder…</p>
      </>,
      'app/public/scan-entry.ts'
    )
  );
});
router.get('/books/:id', async (context) => {
  const response: Response = await api(`/Books/${context.params.id}`);
  if (!response.ok)
    return context.render(await document('Book unavailable', <p role="alert">Book not found ({response.status})</p>), {
      status: response.status,
    });
  const book: Book = await response.json();
  return context.render(
    await document(
      book.title,
      <>
        <p>{book.authors?.join(', ')}</p>
        <p>{book.isbn}</p>
        <a href={`/books/${book.id}/edit`}>Edit</a>
      </>
    )
  );
});
router.get('/books/:id/edit', async (context) => {
  const response: Response = await api(`/Books/${context.params.id}`);
  if (!response.ok)
    return context.render(await document('Book unavailable', <p role="alert">Book not found</p>), {
      status: response.status,
    });
  return context.render(await document('Edit book', form(await response.json())));
});
for (const path of ['/books/add', '/books/:id/edit']) {
  router.post(path, async (context) => {
    const data: FormData = context.formData;
    const title: string = String(data.get('title') ?? '').trim();
    if (!title)
      return context.render(await document('Validation error', <p role="alert">Title is required</p>), { status: 400 });
    const book: Book = {
      title,
      authors: String(data.get('authors') ?? '')
        .split(',')
        .map((author) => author.trim())
        .filter(Boolean),
      isbn: String(data.get('isbn') ?? ''),
    };
    const id: string | undefined = context.params.id;
    if (id) book.id = id;
    const body = new FormData();
    body.set('entity', JSON.stringify(book));
    const image: FormDataEntryValue | null = data.get('coverImage');
    if (image instanceof File && image.size > 0) body.set('coverImage', image);
    const response: Response = await api(id ? `/Books/${id}` : '/Books/', {
      method: id ? 'PUT' : 'POST',
      body,
    });
    if (!response.ok)
      return context.render(await document('Save failed', <p role="alert">Books API returned {response.status}</p>), {
        status: response.status,
      });
    const savedId: string = id ?? ((await response.json()) as Book).id!;
    return new Response(null, {
      status: 303,
      headers: { Location: `/books/${savedId}` },
    });
  });
}

const server = createServer(
  createRequestListener(async (request: Request) => {
    try {
      return await router.fetch(request);
    } catch (error: unknown) {
      if (
        error instanceof MaxFileSizeExceededError ||
        error instanceof MaxTotalSizeExceededError ||
        error instanceof MaxPartsExceededError ||
        error instanceof MaxFilesExceededError
      ) {
        return new Response('<h1>Upload too large</h1><p>Use one image up to 5 MiB.</p>', {
          status: 413,
          headers: { 'Content-Type': 'text/html; charset=utf-8' },
        });
      }
      throw error;
    }
  })
);
server.listen(31654, '127.0.0.1', () => console.info('Assessment: http://127.0.0.1:31654/books'));
process.on('SIGINT', () => {
  server.close();
  void assets.close();
});
