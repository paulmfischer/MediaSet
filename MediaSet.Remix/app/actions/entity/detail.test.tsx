import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

// Stubs the API and records the item URLs the page asked for (not the credits lookup). A function body can return any Response.
function stubApi(t: { after: (fn: () => void) => void }, respond: (url: URL) => Response) {
  const original = globalThis.fetch;
  const urls: URL[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (!url.pathname.endsWith('/config/integrations')) urls.push(url);
    return respond(url);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return urls;
}

const book = {
  id: 'b1',
  type: 'Books',
  title: 'Dune',
  subtitle: 'Book One',
  format: 'Hardcover',
  pages: 412,
  publicationDate: '1965-08-01',
  authors: ['Frank Herbert ', 'Brian Herbert'],
  genres: ['Science Fiction', 'Adventure'],
  publisher: 'Ace',
  isbn: '9780441172719',
  plot: 'Spice & worms',
  coverImage: { filePath: 'books/b1.jpg' },
};

const field = (html: string, label: string) =>
  html.match(new RegExp(`data-field="${label}"[^>]*>([\\s\\S]*?)</div>`))?.[1] ?? null;

describe('item detail pages', () => {
  it('fetches the item and renders every book field, joining multi-value fields', async (t) => {
    const urls = stubApi(t, () => Response.json(book));

    const response = await get('/books/b1');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.equal(urls[0].pathname, '/Books/b1');
    assert.match(html, /<title>Dune \| MediaSet<\/title>/);
    assert.match(html, /<h1 class="text-2xl">Dune<\/h1>/);
    assert.match(html, /<h2 class="text-sm">Book One<\/h2>/);
    assert.equal(field(html, 'Format'), 'Hardcover');
    assert.equal(field(html, 'Pages'), '412');
    assert.equal(field(html, 'Publication Date'), '1965-08-01');
    assert.equal(field(html, 'Authors'), 'Frank Herbert, Brian Herbert');
    assert.equal(field(html, 'Genres'), 'Science Fiction, Adventure');
    assert.equal(field(html, 'Publisher'), 'Ace');
    assert.equal(field(html, 'ISBN'), '9780441172719');
    assert.equal(field(html, 'Plot'), 'Spice &amp; worms');
  });

  it('shows the cover from the client API URL, and a placeholder when there is none', async (t) => {
    stubApi(t, () => Response.json(book));
    const withCover = await (await get('/books/b1')).text();
    assert.match(withCover, /<img src="[^"]*\/static\/images\/books\/b1\.jpg" alt="Dune"/);
    assert.doesNotMatch(withCover, /aria-label="No image"/);

    stubApi(t, () => Response.json({ ...book, coverImage: undefined }));
    const without = await (await get('/books/b1')).text();
    assert.doesNotMatch(without, /<img/);
    assert.match(without, /aria-label="No image"/);
  });

  it('links to the existing edit and delete URLs', async (t) => {
    stubApi(t, () => Response.json(book));

    const html = await (await get('/books/b1')).text();

    assert.match(html, /<a href="\/books\/b1\/edit" aria-label="Edit"/);
    assert.match(html, /<a href="\/books\/b1\/delete" aria-label="Delete"/);
  });

  it('does not treat /books/add as an item', async (t) => {
    const urls = stubApi(t, () => Response.json([]));

    const response = await get('/books/add');

    assert.equal(response.status, 200);
    assert.match(await response.text(), /<title>Add Book \| MediaSet<\/title>/);
    assert.ok(!urls.some((url) => url.pathname === '/Books/add'));
  });

  it('encodes the id so it cannot change the API path or query', async (t) => {
    const urls = stubApi(t, () => new Response('gone', { status: 404 }));

    const response = await get('/books/a%3Fb%3Dc%23d');

    assert.equal(response.status, 404);
    assert.equal(urls[0].search, '');
    assert.equal(urls[0].pathname, '/Books/a%3Fb%3Dc%23d');
  });

  it('encodes the id in the edit and delete links', async (t) => {
    stubApi(t, () => Response.json({ ...book, id: 'a b' }));

    const html = await (await get('/books/a%20b')).text();

    assert.match(html, /href="\/books\/a%20b\/edit"/);
    assert.match(html, /href="\/books\/a%20b\/delete"/);
  });

  it('renders a movie, with a check mark for a TV series', async (t) => {
    const movie = {
      id: 'm1',
      type: 'Movies',
      title: 'Show',
      format: 'DVD',
      runtime: 100,
      releaseDate: '2001-01-01',
      isTvSeries: true,
      genres: ['Drama', 'Comedy'],
      studios: ['A ', 'B'],
      barcode: '123',
      plot: 'A plot',
    };
    const urls = stubApi(t, () => Response.json(movie));

    const html = await (await get('/movies/m1')).text();

    assert.equal(urls[0].pathname, '/Movies/m1');
    assert.equal(field(html, 'Format'), 'DVD');
    assert.equal(field(html, 'Runtime'), '100');
    assert.equal(field(html, 'Release Date'), '2001-01-01');
    assert.match(field(html, 'Is TV Show') ?? '', /^<svg/);
    assert.equal(field(html, 'Genres'), 'Drama, Comedy');
    assert.equal(field(html, 'Studios'), 'A, B');
    assert.equal(field(html, 'Barcode'), '123');
    assert.equal(field(html, 'Plot'), 'A plot');
    assert.match(html, /href="\/movies\/m1\/edit"/);
    assert.match(html, /href="\/movies\/m1\/delete"/);

    stubApi(t, () => Response.json({ ...movie, isTvSeries: false }));
    assert.equal(field(await (await get('/movies/m1')).text(), 'Is TV Show'), '');
  });

  it('renders a game', async (t) => {
    const urls = stubApi(t, () =>
      Response.json({
        id: 'g1',
        type: 'Games',
        title: 'Zelda',
        platform: 'Switch',
        format: 'Cart',
        releaseDate: '2017-03-03',
        rating: 'E10+',
        developers: ['Nintendo EPD'],
        publishers: ['Nintendo', 'Other '],
        genres: ['Adventure', 'Action'],
        barcode: '456',
        description: 'Open air',
      })
    );

    const html = await (await get('/games/g1')).text();

    assert.equal(urls[0].pathname, '/Games/g1');
    assert.equal(field(html, 'Platform'), 'Switch');
    assert.equal(field(html, 'Format'), 'Cart');
    assert.equal(field(html, 'Release Date'), '2017-03-03');
    assert.equal(field(html, 'Rating'), 'E10+');
    assert.equal(field(html, 'Developers'), 'Nintendo EPD');
    assert.equal(field(html, 'Publishers'), 'Nintendo, Other');
    assert.equal(field(html, 'Genres'), 'Adventure, Action');
    assert.equal(field(html, 'Barcode'), '456');
    assert.equal(field(html, 'Description'), 'Open air');
    assert.match(html, /href="\/games\/g1\/edit"/);
    assert.match(html, /href="\/games\/g1\/delete"/);
  });

  it('renders music with formatted durations and the disc list', async (t) => {
    const urls = stubApi(t, () =>
      Response.json({
        id: 'x1',
        type: 'Musics',
        title: 'Album',
        artist: 'Band',
        format: 'CD',
        releaseDate: '1999',
        genres: ['Rock', 'Pop'],
        duration: 185000,
        label: 'Label',
        barcode: '789',
        tracks: 2,
        discs: 1,
        discList: [
          { trackNumber: 1, title: 'First', duration: 65000 },
          { trackNumber: 2, title: 'Second', duration: null },
        ],
      })
    );

    const html = await (await get('/musics/x1')).text();

    assert.equal(urls[0].pathname, '/Musics/x1');
    assert.equal(field(html, 'Artist'), 'Band');
    assert.equal(field(html, 'Genres'), 'Rock, Pop');
    assert.equal(field(html, 'Duration'), '3:05');
    assert.equal(field(html, 'Label'), 'Label');
    assert.equal(field(html, 'Barcode'), '789');
    assert.equal(field(html, 'Tracks'), '2');
    assert.equal(field(html, 'Discs'), '1');
    assert.match(html, /<td class="py-1 px-2">First<\/td><td class="py-1 px-2">1:05<\/td>/);
    assert.match(html, /<td class="py-1 px-2">Second<\/td><td class="py-1 px-2"><\/td>/);
    assert.match(html, /href="\/musics\/x1\/edit"/);
    assert.match(html, /href="\/musics\/x1\/delete"/);
  });

  it('shows image lookup failures read-only, and nothing for a clean lookup', async (t) => {
    stubApi(t, () =>
      Response.json({
        ...book,
        imageLookup: { lookupAttemptedAt: '2025-01-01', failureReason: 'No match', permanentFailure: false },
      })
    );
    const failed = await (await get('/books/b1')).text();
    assert.match(failed, /Image lookup failed and will be retried\. Reason: No match/);

    stubApi(t, () => Response.json({ ...book, imageLookup: { lookupAttemptedAt: 'x', permanentFailure: true } }));
    assert.match(await (await get('/books/b1')).text(), /failed permanently/);

    stubApi(t, () => Response.json({ ...book, imageLookup: { lookupAttemptedAt: 'x', permanentFailure: false } }));
    assert.doesNotMatch(await (await get('/books/b1')).text(), /Image lookup/);
  });

  it('escapes the title', async (t) => {
    stubApi(t, () => Response.json({ ...book, title: '<script>alert(1)</script>' }));

    const html = await (await get('/books/b1')).text();

    assert.doesNotMatch(html, /<script>alert/);
    assert.match(html, /&lt;script&gt;alert\(1\)&lt;\/script&gt;/);
  });

  for (const [path, label] of [
    ['/books/nope', 'Book'],
    ['/movies/nope', 'Movie'],
    ['/games/nope', 'Game'],
    ['/musics/nope', 'Music'],
  ] as const) {
    it(`${path} returns a 404 page when the item is missing`, async (t) => {
      stubApi(t, () => new Response('gone', { status: 404 }));

      const response = await get(path);
      const html = await response.text();

      assert.equal(response.status, 404);
      assert.match(html, new RegExp(`<h1 class="text-2xl">${label} not found</h1>`));
      assert.match(html, new RegExp(`<a href="${path.split('/').slice(0, 2).join('/')}">Back to`));
    });
  }

  it('shows an error page with a 500 when the API fails', async (t) => {
    stubApi(t, () => new Response('boom', { status: 503 }));

    const response = await get('/books/b1');
    const html = await response.text();

    assert.equal(response.status, 500);
    assert.match(html, /<h1 class="text-2xl">Couldn't load this book<\/h1>/);
    assert.match(html, /Please try again/);
    assert.match(html, /<a href="\/books">Back to Books<\/a>/);
  });

  it('shows the same error page when the API is unreachable', async (t) => {
    const original = globalThis.fetch;
    globalThis.fetch = (async () => {
      throw new TypeError('fetch failed');
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    const response = await get('/movies/m1');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /Please try again/);
  });
});
