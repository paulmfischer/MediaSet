import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const CASES = [
  { type: 'Books', path: 'books', capability: 'supportsBookLookup', field: 'isbn', label: 'ISBN' },
  { type: 'Movies', path: 'movies', capability: 'supportsMovieLookup', field: 'barcode', label: 'barcode' },
  { type: 'Games', path: 'games', capability: 'supportsGameLookup', field: 'barcode', label: 'barcode' },
  { type: 'Musics', path: 'musics', capability: 'supportsMusicLookup', field: 'barcode', label: 'barcode' },
] as const;

const SCAN_BUTTON_ENTRY = '/assets/app/ui/forms/public/scan-button.tsx';
const SCANNER_DIALOG_ENTRY = '/assets/app/ui/forms/public/barcode-scanner-dialog.tsx';

// Stubs the API: one saved item per type with id `x1`, and lookup capabilities that are all on or all off.
function stubApi(t: { after: (fn: () => void) => void }, supported: boolean) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(new Request(input, init).url);
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json(Object.fromEntries(CASES.map((entry) => [entry.capability, supported])));
    }
    if (/\/(Books|Movies|Games|Musics)\/x1$/.test(url.pathname)) return Response.json({ id: 'x1', title: 'Saved' });
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

const get = async (path: string) => (await router.fetch(new URL(path, 'http://localhost'))).text();

describe('barcode scanner on the entity forms', () => {
  for (const { type, path, field, label } of CASES) {
    for (const [form, url] of [
      ['add', `/${path}/add`],
      ['edit', `/${path}/x1/edit`],
    ] as const) {
      it(`should hydrate the scan button and scanner dialog on the ${type} ${form} form when lookup is available`, async (t) => {
        stubApi(t, true);
        const html = await get(url);

        assert.ok(html.includes(`href="${SCAN_BUTTON_ENTRY}"`), 'scan button entry is preloaded');
        assert.ok(html.includes(`href="${SCANNER_DIALOG_ENTRY}"`), 'scanner dialog entry is preloaded');
        assert.match(html, new RegExp(`"field":"${field}"`));
        assert.match(html, new RegExp(`"identifierLabel":"${label}"`));
        // The scanner runs the field's existing lookup button.
        assert.match(html, new RegExp(`value="lookup:${field}"`));
        // The decoder is fetched on demand (the import map only says where it lives), never preloaded.
        assert.doesNotMatch(html, /<link[^>]*modulepreload[^>]*zxing/);
        assert.doesNotMatch(html, /<script[^>]*src="[^"]*zxing/);
      });

      it(`should omit the scanner from the ${type} ${form} form when lookup is unavailable`, async (t) => {
        stubApi(t, false);
        const html = await get(url);

        assert.match(html, /<form method="post"/);
        assert.ok(!html.includes(SCAN_BUTTON_ENTRY));
        assert.ok(!html.includes(SCANNER_DIALOG_ENTRY));
        assert.doesNotMatch(html, /value="lookup:/);
      });
    }
  }

  for (const { type, path, label } of CASES) {
    it(`should group the ${type} lookup fields in an entity-tinted panel with Search buttons`, async (t) => {
      stubApi(t, true);
      const html = await get(`/${path}/add`);

      const panel = new RegExp(
        `<div role="group" aria-label="${type.replace(/s$/, '')} Lookup" class="[^"]*bg-entity/10`
      );
      assert.match(html, panel);
      assert.match(html, />Search<\/button>/);
      assert.doesNotMatch(html, />Look up<\/button>/);
      assert.doesNotMatch(html, /value="lookup:[a-z]+"[^>]*class="secondary"|class="secondary"[^>]*value="lookup:/);
      assert.match(html, new RegExp(`aria-label="Search by ${label === 'ISBN' ? 'ISBN' : 'barcode'}"`));
    });

    it(`should render the ${type} lookup fields without a panel when lookup is unavailable`, async (t) => {
      stubApi(t, false);
      const html = await get(`/${path}/add`);

      assert.doesNotMatch(html, /role="group"/);
      assert.match(html, /name="title"/);
    });
  }
});
