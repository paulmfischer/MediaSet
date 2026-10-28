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

  it('hydrates the mobile menu: opens, then closes on outside click and on navigation', async (t) => {
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

    await page.getByRole('heading', { name: 'Dashboard' }).click();
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

    await page.getByRole('heading', { name: 'Dashboard' }).click();
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
