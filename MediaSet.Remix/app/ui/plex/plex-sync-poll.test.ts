import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { classifyPoll, MAX_FAILURES, MAX_RETRY_MS, nextDelay, POLL_MS } from './public/plex-sync-poll.ts';

const status = (state: string, enabled = true) => ({ enabled, lastRun: { state } });

describe('plex sync poll decisions', () => {
  it('should keep polling while an enabled type has a running or queued run', () => {
    assert.equal(classifyPoll(200, [status('running')]), 'active');
    assert.equal(classifyPoll(200, [status('succeeded'), status('queued')]), 'active');
  });

  it('should reload when no run is active', () => {
    assert.equal(classifyPoll(200, [status('succeeded')]), 'finished');
    assert.equal(classifyPoll(200, [{ enabled: true, lastRun: null }]), 'finished');
  });

  it('should ignore a stale run on a disabled type', () => {
    assert.equal(classifyPoll(200, [status('running', false)]), 'finished');
  });

  it('should reload when Plex was switched off (404)', () => {
    assert.equal(classifyPoll(404, {}), 'gone');
  });

  it('should treat errors and malformed answers as failures', () => {
    assert.equal(classifyPoll(502, {}), 'failed');
    assert.equal(classifyPoll(200, null), 'failed');
    assert.equal(classifyPoll(200, { not: 'a list' }), 'failed');
  });

  it('should back off after failures and give up after a few in a row', () => {
    assert.equal(nextDelay(0), POLL_MS);
    assert.equal(nextDelay(1), POLL_MS * 2);
    assert.equal(nextDelay(MAX_FAILURES - 1)! <= MAX_RETRY_MS, true);
    assert.equal(nextDelay(MAX_FAILURES), null);
  });
});
