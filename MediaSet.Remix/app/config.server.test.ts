import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { expect } from 'remix/assert';
import { afterEach, beforeEach, describe, it } from 'remix/test';

const ENV_KEYS = ['NODE_ENV', 'apiUrl', 'clientApiUrl'] as const;

let savedEnv: Record<string, string | undefined> = {};
let importCount = 0;

/** Evaluates constants.server.ts afresh so it reads the environment as it is now. */
async function loadConstants(): Promise<typeof import('./constants.server.ts')> {
  importCount += 1;
  return import(`./constants.server.ts?fresh=${importCount}`);
}

/** Reads getClientConfig() in a fresh process, since config.server.ts keeps its own cached constants. */
function loadClientConfigApiUrl(env: Record<string, string>): string {
  const configUrl = new URL('./config.server.ts', import.meta.url).href;
  return execFileSync(
    process.execPath,
    [
      '--import',
      'remix/node-tsx',
      '--input-type=module',
      '-e',
      `const { getClientConfig } = await import(${JSON.stringify(configUrl)}); process.stdout.write(getClientConfig().apiUrl);`,
    ],
    { cwd: fileURLToPath(new URL('..', import.meta.url)), env: { ...process.env, ...env }, encoding: 'utf8' }
  );
}

describe('API URL configuration', () => {
  beforeEach(() => {
    savedEnv = Object.fromEntries(ENV_KEYS.map((key) => [key, process.env[key]]));
    process.env['apiUrl'] = '';
    process.env['clientApiUrl'] = '';
  });

  afterEach(() => {
    for (const key of ENV_KEYS) {
      if (savedEnv[key] === undefined) delete process.env[key];
      else process.env[key] = savedEnv[key];
    }
  });

  it('should use the host API and same-origin browser proxy in development', async () => {
    process.env['NODE_ENV'] = 'development';
    const { baseUrl, clientApiUrl } = await loadConstants();

    expect(baseUrl).toBe('http://127.0.0.1:5000');
    expect(clientApiUrl).toBe('/dev-api');
    expect(loadClientConfigApiUrl({ NODE_ENV: 'development', apiUrl: '', clientApiUrl: '' })).toBe('/dev-api');
  });

  it('should preserve production URL defaults', async () => {
    process.env['NODE_ENV'] = 'production';
    const { baseUrl, clientApiUrl } = await loadConstants();

    expect(baseUrl).toBe('http://localhost:7130');
    expect(clientApiUrl).toBe('http://localhost:7130');
    expect(loadClientConfigApiUrl({ NODE_ENV: 'production', apiUrl: '', clientApiUrl: '' })).toBe(clientApiUrl);
  });

  it('should honor distinct configured server and browser URLs', async () => {
    process.env['NODE_ENV'] = 'development';
    process.env['apiUrl'] = 'http://other-api:8080';
    process.env['clientApiUrl'] = 'https://media.example.test';
    const { baseUrl, clientApiUrl } = await loadConstants();

    expect(baseUrl).toBe('http://other-api:8080');
    expect(clientApiUrl).toBe('https://media.example.test');
    expect(
      loadClientConfigApiUrl({
        NODE_ENV: 'development',
        apiUrl: 'http://other-api:8080',
        clientApiUrl: 'https://media.example.test',
      })
    ).toBe(clientApiUrl);
  });
});
