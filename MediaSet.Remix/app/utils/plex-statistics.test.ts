import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { formatBytes, formatDuration, statisticLabel } from './plex-statistics.ts';

describe('formatBytes', () => {
  it('should show zero when there is no size', () => {
    assert.equal(formatBytes(0), '0 B');
    assert.equal(formatBytes(Number.NaN), '0 B');
  });

  it('should pick the largest fitting decimal unit', () => {
    assert.equal(formatBytes(512), '512 B');
    assert.equal(formatBytes(4_500_000_000), '4.5 GB');
    assert.equal(formatBytes(12_300_000_000_000), '12.3 TB');
  });
});

describe('formatDuration', () => {
  it('should show zero minutes under a minute', () => {
    assert.equal(formatDuration(0), '0m');
    assert.equal(formatDuration(59), '0m');
  });

  it('should show the two largest units', () => {
    assert.equal(formatDuration(45 * 60), '45m');
    assert.equal(formatDuration(6720), '1h 52m');
    assert.equal(formatDuration(2 * 86400 + 4 * 3600 + 30 * 60), '2d 4h');
  });
});

describe('statisticLabel', () => {
  it('should name a missing value Unknown and upper-case on request', () => {
    assert.equal(statisticLabel('unknown', true), 'Unknown');
    assert.equal(statisticLabel('hevc', true), 'HEVC');
    assert.equal(statisticLabel('Stereo'), 'Stereo');
  });
});
