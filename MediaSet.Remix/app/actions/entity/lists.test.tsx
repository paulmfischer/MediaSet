import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';
import { listHref, parseListParams } from './list-params.ts';

const book = {
  id: 'b1',
  type: 'Books',
  title: 'Dune',
  subtitle: 'Book One',
  authors: ['Frank Herbert '],
  format: 'Hardcover',
  pages: 412,
  coverImage: { filePath: 'books/b1.jpg' },
};

function paged(items: unknown[], overrides: Record<string, unknown> = {}) {
  return { items, totalCount: items.length, page: 1, pageSize: 75, totalPages: 1, ...overrides };
}

// Stubs the API and records the URLs the page asked for.
function stubApi(t: { after: (fn: () => void) => void }, body: unknown) {
  const original = globalThis.fetch;
  const urls: URL[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    urls.push(new URL(input instanceof Request ? input.url : String(input)));
    return Response.json(body);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return urls;
}

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

describe('list params', () => {
  it('defaults and clamps invalid values', () => {
    assert.deepEqual(parseListParams(new URL('http://x/books')), {
      searchText: null,
      orderBy: 'title:asc',
      page: 1,
    });
    assert.equal(parseListParams(new URL('http://x/books?page=abc')).page, 1);
    assert.equal(parseListParams(new URL('http://x/books?page=-3')).page, 1);
    assert.deepEqual(parseListParams(new URL('http://x/books?searchText=dune&orderBy=pages:desc&page=3')), {
      searchText: 'dune',
      orderBy: 'pages:desc',
      page: 3,
    });
  });

  it('builds hrefs carrying only the state that is set', () => {
    assert.equal(listHref('/books', {}), '/books');
    assert.equal(
      listHref('/books', { searchText: 'a b', orderBy: 'title:desc', page: 2 }),
      '/books?searchText=a+b&orderBy=title%3Adesc&page=2'
    );
    assert.equal(listHref('/books', { page: 1 }), '/books');
  });
});

describe('collection lists', () => {
  it('passes search, sort and page to the paged search API', async (t) => {
    const urls = stubApi(t, paged([book], { page: 2, totalPages: 3, totalCount: 150 }));

    const response = await get('/books?searchText=dune&orderBy=pages:desc&page=2');

    assert.equal(response.status, 200);
    assert.equal(urls[0].pathname, '/Books/pagedsearch');
    assert.equal(urls[0].searchParams.get('searchText'), 'dune');
    assert.equal(urls[0].searchParams.get('orderBy'), 'pages:desc');
    assert.equal(urls[0].searchParams.get('page'), '2');
  });

  it('uses title:asc and page 1 by default', async (t) => {
    const urls = stubApi(t, paged([book]));

    await get('/books');

    assert.equal(urls[0].searchParams.get('orderBy'), 'title:asc');
    assert.equal(urls[0].searchParams.get('page'), '1');
  });

  it('renders book columns, cover, sortable headers and links', async (t) => {
    stubApi(t, paged([book]));

    const html = await (await get('/books')).text();

    assert.match(html, /<title>Books List \| MediaSet<\/title>/);
    assert.match(html, /<a href="\/books\/b1"[^>]*>Dune: Book One<\/a>/);
    assert.match(html, /Frank Herbert<\/td>/);
    assert.match(html, />412<\/td>/);
    assert.match(html, /src="[^"]*\/static\/images\/books\/b1\.jpg"/);
    assert.match(html, /aria-sort="ascending"/);
    assert.match(html, /href="\/books\?orderBy=title%3Adesc"/);
    assert.match(html, /href="\/books\?orderBy=pages%3Aasc"/);
    assert.match(html, /href="\/books\/b1\/edit"/);
    assert.match(html, /href="\/books\/add"/);
  });

  it('shows the empty state without a table or pagination', async (t) => {
    stubApi(t, paged([]));

    const html = await (await get('/movies')).text();

    assert.match(html, /have any Movies!/);
    assert.doesNotMatch(html, /<table/);
    assert.doesNotMatch(html, /aria-label="Pagination"/);
  });

  it('shows a clear link only while searching, and keeps search in sort and page links', async (t) => {
    stubApi(t, paged([book], { page: 2, totalPages: 3, totalCount: 150 }));

    const html = await (await get('/books?searchText=dune&page=2')).text();

    assert.match(html, /aria-label="Clear search"/);
    assert.match(html, /href="\/books\?searchText=dune&amp;orderBy=authors%3Aasc"/);
    assert.match(html, /href="\/books\?searchText=dune&amp;orderBy=title%3Aasc&amp;page=3"/);
    // Prev from page 2 drops the page parameter.
    assert.match(html, /href="\/books\?searchText=dune&amp;orderBy=title%3Aasc" class="flex items-center gap-1"/);
    assert.match(html, /Page 2 of 3/);
    assert.match(html, /150 items/);
  });

  for (const [path, apiPath, headers] of [
    ['/movies', '/Movies/pagedsearch', ['Format', 'Runtime', 'TV Show']],
    ['/games', '/Games/pagedsearch', ['Platform', 'Format', 'Developers']],
    ['/musics', '/Musics/pagedsearch', ['Artist', 'Format', 'Tracks']],
  ] as const) {
    it(`${path} calls ${apiPath} and renders its columns`, async (t) => {
      const urls = stubApi(t, paged([{ id: 'x1', title: 'Thing' }]));

      const html = await (await get(path)).text();

      assert.equal(urls[0].pathname, apiPath);
      for (const header of headers) assert.match(html, new RegExp(`${header}`));
      assert.match(html, new RegExp(`<a href="${path}/x1"[^>]*>Thing</a>`));
    });
  }

  it('marks the matching header nav link active', async (t) => {
    stubApi(t, paged([book]));

    const html = await (await get('/books')).text();

    assert.match(html, /<a href="\/books" [^>]*aria-current="page"/);
  });
});
