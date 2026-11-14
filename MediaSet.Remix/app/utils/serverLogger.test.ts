import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { buildSeqEvent, renderMessageTemplate, seqEnvironmentName, seqRequestHeaders } from './serverLogger.ts';

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

  it('should give one spelling whatever the casing of NODE_ENV', () => {
    for (const value of ['production', 'Production', 'PRODUCTION', ' production ']) {
      assert.equal(seqEnvironmentName(value), 'Production');
    }
    for (const value of ['development', 'Development']) {
      assert.equal(seqEnvironmentName(value), 'Development');
    }
  });
});

describe('seqRequestHeaders', () => {
  it('should send the X-Seq-ApiKey header when a key is set', () => {
    assert.deepEqual(seqRequestHeaders('KEY'), {
      'Content-Type': 'application/vnd.serilog.clef',
      'X-Seq-ApiKey': 'KEY',
    });
  });

  it('should send no X-Seq-ApiKey header when the key is missing or blank', () => {
    for (const key of [undefined, '', '   ']) {
      assert.deepEqual(seqRequestHeaders(key), { 'Content-Type': 'application/vnd.serilog.clef' });
    }
  });
});

describe('renderMessageTemplate', () => {
  it('should fill each hole from the property of the same name', () => {
    assert.equal(
      renderMessageTemplate('Fetched {count} of {totalCount} {entityType}', {
        count: 2,
        totalCount: 9,
        entityType: 'books',
      }),
      'Fetched 2 of 9 books'
    );
  });

  it('should leave a hole without a property as written', () => {
    assert.equal(
      renderMessageTemplate('Fetching {entityType} entity {id}', { entityType: 'movies' }),
      'Fetching movies entity {id}'
    );
  });

  it('should render objects as JSON and accept destructuring and format markers', () => {
    assert.equal(
      renderMessageTemplate('Got {@value} at {when:u}', { value: { a: 1 }, when: 'now' }),
      'Got {"a":1} at now'
    );
  });
});

describe('buildSeqEvent', () => {
  it('should send the template as @mt and the rendered text as @m, with the properties', () => {
    const event = buildSeqEvent(
      'Information',
      'Fetching {entityType} entity {id}',
      { entityType: 'books', id: '7' },
      undefined,
      'production'
    );
    assert.equal(event['@mt'], 'Fetching {entityType} entity {id}');
    assert.equal(event['@m'], 'Fetching books entity 7');
    assert.equal(event.entityType, 'books');
    assert.equal(event.id, '7');
    assert.equal('Message' in event, false);
    assert.equal('@l' in event, false);
  });

  it('should use the SourceContext a module logger sets, and MediaSet.Remix otherwise', () => {
    assert.equal(
      buildSeqEvent('Debug', 'x', { SourceContext: 'app/api/entity-data' }).SourceContext,
      'app/api/entity-data'
    );
    assert.equal(buildSeqEvent('Debug', 'x').SourceContext, 'MediaSet.Remix');
  });

  it('should keep Application and Environment even when a property has the same name', () => {
    const event = buildSeqEvent(
      'Warning',
      'x',
      { Environment: 'production', Application: 'other' },
      undefined,
      'production'
    );
    assert.equal(event.Environment, 'Production');
    assert.equal(event.Application, 'MediaSet.Remix');
    assert.equal(event['@l'], 'Warning');
  });

  it('should send an exception as @x', () => {
    assert.equal(buildSeqEvent('Error', 'x', {}, 'Error: boom')['@x'], 'Error: boom');
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

  it('should send the key from ExternalLogging__SeqApiKey as the X-Seq-ApiKey header', async (t) => {
    const names = ['ExternalLogging__Enabled', 'ExternalLogging__SeqUrl', 'ExternalLogging__SeqApiKey'] as const;
    const originalFetch = globalThis.fetch;
    const originalEnv = names.map((name) => [name, process.env[name]] as const);
    const headers: Array<HeadersInit | undefined> = [];
    globalThis.fetch = (async (_input: RequestInfo | URL, init?: RequestInit) => {
      headers.push(init?.headers);
      return new Response(null, { status: 201 });
    }) as typeof fetch;
    process.env['ExternalLogging__Enabled'] = 'true';
    process.env['ExternalLogging__SeqUrl'] = 'http://seq.test';
    process.env['ExternalLogging__SeqApiKey'] = 'TEST_KEY';
    t.after(() => {
      globalThis.fetch = originalFetch;
      for (const [name, value] of originalEnv) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
    });

    const specifier = './serverLogger.ts?seq-api-key';
    const { serverLogger } = (await import(specifier)) as typeof import('./serverLogger.ts');
    serverLogger.info('hello');
    await new Promise((resolve) => setTimeout(resolve, 20));

    assert.equal(headers.length, 1);
    assert.deepEqual(headers[0], { 'Content-Type': 'application/vnd.serilog.clef', 'X-Seq-ApiKey': 'TEST_KEY' });
  });

  it('should send a module logger event with its template, SourceContext and the error as @x', async (t) => {
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
    t.mock.method(console, 'error', () => {});
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

    const specifier = './serverLogger.ts?module-logger';
    const { createServerLogger } = (await import(specifier)) as typeof import('./serverLogger.ts');
    createServerLogger('app/api/entity-data').error('Failed to delete {entityType} entity {id}', new Error('boom'), {
      entityType: 'books',
      id: '7',
    });
    await new Promise((resolve) => setTimeout(resolve, 20));

    assert.equal(bodies.length, 1);
    assert.equal(bodies[0]['@mt'], 'Failed to delete {entityType} entity {id}');
    assert.equal(bodies[0]['@m'], 'Failed to delete books entity 7');
    assert.equal(bodies[0]['@l'], 'Error');
    assert.match(String(bodies[0]['@x']), /Error: boom/);
    assert.equal(bodies[0].SourceContext, 'app/api/entity-data');
  });
});
