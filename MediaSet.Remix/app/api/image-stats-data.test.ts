import { expect } from 'remix/assert';
import { describe, it } from 'remix/test';

import { mockFetch } from '../test/fetch-fakes.ts';

import { getImageStats } from './image-stats-data.ts';

const mockStats = {
  totalFiles: 42,
  totalSizeBytes: 1048576,
  filesByEntityType: { books: 20, movies: 15, games: 7 },
  sizeByEntityType: { books: 524288, movies: 393216, games: 131072 },
  brokenLinks: [
    { entityId: 'abc123', entityType: 'books', title: 'Missing Book', missingFilePath: 'books/abc123-guid.jpg' },
  ],
  orphanedFiles: [{ relativePath: 'books/orphan.jpg', sizeBytes: 12345 }],
  lastUpdated: '2026-03-17T02:00:00Z',
};

describe('image-stats-data.ts', () => {
  describe('getImageStats', () => {
    it('should return full image stats including broken links and orphaned files', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockStats,
      });

      const result = await getImageStats();

      expect(result).toEqual(mockStats);
      expect(result?.brokenLinks).toHaveLength(1);
      expect(result?.orphanedFiles).toHaveLength(1);
    });

    it('should return null on 204 No Content', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
        status: 204,
        json: async () => null,
      });

      const result = await getImageStats();

      expect(result).toBeNull();
    });

    it('should throw Response on non-ok status', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 500,
        json: async () => null,
      });

      await expect(getImageStats()).rejects.toBeInstanceOf(Response);
    });

    it('should throw on network error', async (t) => {
      const api = mockFetch(t);
      api.rejectOnce(new Error('Network error'));

      await expect(getImageStats()).rejects.toThrow('Network error');
    });
  });
});
