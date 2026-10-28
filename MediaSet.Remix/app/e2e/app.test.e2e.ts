import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

describe('app shell (e2e)', () => {
  it('loads the home page with the Tailwind stylesheet and the browser runtime, without errors', async (t) => {
    const page = await t.serve(await createTestServer(router.fetch));
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error') errors.push(message.text());
    });

    // The Tailwind config uses the default `media` dark mode strategy.
    await page.emulateMedia({ colorScheme: 'dark' });
    const entryLoaded = page.waitForResponse((response) => response.url().includes('/assets/app/actions/public/entry'));
    await page.goto('/');

    assert.equal(await page.title(), 'Dashboard | MediaSet');
    assert.equal((await entryLoaded).status(), 200);
    const heading = page.getByRole('heading', { name: 'Dashboard' });
    // text-2xl and dark:bg-zinc-800 come from the prebuilt app.css.
    assert.equal(await heading.evaluate((element) => getComputedStyle(element).fontSize), '24px');
    assert.equal(
      await page.locator('body').evaluate((element) => getComputedStyle(element).backgroundColor),
      'rgb(39, 39, 42)'
    );
    await page.waitForLoadState('networkidle');
    assert.deepEqual(errors, []);
  });
});
