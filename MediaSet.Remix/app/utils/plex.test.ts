import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import {
  describeAudioTrack,
  describeScope,
  episodeCode,
  formatDuration,
  formatSize,
  relativeTime,
  seasonLabel,
} from './plex.ts';

describe('plex helpers', () => {
  const now = new Date('2025-06-01T12:00:00Z');

  it('should describe elapsed time in the largest whole unit', () => {
    assert.equal(relativeTime('2025-06-01T11:59:40Z', now), 'just now');
    assert.equal(relativeTime('2025-06-01T11:59:00Z', now), '1 minute ago');
    assert.equal(relativeTime('2025-06-01T09:00:00Z', now), '3 hours ago');
    assert.equal(relativeTime('2025-05-30T12:00:00Z', now), '2 days ago');
    assert.equal(relativeTime('2025-03-01T12:00:00Z', now), '3 months ago');
    assert.equal(relativeTime('2024-06-07T12:00:00Z', now), '11 months ago');
    assert.equal(relativeTime('2024-06-02T12:00:00Z', now), '1 year ago');
    assert.equal(relativeTime('2023-06-01T12:00:00Z', now), '2 years ago');
    assert.equal(relativeTime('not a date', now), 'not a date');
  });

  it('should format duration, size and audio', () => {
    assert.equal(formatDuration(2700), '45m');
    assert.equal(formatDuration(6780), '1h 53m');
    assert.equal(formatDuration(null), '');
    assert.equal(formatSize(850 * 1024 * 1024), '850 MB');
    assert.equal(formatSize(null), '');
    assert.equal(describeAudioTrack({ codec: 'dts', channels: 8, language: 'English' }), '7.1 DTS English');
    assert.equal(describeAudioTrack(undefined), '');
  });
});

describe('plex TV helpers', () => {
  it('should describe scope', () => {
    assert.equal(describeScope({ kind: 'season', from: 2, to: 2 }), 'Season 2');
    assert.equal(describeScope({ kind: 'range', from: 1, to: 3 }), 'Seasons 1-3');
    assert.equal(describeScope({ kind: 'all' }), 'Complete series');
    assert.equal(describeScope(null), '');
  });

  it('should label seasons and episodes', () => {
    assert.equal(seasonLabel(0), 'Specials');
    assert.equal(seasonLabel(3), 'Season 3');
    assert.equal(episodeCode(2, 5), 'S02E05');
  });
});
