import { afterEach, describe, expect, it, vi } from 'vitest';
import { getApiHealth } from './api.server';
import { publicApiUrl } from './config.server';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
});

describe('API loader boundary', () => {
  it('should fetch the private API while returning only the public status', async () => {
    vi.stubEnv('API_URL', 'http://private-api.invalid:5100/');
    vi.stubEnv('PUBLIC_API_URL', 'https://images.example.test');
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 'healthy',
          database: { name: 'private-database', status: 'up' },
          commit: 'private',
        })
      )
    );
    expect(await getApiHealth()).toEqual({ status: 'healthy' });
    expect(fetch).toHaveBeenCalledWith(
      'http://private-api.invalid:5100/health',
      expect.objectContaining({
        signal: expect.any(AbortSignal),
      })
    );
    expect(publicApiUrl()).toBe('https://images.example.test');
  });

  it.each([
    new Response('private failure details', { status: 500 }),
    new Response('invalid JSON'),
    new Response(JSON.stringify({ database: 'private' })),
  ])('should render a sanitized 503 when the API response is unusable', async (response) => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(response);
    const error: unknown = await getApiHealth().catch((error: unknown) => error);
    expect(error).toBeInstanceOf(Response);
    expect((error as Response).status).toBe(503);
    expect(await (error as Response).text()).toBe('The API is unavailable. Please try again shortly.');
  });

  it('should return a sanitized 503 when the network fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('secret-address'));
    await expect(getApiHealth()).rejects.toMatchObject({ status: 503 });
  });
});
