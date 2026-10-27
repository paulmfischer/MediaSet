import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { readCollection, readEntity } from './collections.server';
import { Entity } from '~/models';

describe('collection API contract', () => {
  const signal = new AbortController().signal;
  beforeEach(() => vi.stubEnv('API_URL', 'http://private-api/'));
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.clearAllMocks();
  });
  it.each(Object.values(Entity))(
    'should request paged %s with encoded search, sort, pagination and cancellation',
    async (type) => {
      const page = { items: [], page: 2, totalPages: 3, totalCount: 160 };
      vi.mocked(fetch).mockResolvedValueOnce(Response.json(page));
      expect(await readCollection(type, { searchText: 'A&B', orderBy: 'title:desc', page: 2 }, signal)).toEqual(page);
      expect(fetch).toHaveBeenCalledWith(
        `http://private-api/${type}/pagedsearch?searchText=A%26B&orderBy=title%3Adesc&page=2&pageSize=75`,
        { signal }
      );
    }
  );
  it('should preserve 404 for missing media', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 404 }));
    await expect(readEntity(Entity.Books, 'a'.repeat(24), signal)).rejects.toMatchObject({ status: 404 });
  });
  it('should reject malformed IDs before calling the API', async () => {
    await expect(readEntity(Entity.Books, 'add', signal)).rejects.toMatchObject({ status: 404 });
    expect(fetch).not.toHaveBeenCalled();
  });
  it('should hide upstream failure details', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response('private host credentials', { status: 500 }));
    const response = await readEntity(Entity.Movies, 'b'.repeat(24), signal).catch((error: Response) => error);
    expect(response).toMatchObject({ status: 502 });
    expect(await (response as Response).text()).not.toContain('private');
  });
  it('should return a recoverable failure when the API is unreachable', async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new Error('ECONNREFUSED'));
    await expect(readEntity(Entity.Games, 'c'.repeat(24), signal)).rejects.toMatchObject({ status: 503 });
  });
  it('should reject non-JSON upstream responses', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response('not json'));
    await expect(readEntity(Entity.Musics, 'd'.repeat(24), signal)).rejects.toMatchObject({ status: 502 });
  });
});
