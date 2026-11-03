import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { createAppRouter } from '../router.ts';

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
    assert.match(policy, /script-src[^;]*blob:[^;]*'wasm-unsafe-eval'/);
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
});
