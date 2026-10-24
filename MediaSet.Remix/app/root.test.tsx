import { afterEach, describe, expect, it, vi } from 'vitest';
import { headers } from './root';

describe('root security headers', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('should allow cover previews from arbitrary HTTPS image hosts in production', () => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('clientApiUrl', 'https://mediaset-api.fcortext.net');

    const policy = headers()['Content-Security-Policy'];
    const imageSources = policy
      .split(';')
      .find((directive) => directive.trim().startsWith('img-src '))
      ?.trim()
      .split(/\s+/)
      .slice(1);

    expect(imageSources).toContain('https:');
    expect(imageSources).toContain("'self'");
    expect(imageSources).toContain('data:');
    expect(policy).toContain("default-src 'self'");
    expect(policy).toContain("style-src 'self' 'unsafe-inline'");
    expect(policy).toContain("script-src 'self' 'unsafe-inline'");
  });

  it('should retain the configured HTTP API source for stored images', () => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('clientApiUrl', 'http://192.168.50.221:7130');

    const policy = headers()['Content-Security-Policy'];

    expect(policy).toContain('http://192.168.50.221:7130');
    expect(policy).not.toMatch(/(?:^|\s)http:(?:\s|;|$)/);
  });

  it('should omit the production content security policy in development', () => {
    vi.stubEnv('NODE_ENV', 'development');

    const securityHeaders = headers();

    expect(securityHeaders).not.toHaveProperty('Content-Security-Policy');
    expect(securityHeaders['X-Content-Type-Options']).toBe('nosniff');
    expect(securityHeaders['X-Frame-Options']).toBe('DENY');
  });
});
