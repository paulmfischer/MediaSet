import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it, type TestContext } from 'remix/test';
import type { Page } from 'playwright';

import { createAppRouter } from '../router.ts';

const ISBN = '9780441172719';

// `pending` leaves the permission prompt unanswered until the test calls `releasePending()`.
type CameraMode = 'ok' | 'denied' | 'none' | 'unsupported' | 'pending';
// The stubbed BarcodeDetector: absent, finds the code at once, finds it only on the 4th frame, or throws every time.
type NativeMode = 'absent' | 'instant' | 'slow' | 'broken';

interface CameraStub {
  streams: MediaStream[];
  requests: unknown[];
  detectCalls: number;
  releasePending?: () => void;
}

// Replaces the camera before the page loads. `ok` hands out a real MediaStream captured from a canvas that shows
// an EAN-13 barcode, so the video preview and both decoders (a stubbed native BarcodeDetector, or the real zxing
// library) see genuine frames; its tracks are real, so `readyState` shows whether they were stopped.
function installCamera([mode, native, code]: readonly [CameraMode, NativeMode, string]): void {
  const L = [
    '0001101',
    '0011001',
    '0010011',
    '0111101',
    '0100011',
    '0110001',
    '0101111',
    '0111011',
    '0110111',
    '0001011',
  ];
  const G = [
    '0100111',
    '0110011',
    '0011011',
    '0100001',
    '0011101',
    '0111001',
    '0000101',
    '0010001',
    '0001001',
    '0010111',
  ];
  const R = [
    '1110010',
    '1100110',
    '1101100',
    '1000010',
    '1011100',
    '1001110',
    '1010000',
    '1000100',
    '1001000',
    '1110100',
  ];
  const parity = ['LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG', 'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL'];
  const digits = [...code].map(Number);
  const pattern = parity[digits[0]!]!;
  let bits = '101';
  for (let i = 0; i < 6; i += 1) bits += (pattern[i] === 'L' ? L : G)[digits[i + 1]!]!;
  bits += '01010';
  for (let i = 0; i < 6; i += 1) bits += R[digits[i + 7]!]!;
  bits += '101';

  // A phone or tablet: the scan button is only offered on touch devices.
  Object.defineProperty(navigator, 'maxTouchPoints', { value: 5, configurable: true });

  const stub: CameraStub = { streams: [], requests: [], detectCalls: 0 };
  (window as unknown as { __camera: CameraStub }).__camera = stub;

  if (mode === 'unsupported') {
    Object.defineProperty(navigator, 'mediaDevices', { value: undefined, configurable: true });
    return;
  }
  navigator.mediaDevices.getUserMedia = async (constraints?: MediaStreamConstraints) => {
    stub.requests.push(constraints);
    if (mode === 'denied') throw new DOMException('Permission denied', 'NotAllowedError');
    if (mode === 'none') throw new DOMException('Requested device not found', 'NotFoundError');
    if (mode === 'pending') await new Promise<void>((resolve) => (stub.releasePending = resolve));
    const canvas = document.createElement('canvas');
    canvas.width = 640;
    canvas.height = 480;
    const context = canvas.getContext('2d')!;
    const draw = () => {
      context.fillStyle = '#fff';
      context.fillRect(0, 0, 640, 480);
      context.fillStyle = '#000';
      const module = 4;
      const left = (640 - bits.length * module) / 2;
      for (let i = 0; i < bits.length; i += 1) {
        if (bits[i] === '1') context.fillRect(left + i * module, 140, module, 200);
      }
    };
    draw();
    const stream = canvas.captureStream(15);
    // A captured canvas only emits frames when it changes, so keep repainting while the camera is live.
    const timer = setInterval(() => {
      if (stream.getVideoTracks().every((track) => track.readyState === 'ended')) clearInterval(timer);
      else draw();
    }, 100);
    stub.streams.push(stream);
    return stream;
  };

  if (native !== 'absent') {
    (window as unknown as { BarcodeDetector: unknown }).BarcodeDetector = class {
      static async getSupportedFormats() {
        return ['ean_13', 'upc_a', 'upc_e', 'ean_8', 'code_128'];
      }
      async detect() {
        stub.detectCalls += 1;
        if (native === 'broken') throw new Error('detector is not supported');
        return native === 'slow' && stub.detectCalls < 4 ? [] : [{ rawValue: code }];
      }
    };
  } else {
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
  }
}

// Serves the book form with ISBN lookup available, and records the lookups it runs.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const lookups: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.endsWith('/config/lookup-capabilities')) return Response.json({ supportsBookLookup: true });
    if (url.pathname.includes('/lookup/Books/')) {
      lookups.push(url.pathname + url.search);
      return Response.json([{ title: 'Dune', authors: [{ name: 'Frank Herbert', url: '' }], publishDate: '1990' }]);
    }
    if (url.pathname.endsWith('/Books/b1')) return Response.json({ id: 'b1', type: 'Books', title: 'Dune' });
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return lookups;
}

// Opens a page on the production-CSP app with the camera stubbed; collects CSP violations and page errors.
async function openForm(t: TestContext, path: string, mode: CameraMode, options: { native?: NativeMode } = {}) {
  const lookups = stubApi(t);
  const page = await t.serve(await createTestServer(createAppRouter({ production: true }).fetch));
  const problems: string[] = [];
  const requests: string[] = [];
  page.on('pageerror', (error) => problems.push(error.message));
  page.on('console', (message) => {
    if (message.text().includes('Content Security Policy')) problems.push(message.text());
  });
  page.on('request', (request) => requests.push(new URL(request.url()).pathname));
  await page.addInitScript(installCamera, [mode, options.native ?? 'instant', ISBN] as const);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  const camera = {
    tracksEnded: () =>
      page.evaluate(() =>
        (window as unknown as { __camera: CameraStub }).__camera.streams.flatMap((stream) =>
          stream.getTracks().map((track) => track.readyState === 'ended')
        )
      ),
    requestCount: () => page.evaluate(() => (window as unknown as { __camera: CameraStub }).__camera.requests.length),
    lastConstraints: () =>
      page.evaluate(() => (window as unknown as { __camera: CameraStub }).__camera.requests.at(-1)),
  };
  return { page, lookups, problems, requests, camera };
}

// Waits for the scanned ISBN's single match to fill the form (title from the stubbed lookup).
const filled = (page: Page, timeout = 5000) =>
  page.waitForFunction(() => document.querySelector<HTMLInputElement>('#title')?.value === 'Dune', undefined, {
    timeout,
  });

describe('barcode scanner (e2e, production CSP, stubbed camera)', () => {
  it('should open with a live preview, then fill the ISBN and run the lookup on detection with the native detector', async (t) => {
    const { page, lookups, problems, requests, camera } = await openForm(t, '/books/add', 'ok');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await filled(page);

    assert.equal(await page.locator('#isbn').inputValue(), ISBN);
    assert.equal(lookups.length, 1);
    assert.match(lookups[0] ?? '', new RegExp(ISBN));
    // The ISBN has a single match, so the form is filled and there is no dialog to choose from.
    assert.equal(await page.getByRole('dialog', { name: 'Look up book' }).count(), 0);
    // The existing lookup flow ran in the same document, and the scanner is gone with the camera released.
    assert.equal(await page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument), true);
    assert.equal(await page.getByRole('dialog', { name: 'Scan ISBN' }).isVisible(), false);
    assert.deepEqual(await camera.lastConstraints(), { video: { facingMode: 'environment' }, audio: false });
    assert.deepEqual(await camera.tracksEnded(), [true]);
    // The native detector was enough: the decoder library was never downloaded.
    assert.equal(
      requests.some((path) => path.includes('zxing')),
      false
    );
    assert.deepEqual(problems, []);
  });

  it('should load the zxing decoder only when scanning and decode a real frame when there is no native detector', async (t) => {
    const { page, lookups, problems, requests, camera } = await openForm(t, '/books/add', 'ok', { native: 'absent' });
    assert.equal(
      requests.some((path) => path.includes('zxing')),
      false
    );

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await filled(page, 15_000);

    assert.equal(
      requests.some((path) => path.includes('zxing')),
      true
    );
    assert.equal(await page.locator('#isbn').inputValue(), ISBN);
    assert.equal(lookups.length, 1);
    assert.deepEqual(await camera.tracksEnded(), [true]);
    assert.deepEqual(problems, []);
  });

  it('should release the camera on Cancel and on Escape', async (t) => {
    // A detector that finds nothing keeps the scanner open.
    const { page, lookups, camera } = await openForm(t, '/books/add', 'ok', { native: 'absent' });
    await page.evaluate(() => {
      (window as unknown as { BarcodeDetector: unknown }).BarcodeDetector = class {
        async detect() {
          return [];
        }
      };
    });
    const scan = page.getByRole('button', { name: 'Scan ISBN' });
    const scanner = page.getByRole('dialog', { name: 'Scan ISBN' });

    await scan.click();
    await scanner.waitFor();
    assert.equal(await page.evaluate(() => document.querySelector('dialog')?.matches(':modal')), true);
    await page.waitForFunction(() => document.querySelector('video')?.srcObject != null);
    assert.deepEqual(await camera.tracksEnded(), [false]);

    await scanner.getByRole('button', { name: 'Cancel' }).click();
    await scanner.waitFor({ state: 'hidden' });
    assert.deepEqual(await camera.tracksEnded(), [true]);
    await page.waitForFunction(() => document.activeElement?.getAttribute('data-scan-field') === 'isbn');

    // Scanning again asks for a new stream; Escape closes it and releases that one too.
    await scan.click();
    await scanner.waitFor();
    await page.waitForFunction(() => document.querySelector('video')?.srcObject != null);
    assert.deepEqual(await camera.tracksEnded(), [true, false]);
    await page.keyboard.press('Escape');
    await scanner.waitFor({ state: 'hidden' });
    // Escape closes the dialog natively; its close event stops the tracks a moment later.
    await page.waitForFunction(() =>
      (window as unknown as { __camera: CameraStub }).__camera.streams.every((stream) =>
        stream.getTracks().every((track) => track.readyState === 'ended')
      )
    );
    assert.deepEqual(await camera.tracksEnded(), [true, true]);
    assert.equal(lookups.length, 0);
    assert.equal(await page.locator('#isbn').inputValue(), '');
  });

  it('should keep scanning frames until the native detector finds the code', async (t) => {
    const { page, lookups } = await openForm(t, '/books/add', 'ok', { native: 'slow' });

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await filled(page);

    assert.equal(await page.locator('#isbn').inputValue(), ISBN);
    assert.equal(lookups.length, 1);
    const detectCalls = await page.evaluate(() => (window as unknown as { __camera: CameraStub }).__camera.detectCalls);
    assert.equal(detectCalls >= 4, true);
  });

  it('should fall back to the zxing decoder when the native detector keeps failing', async (t) => {
    const { page, requests, camera } = await openForm(t, '/books/add', 'ok', { native: 'broken' });
    assert.equal(
      requests.some((path) => path.includes('zxing')),
      false
    );

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await filled(page, 15_000);

    assert.equal(
      requests.some((path) => path.includes('zxing')),
      true
    );
    assert.equal(await page.locator('#isbn').inputValue(), ISBN);
    assert.deepEqual(await camera.tracksEnded(), [true]);
  });

  it('should not keep the camera when the dialog is closed while the permission prompt is open', async (t) => {
    const { page, camera } = await openForm(t, '/books/add', 'pending');

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await page.getByRole('dialog', { name: 'Scan ISBN' }).waitFor();
    await page.waitForFunction(() => (window as unknown as { __camera: CameraStub }).__camera.requests.length === 1);
    await page.getByRole('button', { name: 'Cancel' }).click();
    assert.deepEqual(await camera.tracksEnded(), []);

    // The user allows the camera after the dialog is gone: the stream that arrives must be stopped at once.
    await page.evaluate(() => (window as unknown as { __camera: CameraStub }).__camera.releasePending?.());
    await page.waitForFunction(() =>
      (window as unknown as { __camera: CameraStub }).__camera.streams.every((stream) =>
        stream.getTracks().every((track) => track.readyState === 'ended')
      )
    );
    assert.deepEqual(await camera.tracksEnded(), [true]);
    assert.equal(await page.getByRole('dialog', { name: 'Scan ISBN' }).isVisible(), false);
  });

  it('should release the camera when the form is replaced while the scanner is open', async (t) => {
    // A detector that finds nothing keeps the scanner open while the form is re-rendered underneath it.
    const { page, camera } = await openForm(t, '/books/add', 'ok', { native: 'absent' });
    await page.evaluate(() => {
      (window as unknown as { BarcodeDetector: unknown }).BarcodeDetector = class {
        async detect() {
          return [];
        }
      };
    });
    await page.locator('#title').fill('Dune');
    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await page.getByRole('dialog', { name: 'Scan ISBN' }).waitFor();
    await page.waitForFunction(() => document.querySelector('video')?.srcObject != null);
    assert.deepEqual(await camera.tracksEnded(), [false]);

    // Run a title lookup and apply its result: the form renders fresh (new key) and its islands are removed.
    await page.evaluate(() => {
      const form = document.querySelector('form')!;
      form.requestSubmit(form.querySelector<HTMLButtonElement>('button[value="lookup:title"]')!);
    });
    const results = page.getByRole('dialog', { name: 'Look up book' });
    await results.waitFor();
    await results.getByRole('button', { name: 'Use this result' }).click();
    await results.waitFor({ state: 'detached' });

    await page.waitForFunction(() =>
      (window as unknown as { __camera: CameraStub }).__camera.streams.every((stream) =>
        stream.getTracks().every((track) => track.readyState === 'ended')
      )
    );
    assert.deepEqual(await camera.tracksEnded(), [true]);
  });

  it('should close on a backdrop click but not on a click inside the dialog', async (t) => {
    const { page } = await openForm(t, '/books/add', 'ok', { native: 'absent' });
    await page.evaluate(() => {
      (window as unknown as { BarcodeDetector: unknown }).BarcodeDetector = class {
        async detect() {
          return [];
        }
      };
    });
    const scanner = page.getByRole('dialog', { name: 'Scan ISBN' });
    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await scanner.waitFor();

    // The dialog's edge area is padding inside the content wrapper, not a backdrop.
    const box = (await scanner.boundingBox())!;
    await page.mouse.click(box.x + 2, box.y + 2);
    assert.equal(await scanner.isVisible(), true);

    await page.mouse.click(2, 2);
    await scanner.waitFor({ state: 'hidden' });
  });

  it('should show a clear error when camera permission is denied', async (t) => {
    const { page, camera } = await openForm(t, '/books/add', 'denied');

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    const alert = page.getByRole('dialog', { name: 'Scan ISBN' }).getByRole('alert');
    await alert.waitFor();
    assert.match(await alert.innerText(), /Camera access was denied/);
    assert.equal(await camera.requestCount(), 1);

    await page.getByRole('button', { name: 'Cancel' }).click();
    await alert.waitFor({ state: 'hidden' });
    // Opening again retries and shows the error again.
    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await alert.waitFor();
    assert.equal(await camera.requestCount(), 2);
  });

  it('should show a clear error when the device has no camera', async (t) => {
    const { page } = await openForm(t, '/books/b1/edit', 'none');

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    const alert = page.getByRole('dialog', { name: 'Scan ISBN' }).getByRole('alert');
    await alert.waitFor();
    assert.match(await alert.innerText(), /No camera was found/);
  });

  it('should not offer the scan button when the browser has no camera API', async (t) => {
    const { page } = await openForm(t, '/books/add', 'unsupported');

    assert.equal(await page.getByRole('button', { name: 'Scan ISBN' }).count(), 0);
    // The plain lookup button is still there.
    assert.equal(await page.getByRole('button', { name: 'Search by ISBN' }).count(), 1);
  });

  it('should offer the scan button only on a touch device that is narrow or a phone/tablet', async (t) => {
    const { page } = await openForm(t, '/books/add', 'ok');
    const scan = page.getByRole('button', { name: 'Scan ISBN' });
    await scan.waitFor();

    // A desktop-sized window hides it, and narrowing the window brings it back.
    await page.setViewportSize({ width: 1280, height: 800 });
    await scan.waitFor({ state: 'detached' });
    await page.setViewportSize({ width: 390, height: 844 });
    await scan.waitFor();

    // No touch hardware, however narrow the window, means no scan button.
    await page.evaluate(() => Object.defineProperty(navigator, 'maxTouchPoints', { value: 0, configurable: true }));
    await page.setViewportSize({ width: 380, height: 844 });
    await scan.waitFor({ state: 'detached' });
  });
});
