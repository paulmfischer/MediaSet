import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { clientApiUrl as configuredApiUrl } from '../constants.server.ts';
import { createAppRouter } from '../router.ts';

const book = { id: 'b1', type: 'Books', title: 'Dune', coverImage: { filePath: 'books/b1.png' } };

// 1x1 transparent PNG.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64'
);

// The home page loads dashboard stats, so serve an empty collection: no items, no API needed.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/stats') {
      return Response.json({
        bookStats: { total: 0 },
        movieStats: { total: 0 },
        gameStats: { total: 0 },
        musicStats: { total: 0 },
      });
    }
    if (url.pathname === '/Books/b1') return Response.json(book);
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

describe('production Content-Security-Policy (e2e)', () => {
  it('hydrates an island and handles a client interaction with the CSP enforced', async (t) => {
    stubApi(t);
    // The test server runs outside production, so force the production policy on.
    const page = await t.serve(await createTestServer(createAppRouter({ production: true }).fetch));
    const problems: string[] = [];
    page.on('pageerror', (error) => problems.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error' || message.text().includes('Content Security Policy')) {
        problems.push(message.text());
      }
    });
    await page.setViewportSize({ width: 390, height: 844 });

    const response = await page.goto('/');
    const policy = (await response?.headerValue('content-security-policy')) ?? '';
    assert.match(policy, /script-src[^;]*blob:/);
    assert.match(policy, /(^|; )worker-src 'none'(;|$)/);
    await page.waitForLoadState('networkidle');

    // The menu toggle only works once its client entry has loaded and hydrated under the CSP.
    const toggle = page.getByRole('button', { name: 'Toggle menu' });
    const menu = page.getByRole('navigation', { name: 'Mobile' });
    assert.equal(await menu.count(), 0);
    await toggle.click();
    await menu.waitFor();
    assert.equal(await toggle.getAttribute('aria-expanded'), 'true');
    await page.keyboard.press('Escape');
    await menu.waitFor({ state: 'detached' });

    assert.deepEqual(problems, []);
  });

  it('hydrates through the import-map polyfill with the CSP enforced', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(createAppRouter({ production: true }).fetch));
    const problems: string[] = [];
    page.on('pageerror', (error) => problems.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error' || message.text().includes('Content Security Policy')) {
        problems.push(message.text());
      }
    });
    // Browsers with import maps but not multiple of them (Chromium before 133, older Safari and Firefox) load every
    // client entry through the polyfill, which parses modules with a WebAssembly lexer and re-links them as blob:
    // URLs, so it needs script-src blob: and 'wasm-unsafe-eval'. Chromium has native support: make the polyfill's
    // feature-detection frame (the only frame here) lose its second import map so detection fails and it kicks in.
    await page.addInitScript(() => {
      if (window === window.top) return;
      const append = Node.prototype.appendChild;
      let maps = 0;
      Node.prototype.appendChild = function <T extends Node>(this: Node, node: T): T {
        if (node instanceof HTMLScriptElement && node.type === 'importmap' && (maps += 1) > 1) {
          // Same-origin srcdoc frame: record in the page that detection was made to fail.
          (window.top as unknown as { __detectionStripped?: boolean }).__detectionStripped = true;
          return node;
        }
        return append.call(this, node) as T;
      };
    });
    await page.setViewportSize({ width: 390, height: 844 });

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const toggle = page.getByRole('button', { name: 'Toggle menu' });
    const menu = page.getByRole('navigation', { name: 'Mobile' });
    // Guard against this test silently turning into a copy of the hydration test: the polyfill path was taken.
    assert.equal(
      await page.evaluate(() => (window as unknown as { __detectionStripped?: boolean }).__detectionStripped),
      true
    );
    await toggle.click();
    await menu.waitFor();
    assert.equal(await toggle.getAttribute('aria-expanded'), 'true');
    assert.deepEqual(problems, []);
  });

  it('loads a cover image from the client API origin with the CSP enforced', async (t) => {
    stubApi(t);
    // Pinned absolute cross-origin API URL; the document is rewritten below so the page uses it
    // whatever the environment's clientApiUrl is.
    const apiOrigin = 'http://api.csp-test.example:7130';
    const page = await t.serve(
      await createTestServer(createAppRouter({ production: true, clientApiUrl: apiOrigin }).fetch)
    );
    const violations: string[] = [];
    page.on('console', (message) => {
      if (message.text().includes('Content Security Policy')) violations.push(message.text());
    });
    // The browser never reaches the fake API host; answer its image request here.
    await page.route(`${apiOrigin}/static/images/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'image/png', body: PNG })
    );
    await page.route('**/books/b1', async (route) => {
      const response = await route.fetch();
      const body = (await response.text()).replaceAll(configuredApiUrl, apiOrigin);
      await route.fulfill({ response, body });
    });
    await page.goto('/books/b1');
    const cover = page.getByRole('img', { name: 'Dune' });
    await cover.waitFor();
    await page.waitForLoadState('networkidle');

    assert.equal(await cover.getAttribute('src'), `${apiOrigin}/static/images/books/b1.png`);
    assert.equal(await cover.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0), true);
    assert.deepEqual(violations, []);
  });
});
