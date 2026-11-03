import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it, type TestContext } from 'remix/test';

import { createAppRouter } from '../router.ts';

const ISBN = '9780441172719';

type CameraMode = 'ok' | 'denied' | 'none' | 'unsupported';

interface CameraStub {
  streams: MediaStream[];
  requests: unknown[];
}

// Replaces the camera before the page loads. `ok` hands out a real MediaStream captured from a canvas that shows
// an EAN-13 barcode, so the video preview and both decoders (a stubbed native BarcodeDetector, or the real zxing
// library) see genuine frames; its tracks are real, so `readyState` shows whether they were stopped.
function installCamera([mode, native, code]: readonly [CameraMode, boolean, string]): void {
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

  const stub: CameraStub = { streams: [], requests: [] };
  (window as unknown as { __camera: CameraStub }).__camera = stub;

  if (mode === 'unsupported') {
    Object.defineProperty(navigator, 'mediaDevices', { value: undefined, configurable: true });
    return;
  }
  navigator.mediaDevices.getUserMedia = async (constraints?: MediaStreamConstraints) => {
    stub.requests.push(constraints);
    if (mode === 'denied') throw new DOMException('Permission denied', 'NotAllowedError');
    if (mode === 'none') throw new DOMException('Requested device not found', 'NotFoundError');
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

  if (native) {
    (window as unknown as { BarcodeDetector: unknown }).BarcodeDetector = class {
      static async getSupportedFormats() {
        return ['ean_13', 'upc_a', 'upc_e', 'ean_8', 'code_128'];
      }
      async detect() {
        return [{ rawValue: code }];
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
async function openForm(t: TestContext, path: string, mode: CameraMode, options: { native?: boolean } = {}) {
  const lookups = stubApi(t);
  const page = await t.serve(await createTestServer(createAppRouter({ production: true }).fetch));
  const problems: string[] = [];
  const requests: string[] = [];
  page.on('pageerror', (error) => problems.push(error.message));
  page.on('console', (message) => {
    if (message.text().includes('Content Security Policy')) problems.push(message.text());
  });
  page.on('request', (request) => requests.push(new URL(request.url()).pathname));
  await page.addInitScript(installCamera, [mode, options.native ?? true, ISBN] as const);
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

describe('barcode scanner (e2e, production CSP, stubbed camera)', () => {
  it('should open with a live preview, then fill the ISBN and run the lookup on detection with the native detector', async (t) => {
    const { page, lookups, problems, requests, camera } = await openForm(t, '/books/add', 'ok');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    const lookupDialog = page.getByRole('dialog', { name: 'Look up book' });
    await lookupDialog.waitFor();

    assert.equal(await page.locator('#isbn').inputValue(), ISBN);
    assert.equal(lookups.length, 1);
    assert.match(lookups[0] ?? '', new RegExp(ISBN));
    assert.equal(await lookupDialog.getByRole('listitem').count(), 1);
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
    const { page, lookups, problems, requests, camera } = await openForm(t, '/books/add', 'ok', { native: false });
    assert.equal(
      requests.some((path) => path.includes('zxing')),
      false
    );

    await page.getByRole('button', { name: 'Scan ISBN' }).click();
    await page.getByRole('dialog', { name: 'Look up book' }).waitFor({ timeout: 15_000 });

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
    const { page, lookups, camera } = await openForm(t, '/books/add', 'ok', { native: false });
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
    assert.equal(await page.getByRole('button', { name: 'Look up by ISBN' }).count(), 1);
  });
});
