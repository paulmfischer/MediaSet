import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

import { isActivePath } from './ui/nav.ts';
import { createRouter } from 'remix/router';
import { render } from 'remix/middleware/render';
import { assets } from './assets.ts';
import { Document } from './ui/document.tsx';
import { router } from './router.ts';
import { routes } from './routes.ts';
import { createRequestHandler, readHttpsOptions, readPort } from './server-config.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

const emptyStats = {
  bookStats: {
    total: 0,
    totalFormats: 0,
    formats: [],
    uniqueAuthors: 0,
    totalPages: 0,
    avgPages: 0,
    formatBreakdown: {},
    decadeBreakdown: {},
    pageCountBuckets: {},
    topAuthors: [],
    topGenres: [],
  },
  movieStats: {
    total: 0,
    totalFormats: 0,
    formats: [],
    totalTvSeries: 0,
    formatBreakdown: {},
    decadeBreakdown: {},
    genreBreakdown: {},
    topStudios: [],
  },
  gameStats: {
    total: 0,
    totalFormats: 0,
    formats: [],
    totalPlatforms: 0,
    platforms: [],
    formatBreakdown: {},
    platformBreakdown: {},
    decadeBreakdown: {},
    genreBreakdown: {},
    topPublishers: [],
    topDevelopers: [],
  },
  musicStats: {
    total: 0,
    totalFormats: 0,
    formats: [],
    uniqueArtists: 0,
    totalTracks: 0,
    avgTracks: 0,
    uniqueLabels: 0,
    totalDiscs: 0,
    formatBreakdown: {},
    decadeBreakdown: {},
    genreBreakdown: {},
    topArtists: [],
    topLabels: [],
  },
};

const fullStats = {
  bookStats: {
    ...emptyStats.bookStats,
    total: 6,
    totalFormats: 2,
    uniqueAuthors: 4,
    totalPages: 2400,
    avgPages: 400,
    formatBreakdown: { Hardcover: 5, Paperback: 1 },
    decadeBreakdown: { '1960s': 2, '1990s': 4 },
    pageCountBuckets: { '0-200': 1, '201-400': 5 },
    topAuthors: [{ name: 'Frank Herbert', count: 3 }],
    topGenres: [{ name: 'Science Fiction', count: 4 }],
  },
  movieStats: {
    ...emptyStats.movieStats,
    total: 3,
    totalFormats: 1,
    totalTvSeries: 1,
    formatBreakdown: { Bluray: 3 },
    topStudios: [{ name: 'Pixar', count: 2 }],
  },
  gameStats: { ...emptyStats.gameStats, total: 1 },
  musicStats: { ...emptyStats.musicStats, total: 0 },
};

// Stubs the API: /stats and /config/integrations answer from the arguments.
function stubDashboardApi(
  t: { after: (fn: () => void) => void },
  stats: Response | unknown,
  integrations: unknown = []
) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/stats') return stats instanceof Response ? stats : Response.json(stats);
    if (url.pathname === '/config/integrations') return Response.json(integrations);
    return new Response('not found', { status: 404 });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

describe('router', () => {
  it('GET / renders the home page document with the prebuilt stylesheet and browser entry', async (t) => {
    stubDashboardApi(t, emptyStats);
    const response = await get(routes.home.href());

    assert.equal(response.status, 200);
    assert.match(response.headers.get('Content-Type') ?? '', /text\/html/);
    const html = await response.text();
    assert.match(html, /<html lang="en">/);
    assert.match(html, /<title>Dashboard \| MediaSet<\/title>/);
    assert.match(html, /<link rel="stylesheet" href="\/app.css"/);
    assert.match(html, /<script type="module" src="\/assets\/app\/actions\/public\/entry\.ts[^"]*"/);
  });

  it('renders the header nav links with per-media-type accents and marks only the current path active', async (t) => {
    stubDashboardApi(t, emptyStats);
    const html = await (await get(routes.home.href())).text();

    const link = (href: string) => html.match(new RegExp(`<a href="${href}" [^>]*class="[^>]*>`))?.[0] ?? '';
    assert.match(link('/'), /aria-current="page"/);
    assert.match(link('/'), /class="[^"]*\bactive\b/);
    for (const [href, accent] of [
      ['/books', 'books'],
      ['/movies', 'movies'],
      ['/games', 'games'],
      ['/musics', 'musics'],
    ]) {
      assert.match(link(href), new RegExp(`class="entity-${accent} `));
      assert.doesNotMatch(link(href), /aria-current|\bactive\b/);
    }
    assert.match(html, /<span class="text-3xl">MediaSet<\/span>/);
    assert.match(html, /aria-label="Toggle menu"/);
    assert.match(html, /aria-label="Tools"/);
  });

  it('renders the footer with the runtime APP_VERSION, falling back to a local default', async (t) => {
    const previous = { APP_VERSION: process.env.APP_VERSION, VITE_APP_VERSION: process.env.VITE_APP_VERSION };
    t.after(() => {
      for (const [name, value] of Object.entries(previous)) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
    });
    delete process.env.VITE_APP_VERSION;
    stubDashboardApi(t, emptyStats);

    delete process.env.APP_VERSION;
    assert.match(await (await get(routes.home.href())).text(), /<footer[^>]*>.*v0\.0\.0-local<\/span><\/footer>/);

    // Read per request, not at startup.
    process.env.APP_VERSION = '1.2.3';
    assert.match(
      await (await get(routes.home.href())).text(),
      /<footer[^>]*>.*Copyright \d{4} Paul Fischer.*v1\.2\.3<\/span><\/footer>/
    );
  });

  it('GET /config.json returns the runtime client config, cached for an hour', async (t) => {
    // showErrorDetails is read from the environment per request.
    const previous = process.env.SHOW_ERROR_DETAILS;
    process.env.SHOW_ERROR_DETAILS = 'true';
    t.after(() => {
      if (previous === undefined) delete process.env.SHOW_ERROR_DETAILS;
      else process.env.SHOW_ERROR_DETAILS = previous;
    });

    const response = await get(routes.config.href());

    assert.equal(response.status, 200);
    assert.equal(response.headers.get('Cache-Control'), 'public, max-age=3600');
    assert.match(response.headers.get('Content-Type') ?? '', /application\/json/);
    assert.deepEqual(await response.json(), { apiUrl: 'http://localhost:7130', showErrorDetails: true });
  });

  it('serves files from public/', async () => {
    const response = await get('/favicon.ico');

    assert.equal(response.status, 200);
  });

  it('returns 404 for unknown paths', async () => {
    const response = await get('/no-such-page/at-all');

    assert.equal(response.status, 404);
  });
});

describe('home dashboard', () => {
  const panel = (html: string, id: string) =>
    html.match(new RegExp(`<div id="dashboard-panel-${id}"[\\s\\S]*?(?=<div id="dashboard-panel-|$)`))?.[0] ?? '';

  it('should render the overview heading, tabs with counts and percentages, and every panel when the collection has items', async (t) => {
    stubDashboardApi(t, fullStats);

    const response = await get('/');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /Collection Overview\s*<span[^>]*>10 items<\/span>/);
    assert.match(html, /<label for="dashboard-tab-books"[^>]*>[\s\S]*?Books[\s\S]*?>6<[\s\S]*?60\.0% of collection/);
    assert.match(
      html,
      /<label for="dashboard-tab-movies"[^>]*>[\s\S]*?Movies &amp; TV[\s\S]*?>3<[\s\S]*?30\.0% of collection/
    );
    assert.match(html, /<label for="dashboard-tab-games"[^>]*>[\s\S]*?Games[\s\S]*?10\.0% of collection/);
    assert.doesNotMatch(html, /dashboard-tab-musics/);
    assert.match(html, /id="dashboard-panel-books"/);
    assert.match(html, /id="dashboard-panel-movies"/);
    assert.match(html, /id="dashboard-panel-games"/);
    assert.doesNotMatch(html, /id="dashboard-panel-musics"/);
  });

  it('should select the Books tab by default', async (t) => {
    stubDashboardApi(t, fullStats);

    const html = await (await get('/')).text();

    assert.match(html, /<input[^>]*id="dashboard-tab-books"[^>]*checked/);
    assert.doesNotMatch(html, /<input[^>]*id="dashboard-tab-movies"[^>]*checked/);
  });

  it('should select the first type with items when there are no books', async (t) => {
    stubDashboardApi(t, { ...fullStats, bookStats: emptyStats.bookStats });

    const html = await (await get('/')).text();

    assert.doesNotMatch(html, /dashboard-tab-books/);
    assert.match(html, /<input[^>]*id="dashboard-tab-movies"[^>]*checked/);
  });

  it('should render the books stat cards and charts', async (t) => {
    stubDashboardApi(t, fullStats);

    const books = panel(await (await get('/')).text(), 'books');

    for (const [title, value] of [
      ['Total Pages', '2,400'],
      ['Avg Pages', '400'],
      ['Unique Authors', '4'],
      ['Formats', '2'],
    ]) {
      assert.match(books, new RegExp(`${title}</p><p class="[^"]*">${value}</p>`));
    }
    for (const title of [
      'Format Breakdown',
      'Page Count Distribution',
      'Top Authors',
      'Publication Decade',
      'Top Genres',
    ]) {
      assert.match(books, new RegExp(`>${title}</p>`));
    }
    // Pie slices carry hover detail and the legend shows counts and percentages.
    assert.match(books, /<title>Hardcover: 5 · 83\.3%<\/title>/);
    assert.match(books, /Paperback<\/span><span[^>]*>1 · 16\.7%/);
    assert.match(books, /<svg[^>]*aria-label="Pie chart"/);
    assert.match(books, /Frank Herbert/);
  });

  it('should show pie charts only when they have more than one slice', async (t) => {
    stubDashboardApi(t, fullStats);

    const movies = panel(await (await get('/')).text(), 'movies');

    // One format and a movie plus a TV series: only the type pie shows.
    assert.match(movies, />Movies vs TV Series</);
    assert.match(movies, /Movies: 2 · 66\.7%/);
    assert.doesNotMatch(movies, />Format Breakdown</);
    assert.match(movies, />Top Studios</);
    assert.doesNotMatch(movies, />Release Decade</);
  });

  it('should keep tiny pie slices visible', async (t) => {
    const bookStats = { ...fullStats.bookStats, total: 1001, formatBreakdown: { Hardcover: 1000, Paperback: 1 } };
    stubDashboardApi(t, { ...fullStats, bookStats });

    const books = panel(await (await get('/')).text(), 'books');
    const paths = [...books.matchAll(/<path d="M 70 70 L (\S+) (\S+) A \S+ \S+ 0 \d 1 (\S+) (\S+) Z"/g)];

    assert.equal(paths.length, 2);
    // The small slice spans 8 degrees: its endpoints are about 2 * r * sin(4 degrees) apart.
    const [sx, sy, ex, ey] = paths[1].slice(1).map(Number);
    const chord = Math.hypot(ex - sx, ey - sy);
    assert.ok(chord > 9 && chord < 10, `chord was ${chord}`);
  });

  it('should show the empty state when the collection has no items', async (t) => {
    stubDashboardApi(t, emptyStats);

    const response = await get('/');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /No media items yet/);
    assert.doesNotMatch(html, /Collection Overview/);
    assert.doesNotMatch(html, /dashboard-tab-/);
  });

  it('should show an error state with a 500 and keep the shell and attribution when the API fails', async (t) => {
    stubDashboardApi(t, new Response('boom', { status: 503 }), [
      {
        key: 'tmdb',
        displayName: 'TMDB',
        enabled: true,
        attributionUrl: 'https://www.themoviedb.org',
        attributionText: 'Powered by TMDB',
        logoPath: '/tmdb.svg',
      },
    ]);

    const response = await get('/');
    const html = await response.text();

    assert.equal(response.status, 500);
    assert.match(html, /<h1 class="text-2xl">Couldn't load the dashboard<\/h1>/);
    assert.match(html, /Please try again/);
    assert.match(html, /<title>Dashboard \| MediaSet<\/title>/);
    assert.match(html, /<footer/);
    assert.match(html, /Powered by TMDB/);
  });

  it('should show the same error state when the API is unreachable', async (t) => {
    const original = globalThis.fetch;
    globalThis.fetch = (async () => {
      throw new TypeError('fetch failed');
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    const response = await get('/');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /Couldn't load the dashboard/);
  });

  it('should render attribution badges for enabled integrations only', async (t) => {
    stubDashboardApi(t, fullStats, [
      {
        key: 'tmdb',
        displayName: 'TMDB',
        enabled: true,
        attributionUrl: 'https://www.themoviedb.org',
        attributionText: 'Powered by TMDB',
        logoPath: '/tmdb.svg',
      },
      { key: 'igdb', displayName: 'IGDB', enabled: false, logoPath: '/igdb.svg' },
    ]);

    const html = await (await get('/')).text();

    assert.match(html, /<h2 id="attribution-heading"[^>]*>\s*Attribution/);
    assert.match(html, /aria-label="TMDB"/);
    assert.match(html, /Powered by TMDB/);
    assert.doesNotMatch(html, /IGDB/);
  });

  it('should omit the attribution section when no integration is enabled', async (t) => {
    stubDashboardApi(t, fullStats, []);

    assert.doesNotMatch(await (await get('/')).text(), /attribution-heading/);
  });
});

describe('header on other pages', () => {
  // The only real page is Home today, so render the layout at a path owned by a later ticket.
  const other = createRouter({ middleware: [render({ assets })] });
  other.get('/image-stats', (context) =>
    context.render(
      <Document path={context.url.pathname} title="Stats">
        <p>stats</p>
      </Document>
    )
  );

  it('marks the Image Statistics link and the Tools trigger active on /image-stats, and Home inactive', async () => {
    const html = await (await other.fetch(new URL('/image-stats', 'http://localhost'))).text();

    assert.match(html, /<button[^>]*aria-label="Tools"[^>]*class="[^"]*\bactive\b/);
    assert.doesNotMatch(html, /<a href="\/" aria-current/);
    assert.match(html, /Skip to main content/);
  });
});

describe('isActivePath', () => {
  it('matches Home only on the root and other links on their path and below', () => {
    assert.equal(isActivePath('/', '/'), true);
    assert.equal(isActivePath('/books', '/'), false);
    assert.equal(isActivePath('/books', '/books'), true);
    assert.equal(isActivePath('/books/42', '/books'), true);
    assert.equal(isActivePath('/books/42/edit', '/books'), true);
    assert.equal(isActivePath('/bookshelf', '/books'), false);
    assert.equal(isActivePath('/movies', '/books'), false);
    assert.equal(isActivePath('/image-stats', '/image-stats'), true);
  });
});

// Echoes what reached the API so tests can assert on the forwarded request.
async function echoApi(input: RequestInfo | URL, init?: RequestInit) {
  const request = new Request(input, init);
  return Response.json({
    url: request.url,
    method: request.method,
    contentType: request.headers.get('Content-Type'),
    body: await request.text(),
  });
}

const devEnv = { NODE_ENV: 'development' };

describe('dev API proxy', () => {
  it('strips /dev-api and forwards to the API; other requests reach the app', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, devEnv, { fetch: apiFetch as typeof fetch });

    const proxied = await handler(new Request('https://192.168.1.20:3000/dev-api/static/images/books/1.jpg?w=1'));
    const root = await handler(new Request('http://localhost:3000/dev-api'));
    const app = await handler(new Request('http://localhost:3000/config.json'));

    assert.equal((await proxied.json()).url, 'http://127.0.0.1:5000/static/images/books/1.jpg?w=1');
    assert.equal((await root.json()).url, 'http://127.0.0.1:5000/');
    assert.equal(apiFetch.mock.calls.length, 2);
    assert.equal(app.headers.get('Cache-Control'), 'public, max-age=3600');
  });

  it('forwards the method, content type and body of POST and PUT requests', async (t) => {
    const handler = createRequestHandler(
      router.fetch,
      { ...devEnv, apiUrl: 'http://api.test:8080' },
      { fetch: t.mock.fn(echoApi) as typeof fetch }
    );
    const upload = new FormData();
    upload.set('image', new File(['fake-jpeg'], 'cover.jpg', { type: 'image/jpeg' }));

    const post = await (
      await handler(new Request('http://localhost:3000/dev-api/books', { method: 'POST', body: upload }))
    ).json();
    const put = await (
      await handler(
        new Request('http://localhost:3000/dev-api/books/42', {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title: 'Dune' }),
        })
      )
    ).json();

    assert.equal(post.url, 'http://api.test:8080/books');
    assert.equal(post.method, 'POST');
    assert.match(post.contentType, /^multipart\/form-data; boundary=/);
    assert.match(post.body, /filename="cover.jpg"[\s\S]*fake-jpeg/);
    assert.deepEqual(put, {
      url: 'http://api.test:8080/books/42',
      method: 'PUT',
      contentType: 'application/json',
      body: '{"title":"Dune"}',
    });
  });

  it('does not treat paths that only start with "dev-api" as API requests', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, devEnv, { fetch: apiFetch as typeof fetch });

    const response = await handler(new Request('http://localhost:3000/dev-apis'));

    assert.equal(response.status, 404);
    assert.equal(apiFetch.mock.calls.length, 0);
  });

  it('is not installed in production, so /dev-api is a 404', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, { NODE_ENV: 'production' }, { fetch: apiFetch as typeof fetch });

    const response = await handler(new Request('http://localhost:3000/dev-api/health'));

    assert.equal(response.status, 404);
    assert.equal(apiFetch.mock.calls.length, 0);
  });
});

describe('server config', () => {
  it('defaults the port to 3000 and rejects values that are not a valid port', () => {
    assert.equal(readPort({}), 3000);
    assert.equal(readPort({ PORT: '' }), 3000);
    assert.equal(readPort({ PORT: '3005' }), 3005);
    for (const PORT of ['abc', '30x0', '3.5', '0', '70000', '-1']) {
      assert.throws(() => readPort({ PORT }), /PORT must be an integer between 1 and 65535/);
    }
  });

  it('serves HTTPS only in mobile mode outside production, and requires both certificate settings', (t) => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mediaset-certs-'));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const DEV_HTTPS_CERT = path.join(dir, 'dev.pem');
    const DEV_HTTPS_KEY = path.join(dir, 'dev-key.pem');
    fs.writeFileSync(DEV_HTTPS_CERT, 'cert');
    fs.writeFileSync(DEV_HTTPS_KEY, 'key');
    const certs = { DEV_HTTPS_CERT, DEV_HTTPS_KEY };

    assert.equal(readHttpsOptions({ ...devEnv, ...certs }), null);
    assert.equal(readHttpsOptions({ NODE_ENV: 'production', MEDIASET_DEV_HTTPS: '1', ...certs }), null);
    assert.throws(
      () => readHttpsOptions({ ...devEnv, MEDIASET_DEV_HTTPS: '1', DEV_HTTPS_CERT }),
      /Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY/
    );
    const options = readHttpsOptions({ ...devEnv, MEDIASET_DEV_HTTPS: '1', ...certs });
    assert.equal(String(options?.cert), 'cert');
    assert.equal(String(options?.key), 'key');
  });
});
