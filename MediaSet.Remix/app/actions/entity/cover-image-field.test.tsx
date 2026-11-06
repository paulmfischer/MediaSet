import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const FORMS = ['books', 'movies', 'games', 'musics'] as const;

// Stubs the API: lookup is on, every metadata list is empty.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(new Request(input, init).url);
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({
        supportsBookLookup: true,
        supportsMovieLookup: true,
        supportsGameLookup: true,
        supportsMusicLookup: true,
      });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

const get = async (path: string) => (await router.fetch(new URL(path, 'http://localhost'))).text();

describe('cover image field on the entity forms', () => {
  for (const path of FORMS) {
    it(`should place the ${path} cover image right after the lookup panel, with the upload area and its limits`, async (t) => {
      stubApi(t);
      const html = await get(`/${path}/add`);

      const [panel, cover, format] = ['role="group"', 'Cover Image', 'name="format"'].map((part) => html.indexOf(part));
      assert.ok(panel !== -1 && panel < cover!, 'after the lookup panel');
      assert.ok(cover! < format!, 'before the format field');
      assert.match(html, /Drag and drop your image here/);
      assert.match(html, /or click to select/);
      assert.match(html, /PNG or JPEG up to 5MB/);
      assert.match(html, /<label for="imageUrl"[^>]*>\s*Image URL/);
      assert.match(
        html,
        /Provide an image URL or upload a file above\. If both are provided, the uploaded file takes precedence\.\s*Accepted: JPEG or PNG, up to 5MB\./
      );
      assert.match(html, /placeholder="https:\/\/example\.com\/image\.jpg"/);
    });
  }

  for (const [path, label] of [
    ['books', 'Add Book'],
    ['movies', 'Add Movie'],
    ['games', 'Add Game'],
    ['musics', 'Add Music'],
  ] as const) {
    it(`should show ${label} before a Cancel button on the ${path} add form`, async (t) => {
      stubApi(t);
      const html = await get(`/${path}/add`);

      const submit = html.indexOf(`>${label}</button>`);
      const cancel = html.search(/<button[^>]*class="secondary"[^>]*>Cancel<\/button>/);
      assert.ok(submit !== -1 && cancel !== -1 && submit < cancel, 'submit then Cancel');
      assert.doesNotMatch(html, /<a [^>]*>Cancel<\/a>/);
    });
  }

  for (const [path, group, second, identifier] of [
    ['books', 'Search by title and author', 'id="lookupAuthor"', 'id="isbn"'],
    ['musics', 'Search by title and artist', 'name="artist"', 'id="barcode"'],
  ] as const) {
    it(`should group the ${path} title search fields with one Search button below them`, async (t) => {
      stubApi(t);
      const html = await get(`/${path}/add`);

      const parts = [`aria-label="${group}"`, 'id="title"', second, 'value="lookup:title"', identifier].map((part) =>
        html.indexOf(part)
      );
      assert.ok(
        parts.every((at, index) => at !== -1 && (index === 0 || at > parts[index - 1]!)),
        'group, title, second field, its Search, then the identifier'
      );
      assert.equal(html.split('value="lookup:title"').length - 1, 1);
      assert.doesNotMatch(html, />or</);
    });
  }
});
