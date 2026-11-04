import { expect } from 'remix/assert';
import { afterEach, beforeEach, describe, it } from 'remix/test';

import { clientApiUrl as loadedClientApiUrl } from './constants.server.ts';
import { getClientConfig } from './config.server.ts';

const ENV_KEYS = ['NODE_ENV', 'apiUrl', 'clientApiUrl'] as const;

let savedEnv: Record<string, string | undefined> = {};
let importCount = 0;

/** Evaluates constants.server.ts afresh so it reads the environment as it is now. */
async function loadConstants(): Promise<typeof import('./constants.server.ts')> {
  importCount += 1;
  return import(`./constants.server.ts?fresh=${importCount}`);
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
  });

  it('should preserve production URL defaults', async () => {
    process.env['NODE_ENV'] = 'production';
    const { baseUrl, clientApiUrl } = await loadConstants();

    expect(baseUrl).toBe('http://localhost:7130');
    expect(clientApiUrl).toBe('http://localhost:7130');
  });

  it('should honor distinct configured server and browser URLs', async () => {
    process.env['NODE_ENV'] = 'development';
    process.env['apiUrl'] = 'http://other-api:8080';
    process.env['clientApiUrl'] = 'https://media.example.test';
    const { baseUrl, clientApiUrl } = await loadConstants();

    expect(baseUrl).toBe('http://other-api:8080');
    expect(clientApiUrl).toBe('https://media.example.test');
  });

  it('should expose the browser API URL through the client config', () => {
    expect(getClientConfig().apiUrl).toBe(loadedClientApiUrl);
  });
});
