import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { isPlexEnabledForRequest, isPlexNavEnabled, plexNavGate } from './plex-nav.server.ts';

// Runs the gate for a path against a stubbed API; `plexFor` answers the integrations call per request path.
function stub(t: { after: (fn: () => void) => void }, plexFor: (path: string) => boolean, delay = 0) {
  const original = globalThis.fetch;
  const state = { lookups: 0 };
  globalThis.fetch = (async () => {
    state.lookups++;
    const current = state.lookups;
    if (delay) await new Promise((resolve) => setTimeout(resolve, delay));
    return Response.json([
      { key: 'plex', displayName: 'Plex', enabled: plexFor(String(current)), entityTypes: ['movies'] },
    ]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return state;
}

const run = (path: string, observe: () => Promise<unknown> | unknown) => {
  const gate = plexNavGate();
  const context = { url: new URL(path, 'http://localhost') } as Parameters<typeof gate>[0];
  return gate(context, async () => new Response(String(await observe())));
};

describe('plexNavGate', () => {
  it('should skip asset and config requests without calling the API', async (t) => {
    const state = stub(t, () => true);
    await run('/assets/app.js', () => isPlexNavEnabled());
    await run('/config.json', () => isPlexNavEnabled());
    assert.equal(state.lookups, 0);
  });

  it('should look the gate up once per request and share it with the handlers', async (t) => {
    const state = stub(t, () => true);
    const response = await run('/plex', async () => [isPlexNavEnabled(), await isPlexEnabledForRequest()].join());
    assert.equal(await response.text(), 'true,true');
    assert.equal(state.lookups, 1);
  });

  it('should be off outside a request and when Plex is off', async (t) => {
    stub(t, () => false);
    assert.equal(isPlexNavEnabled(), false);
    const response = await run('/plex', () => isPlexNavEnabled());
    assert.equal(await response.text(), 'false');
  });

  it('should keep concurrent requests apart', async (t) => {
    // The first lookup answers "on", the second "off", each after a delay so the requests overlap.
    stub(t, (call) => call === '1', 20);
    const seen = async () => {
      await new Promise((resolve) => setTimeout(resolve, 40));
      return isPlexNavEnabled();
    };
    const [first, second] = await Promise.all([run('/a', seen), run('/b', seen)]);
    assert.deepEqual([await first.text(), await second.text()], ['true', 'false']);
  });
});
