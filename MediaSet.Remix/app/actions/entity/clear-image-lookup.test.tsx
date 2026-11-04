import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const types = [
  { path: 'books', api: 'Books', singular: 'Book', title: 'Dune' },
  { path: 'movies', api: 'Movies', singular: 'Movie', title: 'Heat' },
  { path: 'games', api: 'Games', singular: 'Game', title: 'Zelda' },
  { path: 'musics', api: 'Musics', singular: 'Music', title: 'Kind of Blue' },
];

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));
const post = (path: string) => router.fetch(new Request(new URL(path, 'http://localhost'), { method: 'POST' }));

// Stubs the API: item loads return the given lookup state, DELETE /images/lookup/<type> is recorded and answered with `resetStatus`.
function stubApi(
  t: { after: (fn: () => void) => void },
  type: (typeof types)[number],
  options: { permanentFailure?: boolean; resetStatus?: number } = {}
) {
  const original = globalThis.fetch;
  const resets: Array<{ pathname: string; body: unknown }> = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.includes('/images/lookup/')) {
      resets.push({ pathname: url.pathname, body: await request.json() });
      const status = options.resetStatus ?? 200;
      return status === 200 ? Response.json({ reset: 1 }) : new Response('nope', { status });
    }
    if (url.pathname.endsWith(`/${type.api}/i1`)) {
      return Response.json({
        id: 'i1',
        type: type.api,
        title: type.title,
        imageLookup: {
          lookupAttemptedAt: '2025-01-05T10:30:00Z',
          failureReason: 'No match',
          permanentFailure: options.permanentFailure ?? false,
        },
      });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return resets;
}

for (const type of types) {
  describe(`clearing the image lookup for ${type.path}`, () => {
    it('shows the attempt date, reason and a retry button for a failed lookup', async (t) => {
      stubApi(t, type);
      const html = await (await get(`/${type.path}/i1`)).text();
      assert.match(html, /Image lookup failed and will be retried\./);
      assert.match(html, /Reason: No match/);
      assert.match(html, /Last attempted Jan 5, 2025\./);
      assert.match(html, new RegExp(`<form method="post" action="/${type.path}/i1/clear-image-lookup">`));
      assert.match(html, /<button type="submit">Clear &amp; retry<\/button>/);
    });

    it('shows the permanent-failure note with the retry button', async (t) => {
      stubApi(t, type, { permanentFailure: true });
      const html = await (await get(`/${type.path}/i1`)).text();
      assert.match(html, /Image lookup failed permanently and will not be retried\./);
      assert.match(html, /Last attempted Jan 5, 2025\./);
      assert.match(html, /Clear &amp; retry/);
    });

    it('resets the lookup and redirects to the detail page', async (t) => {
      const resets = stubApi(t, type);
      const response = await post(`/${type.path}/i1/clear-image-lookup`);
      assert.equal(response.status, 303);
      assert.equal(response.headers.get('location'), `/${type.path}/i1`);
      assert.equal(resets.length, 1);
      assert.equal(resets[0].pathname, `/images/lookup/${type.api}`);
      assert.deepEqual(resets[0].body, { entityIds: ['i1'] });
    });

    it('renders the error page when the reset fails', async (t) => {
      stubApi(t, type, { resetStatus: 500 });
      const response = await post(`/${type.path}/i1/clear-image-lookup`);
      const html = await response.text();
      assert.equal(response.status, 500);
      assert.match(html, /retry the image lookup/);
      assert.match(html, new RegExp(`href="/${type.path}/i1"`));
    });

    it('renders the error page when the API answers 404 (the API client reports it as a failure)', async (t) => {
      stubApi(t, type, { resetStatus: 404 });
      const response = await post(`/${type.path}/i1/clear-image-lookup`);
      assert.equal(response.status, 500);
      assert.match(await response.text(), new RegExp(`href="/${type.path}/i1"`));
    });
  });
}
