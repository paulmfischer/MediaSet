// Seed only the disposable #675 API, never an existing database.
import { deflateSync } from 'node:zlib';
const base = 'http://127.0.0.1:5100';
function chunk(type, data) {
  const contents = Buffer.concat([Buffer.from(type), data]);
  let crc = 0xffffffff;
  for (const byte of contents) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
  }
  const result = Buffer.alloc(contents.length + 8);
  result.writeUInt32BE(data.length);
  contents.copy(result, 4);
  result.writeUInt32BE((crc ^ 0xffffffff) >>> 0, result.length - 4);
  return result;
}
const header = Buffer.alloc(13);
header.writeUInt32BE(80, 0); header.writeUInt32BE(120, 4); header[8] = 8; header[9] = 2;
const scanlines = Buffer.alloc(120 * (80 * 3 + 1));
for (let y = 0; y < 120; y++) for (let x = 0; x < 80; x++) { const offset = y * 241 + 1 + x * 3; scanlines[offset] = 28; scanlines[offset + 1] = 160; scanlines[offset + 2] = 130; }
const cover = Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', deflateSync(scanlines)), chunk('IEND', Buffer.alloc(0))]);
const health = await (await fetch(`${base}/health`)).json();
if (health.database?.status !== 'up') throw new Error('Disposable API database unavailable');
for (const [type, extra] of Object.entries({
  Books: { authors: ['Ursula Example'], subtitle: 'A collection fixture', pages: 320, publicationDate: '2020', publisher: 'Example Press', isbn: '9780000000002', plot: 'A story for collection routing verification.' },
  Movies: { runtime: 120, isTvSeries: true, studios: ['Example Studio'], releaseDate: '2021', plot: 'A film for collection routing verification.' },
  Games: { platform: 'PC', developers: ['Example Developer'], publishers: ['Example Publisher'], rating: 'E', description: 'A game for collection routing verification.' },
  Musics: { artist: 'Example Artist', duration: 185000, label: 'Example Records', tracks: 1, discs: 1, discList: [{ trackNumber: 1, title: 'Example Song', duration: 185000 }] },
})) {
  const existing = await (await fetch(`${base}/${type}/pagedsearch?searchText=&orderBy=title:asc&page=1&pageSize=1`)).json();
  if (existing.totalCount !== 0) throw new Error('Use a fresh, empty disposable database. Existing records will not be changed.');
  for (let i = 1; i <= 78; i++) {
    const entity = { type, title: `${type} Fixture ${String(i).padStart(3, '0')}`, format: type === 'Books' ? 'Hardcover' : type === 'Musics' ? 'CD' : 'Disc', genres: ['Adventure'], ...extra };
    const form = new FormData();
    form.set('entity', JSON.stringify(entity));
    if (i === 1 || i === 78) {
      form.set('coverImage', new Blob([cover], { type: 'image/png' }), 'fixture.png');
    }
    const response = await fetch(`${base}/${type}`, { method: 'POST', body: form });
    if (!response.ok) throw new Error(`${type} seed failed: ${response.status}`);
  }
  console.log(`${type}: seeded 78 disposable records`);
}
