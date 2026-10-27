import { test } from 'node:test';
import assert from 'node:assert/strict';
import { deflateSync, crc32 } from 'node:zlib';

const ui: string = 'http://127.0.0.1:31654';
const api: string = 'http://127.0.0.1:51654';

test('should preserve book CRUD, multipart image and error contracts through Remix 3 SSR', async () => {
  let id: string | undefined;
  const title: string = `Assessment ${Date.now()}`;
  try {
    const invalid: Response = await fetch(`${ui}/books/add`, {
      method: 'POST',
      body: new URLSearchParams({ title: '' }),
    });
    assert.equal(invalid.status, 400);
    assert.match(await invalid.text(), /Title is required/);
    const oversized = new FormData();
    oversized.set('title', title);
    oversized.set('coverImage', new Blob([new Uint8Array(5 * 1024 * 1024 + 1)], { type: 'image/png' }), 'large.png');
    const rejected: Response = await fetch(`${ui}/books/add`, {
      method: 'POST',
      body: oversized,
    });
    assert.equal(rejected.status, 413);
    const body = new FormData();
    body.set('title', title);
    body.set('authors', 'Disposable Author');
    // Valid 1x1 PNG exercises the existing API's multipart image handling.
    function chunk(type: string, data: Buffer): Buffer {
      const length = Buffer.alloc(4);
      length.writeUInt32BE(data.length);
      const bytes = Buffer.concat([Buffer.from(type), data]);
      const checksum = Buffer.alloc(4);
      checksum.writeUInt32BE(crc32(bytes));
      return Buffer.concat([length, bytes, checksum]);
    }
    const header = Buffer.alloc(13);
    header.writeUInt32BE(1, 0);
    header.writeUInt32BE(1, 4);
    header[8] = 8;
    header[9] = 6;
    const png = Buffer.concat([
      Buffer.from('89504e470d0a1a0a', 'hex'),
      chunk('IHDR', header),
      chunk('IDAT', deflateSync(Buffer.from([0, 255, 0, 0, 255]))),
      chunk('IEND', Buffer.alloc(0)),
    ]);
    body.set('coverImage', new Blob([png], { type: 'image/png' }), 'cover.png');
    const created: Response = await fetch(`${ui}/books/add`, {
      method: 'POST',
      body,
      redirect: 'manual',
    });
    assert.equal(created.status, 303);
    const location: string = created.headers.get('location')!;
    id = location.split('/').at(-1);
    assert.match(id!, /^[a-f0-9]{24}$/);
    const detail: Response = await fetch(`${ui}${location}`);
    assert.equal(detail.status, 200);
    const html: string = await detail.text();
    assert.match(html, new RegExp(title));
    assert.match(html, /Hydration probe: 0/);
    assert.match(html, /type="importmap"/);
    const saved = (await (await fetch(`${api}/Books/${id}`)).json()) as {
      coverImage?: { filePath: string };
    };
    assert.ok(saved.coverImage?.filePath, 'API must persist uploaded image');
    const edited: Response = await fetch(`${ui}/books/${id}/edit`, {
      method: 'POST',
      body: new URLSearchParams({
        title: `${title} edited`,
        authors: 'Changed Author',
      }),
      redirect: 'manual',
    });
    assert.equal(edited.status, 303);
    const list: string = await (await fetch(`${ui}/books?searchText=${encodeURIComponent(title)}`)).text();
    assert.match(list, /edited/);
    assert.match(list, /Changed Author/);
    const missing: Response = await fetch(`${ui}/books/000000000000000000000000`);
    assert.equal(missing.status, 404);
    assert.match(await missing.text(), /Book not found/);
    console.info('Verified: SSR list/detail, validation400, API404, add/edit303, persisted cover image, search');
  } finally {
    if (id) assert.equal((await fetch(`${api}/Books/${id}`, { method: 'DELETE' })).status, 200);
  }
});
