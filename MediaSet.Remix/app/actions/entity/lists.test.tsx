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
    stubApi(t, paged([], { totalCount: 0, totalPages: 0 }));

    const html = await (await get('/movies')).text();

    assert.match(html, /have any Movies!/);
    assert.doesNotMatch(html, /<table/);
    assert.doesNotMatch(html, /aria-label="Pagination"/);
  });

  it('says nothing matched, not that the collection is empty, when a search has no results', async (t) => {
    stubApi(t, paged([], { totalCount: 0, totalPages: 0 }));

    const html = await (await get('/movies?searchText=zzz')).text();

    assert.match(html, /No Movies match "zzz"\./);
    assert.doesNotMatch(html, /have any Movies!/);
  });

  it('redirects a page past the last page to the last page, keeping search and sort', async (t) => {
    stubApi(t, paged([], { page: 9, totalPages: 3, totalCount: 150 }));

    const response = await get('/books?searchText=dune&orderBy=pages:desc&page=9');

    assert.equal(response.status, 302);
    assert.equal(response.headers.get('Location'), '/books?searchText=dune&orderBy=pages%3Adesc&page=3');
  });

  it('shows pagination with links to the neighbouring pages, keeping search and sort', async (t) => {
    stubApi(t, paged([book], { page: 2, totalPages: 3, totalCount: 150 }));

    const html = await (await get('/books?searchText=dune&page=2')).text();

    assert.match(html, /aria-label="Clear search"/);
    assert.match(html, /href="\/books\?searchText=dune&amp;orderBy=authors%3Aasc"/);
    assert.match(html, /<a href="\/books\?searchText=dune&amp;orderBy=title%3Aasc&amp;page=3"[^>]*>Next/);
    // Prev from page 2 drops the page parameter.
    assert.match(html, /<a href="\/books\?searchText=dune&amp;orderBy=title%3Aasc"[^>]*>(?:(?!<\/a>)[\s\S])*Prev<\/a>/);
    assert.match(html, /aria-label="Select page"/);
    assert.match(html, /150 items/);
  });

  it('disables Prev on the first page and Next on the last', async (t) => {
    stubApi(t, paged([book], { page: 1, totalPages: 2, totalCount: 100 }));
    const first = await (await get('/books')).text();
    assert.doesNotMatch(first, /<a [^>]*>(?:(?!<\/a>)[\s\S])*Prev<\/a>/);
    assert.match(first, /<a [^>]*page=2"[^>]*>Next/);

    stubApi(t, paged([book], { page: 2, totalPages: 2, totalCount: 100 }));
    const last = await (await get('/books?page=2')).text();
    assert.doesNotMatch(last, /<a [^>]*>Next/);
  });

  it('only reports a sort direction for a well-formed orderBy', async (t) => {
    stubApi(t, paged([book]));

    const html = await (await get('/books?orderBy=title')).text();

    assert.doesNotMatch(html, /aria-sort="(ascending|descending)"/);
  });

  it('escapes the search text and titles', async (t) => {
    stubApi(t, paged([{ ...book, title: '<script>alert(1)</script>' }]));

    const html = await (await get('/books?searchText=%22%3E%3Cscript%3Ex%3C%2Fscript%3E')).text();

    assert.doesNotMatch(html, /<script>alert/);
    assert.doesNotMatch(html, /"><script>x/);
    assert.match(html, /&lt;script&gt;alert\(1\)&lt;\/script&gt;/);
  });

  for (const [path, apiPath, item, headers, cells] of [
    [
      '/movies',
      '/Movies/pagedsearch',
      { id: 'x1', title: 'Thing', format: 'DVD', runtime: 100, isTvSeries: true },
      ['Format', 'Runtime', 'TV Show'],
      ['DVD', '100'],
    ],
    [
      '/games',
      '/Games/pagedsearch',
      { id: 'x1', title: 'Thing', platform: 'Switch', format: 'Cart', developers: ['Dev One ', 'Dev Two'] },
      ['Platform', 'Format', 'Developers'],
      ['Switch', 'Cart', 'Dev One, Dev Two'],
    ],
    [
      '/musics',
      '/Musics/pagedsearch',
      { id: 'x1', title: 'Thing', artist: 'Band', format: 'CD', tracks: 9 },
      ['Artist', 'Format', 'Tracks'],
      ['Band', 'CD', '9'],
    ],
  ] as const) {
    it(`${path} calls ${apiPath} and renders its header and cells`, async (t) => {
      const urls = stubApi(t, paged([item]));

      const html = await (await get(path)).text();

      assert.equal(urls[0].pathname, apiPath);
      for (const header of headers) assert.match(html, new RegExp(`<th [^>]*>\\s*<a [^>]*>${header}`));
      for (const value of cells) assert.match(html, new RegExp(`<td [^>]*>${value}</td>`));
      assert.match(html, new RegExp(`<a href="${path}/x1"[^>]*>Thing</a>`));
    });
  }

  it('shows a check mark for a TV series and none for a movie, and 0 for a missing runtime', async (t) => {
    stubApi(
      t,
      paged([
        { id: 'a', title: 'Show', isTvSeries: true },
        { id: 'b', title: 'Film' },
      ])
    );

    const html = await (await get('/movies')).text();
    const row = (id: string) => html.split('<tr ').find((chunk) => chunk.includes(`<a href="/movies/${id}">`)) ?? '';

    assert.match(row('a'), /<div class="flex justify-center"><svg/);
    assert.match(row('b'), /<div class="flex justify-center"><\/div>/);
    assert.match(row('b'), />0<\/td>/);
  });

  it('marks the matching header nav link active', async (t) => {
    stubApi(t, paged([book]));

    const html = await (await get('/books')).text();

    assert.match(html, /<a href="\/books" [^>]*aria-current="page"/);
  });
});
