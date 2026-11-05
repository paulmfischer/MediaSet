import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { seqEnvironmentName } from './serverLogger.ts';

describe('seqEnvironmentName', () => {
  it('should match the ASP.NET environment names the API sends', () => {
    assert.equal(seqEnvironmentName('development'), 'Development');
    assert.equal(seqEnvironmentName('production'), 'Production');
    assert.equal(seqEnvironmentName('test'), 'Testing');
  });

  it('should default to Development when NODE_ENV is unset or empty', () => {
    assert.equal(seqEnvironmentName(undefined), 'Development');
    assert.equal(seqEnvironmentName(''), 'Development');
  });

  it('should capitalize any other value', () => {
    assert.equal(seqEnvironmentName('staging'), 'Staging');
  });
});

describe('serverLogger Seq events', () => {
  it('should send the mapped Environment, not the raw NODE_ENV', async (t) => {
    const original = {
      fetch: globalThis.fetch,
      enabled: process.env['ExternalLogging__Enabled'],
      url: process.env['ExternalLogging__SeqUrl'],
    };
    const bodies: Array<Record<string, unknown>> = [];
    globalThis.fetch = (async (_input: RequestInfo | URL, init?: RequestInit) => {
      bodies.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
      return new Response(null, { status: 201 });
    }) as typeof fetch;
    process.env['ExternalLogging__Enabled'] = 'true';
    process.env['ExternalLogging__SeqUrl'] = 'http://seq.test';
    t.after(() => {
      globalThis.fetch = original.fetch;
      for (const [name, value] of [
        ['ExternalLogging__Enabled', original.enabled],
        ['ExternalLogging__SeqUrl', original.url],
      ] as const) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
    });

    // The module reads its Seq settings at import time, so load a fresh copy with them set.
    const specifier = './serverLogger.ts?seq-environment';
    const { serverLogger } = (await import(specifier)) as typeof import('./serverLogger.ts');
    serverLogger.info('hello');
    await new Promise((resolve) => setTimeout(resolve, 20));

    assert.equal(bodies.length, 1);
    assert.equal(bodies[0].Environment, seqEnvironmentName(process.env.NODE_ENV));
    assert.notEqual(bodies[0].Environment, process.env.NODE_ENV);
  });
});
