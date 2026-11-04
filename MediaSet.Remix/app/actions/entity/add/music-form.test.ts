import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { parseTrackRows, readTrackRows, toMusicValues, toTrackRow } from './music-form.ts';

const row = (trackNumber: string, title: string, minutes = '', seconds = '') => ({
  trackNumber,
  title,
  minutes,
  seconds,
});

describe('music track rows', () => {
  it('should pair the repeated fields by position, padding short columns', () => {
    const data = new FormData();
    for (const [name, value] of [
      ['trackNumber', '1'],
      ['trackNumber', '2'],
      ['trackTitle', 'One'],
      ['trackMinutes', '3'],
    ] as const) {
      data.append(name, value);
    }

    assert.deepEqual(readTrackRows(data), [row('1', 'One', '3'), row('2', '')]);
  });

  it('should drop fully blank rows and convert durations to milliseconds', () => {
    const { discs, errors } = parseTrackRows([row('', ' '), row('3', ' Song ', '1', '5'), row('4', 'No time')]);

    assert.deepEqual(errors, {});
    assert.deepEqual(discs, [
      { trackNumber: 3, title: 'Song', duration: 65000 },
      { trackNumber: 4, title: 'No time', duration: null },
    ]);
  });

  it('should key errors by the row position as submitted', () => {
    const { discs, errors } = parseTrackRows([
      row('', ''),
      row('0', '', '10000', '60'),
      row('1.5', 'x'),
      row('1000000', 'x'),
      row('', 'x'),
    ]);

    assert.deepEqual(discs, []);
    assert.deepEqual(Object.keys(errors).sort(), [
      'track.2.trackMinutes',
      'track.2.trackNumber',
      'track.2.trackSeconds',
      'track.2.trackTitle',
      'track.3.trackNumber',
      'track.4.trackNumber',
      'track.5.trackNumber',
    ]);
    assert.equal(errors['track.5.trackNumber'], 'Track number is required');
  });

  it('should accept the limits of each field', () => {
    const { discs, errors } = parseTrackRows([row('999999', 'Max', '9999', '59'), row('1', 'Min', '0', '0')]);

    assert.deepEqual(errors, {});
    assert.deepEqual(
      discs.map((disc) => disc.duration),
      [(9999 * 60 + 59) * 1000, 0]
    );
  });

  it('should show saved tracks as editor rows, with whole-second durations', () => {
    assert.deepEqual(toTrackRow({ trackNumber: 2, title: 'B', duration: 125900 }), row('2', 'B', '2', '5'));
    assert.deepEqual(toTrackRow({ trackNumber: 3, title: 'C', duration: null }), row('3', 'C'));
    assert.deepEqual(toMusicValues({ type: 'Musics', title: 'T', artist: 'A', genres: [] } as never).trackList, []);
  });
});
