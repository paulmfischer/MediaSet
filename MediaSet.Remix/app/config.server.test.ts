import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

describe('API URL configuration', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('apiUrl', '');
    vi.stubEnv('clientApiUrl', '');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('should use the host API and same-origin browser proxy in development', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    const { baseUrl, clientApiUrl } = await import('./constants.server.ts');
    const { getClientConfig } = await import('./config.server.ts');

    expect(baseUrl).toBe('http://127.0.0.1:5000');
    expect(clientApiUrl).toBe('/dev-api');
    expect(getClientConfig().apiUrl).toBe('/dev-api');
  });

  it('should preserve production URL defaults', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    const { baseUrl, clientApiUrl } = await import('./constants.server.ts');
    const { getClientConfig } = await import('./config.server.ts');

    expect(baseUrl).toBe('http://localhost:7130');
    expect(clientApiUrl).toBe('http://localhost:7130');
    expect(getClientConfig().apiUrl).toBe(clientApiUrl);
  });

  it('should honor distinct configured server and browser URLs', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('apiUrl', 'http://other-api:8080');
    vi.stubEnv('clientApiUrl', 'https://media.example.test');
    const { baseUrl, clientApiUrl } = await import('./constants.server.ts');
    const { getClientConfig } = await import('./config.server.ts');

    expect(baseUrl).toBe('http://other-api:8080');
    expect(clientApiUrl).toBe('https://media.example.test');
    expect(getClientConfig().apiUrl).toBe(clientApiUrl);
  });
});
