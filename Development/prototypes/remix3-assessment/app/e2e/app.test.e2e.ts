import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

describe('app (browser)', () => {
  it('search is a client navigation, shows pending UI, and keeps the DOM shell', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    let errors: string[] = [];
    page.on('pageerror', (e) => errors.push(e.message));
    page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));

    await page.goto('/books');
    // Mark the document: a client-side (frame) navigation keeps it, a full reload would drop it.
    await page.evaluate(() => ((window as any).__marker = 'same-document'));

    await page.getByRole('searchbox').fill('hyp');
    await page.getByRole('button', { name: 'Search' }).click();
    await page.waitForURL('**/books?searchText=hyp');
    await page.getByText('Dune').waitFor({ state: 'detached' }); // list re-rendered with the filtered results

    assert.equal(await page.evaluate(() => (window as any).__marker), 'same-document');
    assert.equal(await page.getByText('Hyperion').count(), 1);
    assert.deepEqual(errors, []);
  });

  it('pending bar becomes visible while the server is slow', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books');
    await page.getByRole('searchbox').fill('a');
    await page.getByRole('button', { name: 'Search' }).click();
    await page.getByRole('progressbar', { name: 'Loading' }).waitFor({ state: 'visible', timeout: 2000 });
    await page.getByRole('progressbar', { name: 'Loading' }).waitFor({ state: 'hidden' });
  });

  it('barcode scanner loads zxing in a client entry and acquires the camera', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    let errors: string[] = [];
    page.on('pageerror', (e) => errors.push(e.message));
    // `remix test` ignores Playwright launchOptions.args, so Chromium's fake-camera flags are unavailable; stub a canvas stream.
    await page.addInitScript(() => {
      navigator.mediaDevices.getUserMedia = async () => {
        let canvas = document.createElement('canvas');
        canvas.width = 320;
        canvas.height = 240;
        canvas.getContext('2d')!.fillRect(0, 0, 320, 240);
        return canvas.captureStream(10);
      };
    });
    await page.goto('/scan');
    await page.getByRole('button', { name: 'Start scanning' }).click();
    await page.locator('#scanner-video').waitFor();
    await page.waitForFunction(
      () => {
        let v = document.querySelector('#scanner-video') as HTMLVideoElement | null;
        return !!v?.srcObject && v.readyState >= 2;
        // First load compiles @zxing/library on demand in the asset server (dev mode).
      },
      undefined,
      { timeout: 20_000 }
    );
    // Closing the scanner removes the <video>; the ref signal must release the camera.
    let tracks = await page.evaluate(() => {
      let v = document.querySelector('#scanner-video') as HTMLVideoElement;
      (window as any).__stream = v.srcObject;
      return (v.srcObject as MediaStream).getTracks().length;
    });
    assert.ok(tracks > 0);
    await page.getByRole('button', { name: 'Stop scanning' }).click();
    await page.waitForFunction(() =>
      ((window as any).__stream as MediaStream).getTracks().every((t) => t.readyState === 'ended')
    );
    assert.deepEqual(errors, []);
  });

  it('mobile menu island hydrates', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 400, height: 800 });
    await page.goto('/');
    await page.getByRole('button', { name: 'Toggle menu' }).click();
    await page.getByRole('link', { name: 'books' }).waitFor();
  });

  it('renders Tailwind styles from the prebuilt stylesheet', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/');
    let size = await page.locator('h1').evaluate((e) => getComputedStyle(e).fontSize);
    assert.equal(size, '30px'); // text-3xl
  });
  it('add form: POST validation errors render in place, success redirects, document is preserved', async (t) => {
    let page = await t.serve(await createTestServer(router.fetch));
    let errors: string[] = [];
    page.on('pageerror', (e) => errors.push(e.message));
    await page.goto('/games/add');
    await page.evaluate(() => ((window as any).__marker = 'same-document'));

    await page.getByRole('button', { name: 'Save' }).click();
    await page.getByText('Title is required').waitFor();
    assert.equal(new URL(page.url()).pathname, '/games/add');

    await page.getByLabel('Title').fill('Celeste');
    await page.getByLabel('fantasy').check();
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/games');
    await page.getByText('Celeste').waitFor();
    assert.equal(await page.evaluate(() => (window as any).__marker), 'same-document');
    assert.deepEqual(errors, []);
  });
});
