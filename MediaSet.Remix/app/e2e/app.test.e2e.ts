import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

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

// A heading that is always on the empty home page.
const HOME_HEADING = { name: 'No media items yet' };

describe('app shell (e2e)', () => {
  it('loads the home page with the Tailwind stylesheet and the browser runtime, without errors', async (t) => {
    stubApi(t);
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
    const heading = page.getByRole('heading', HOME_HEADING);
    // text-xl and dark:bg-zinc-800 (the Tailwind 4 oklch palette) come from the prebuilt app.css.
    assert.equal(await heading.evaluate((element) => getComputedStyle(element).fontSize), '20px');
    assert.equal(
      await page.locator('body').evaluate((element) => getComputedStyle(element).backgroundColor),
      'oklch(0.274 0.006 286.033)'
    );
    await page.waitForLoadState('networkidle');
    assert.deepEqual(errors, []);
  });

  it('hydrates the mobile menu: opens, then closes on outside click and on navigation', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const toggle = page.getByRole('button', { name: 'Toggle menu' });
    const menu = page.getByRole('navigation', { name: 'Mobile' });
    assert.equal(await toggle.getAttribute('aria-expanded'), 'false');
    assert.equal(await menu.count(), 0);

    // The desktop nav is hidden at this width.
    assert.equal(await page.getByRole('navigation', { name: 'Main' }).isVisible(), false);

    await toggle.click();
    await menu.waitFor();
    assert.equal(await toggle.getAttribute('aria-expanded'), 'true');
    assert.deepEqual(await menu.getByRole('link').allTextContents(), [
      'Home',
      'Books',
      'Movies',
      'Games',
      'Music',
      'Image Statistics',
    ]);

    await page.getByRole('heading', HOME_HEADING).click();
    await menu.waitFor({ state: 'detached' });
    assert.equal(await toggle.getAttribute('aria-expanded'), 'false');

    // Navigating closes it without a full document load.
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    await toggle.click();
    await menu.waitFor();
    await menu.getByRole('link', { name: 'Home' }).click();
    await menu.waitFor({ state: 'detached' });
    assert.equal(await page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument), true);
  });

  it('hydrates the desktop tools dropdown: closes on outside click, Escape (refocusing the toggle) and navigation', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const tools = page.getByRole('button', { name: 'Tools' });
    const statistics = page.getByRole('link', { name: 'Image Statistics' });
    assert.equal(await statistics.count(), 0);

    await tools.click();
    await statistics.waitFor();
    assert.equal(await tools.getAttribute('aria-expanded'), 'true');

    await page.getByRole('heading', HOME_HEADING).click();
    await statistics.waitFor({ state: 'detached' });
    assert.equal(await tools.getAttribute('aria-expanded'), 'false');

    await tools.click();
    await statistics.waitFor();
    await page.keyboard.press('Escape');
    await statistics.waitFor({ state: 'detached' });
    assert.equal(await tools.evaluate((element) => element === document.activeElement), true);

    // A client navigation closes it without any pointer event.
    await tools.click();
    await statistics.waitFor();
    // element.click() fires no pointer events, so only the navigation can close the menu.
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
      document.querySelector<HTMLElement>('nav[aria-label="Main"] a[href="/"]')?.click();
    });
    await statistics.waitFor({ state: 'detached' });
    assert.equal(await page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument), true);
  });

  it('closes the mobile menu on Escape and returns focus to the toggle', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const toggle = page.getByRole('button', { name: 'Toggle menu' });
    await toggle.click();
    await page.getByRole('navigation', { name: 'Mobile' }).waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('navigation', { name: 'Mobile' }).waitFor({ state: 'detached' });
    assert.equal(await toggle.evaluate((element) => element === document.activeElement), true);
  });

  it('shows the pending indicator while a client navigation is in flight', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });

    // Hold the navigation response until the indicator has been checked.
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => (release = resolve));
    const handler = async (route: import('playwright').Route) => {
      await gate;
      // The route is already handled if the test failed and unrouted first.
      await route.continue().catch(() => {});
    };
    // Only the navigation request to the document root is held; assets and other requests pass.
    const isNavigationTarget = (url: URL) => url.pathname === '/';
    await page.route(isNavigationTarget, handler);

    try {
      const status = () => page.evaluate(() => document.querySelector('[role=status]')?.textContent);
      assert.equal(await status(), '');

      await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Home' }).click();
      await page.waitForFunction(() => document.querySelector('[role=status]')?.textContent === 'Loading');
      release();
      await page.waitForFunction(() => document.querySelector('[role=status]')?.textContent === '');
      assert.equal(await page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument), true);
    } finally {
      release();
      await page.unroute(isNavigationTarget, handler);
    }
  });
});
