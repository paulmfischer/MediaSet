import { expect } from 'remix/assert';
import { describe, it } from 'remix/test';

import { createApiFakes, expectFetched } from '../test/api-fakes.ts';
import { deleteOrphanedImages } from './image-management-data.ts';

describe('image-management-data.ts', () => {
  describe('deleteOrphanedImages', () => {
    it('should return the count of deleted files', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => ({ deleted: 5 }),
      });

      const result = await deleteOrphanedImages(api.deps);

      expect(result).toEqual({ deleted: 5 });
      expectFetched(api.apiFetch, '/images/orphaned', { method: 'DELETE' });
    });

    it('should return zero when no orphaned files exist', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => ({ deleted: 0 }),
      });

      const result = await deleteOrphanedImages(api.deps);

      expect(result).toEqual({ deleted: 0 });
    });

    it('should throw Response on non-ok status', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({
        ok: false,
        status: 500,
        json: async () => null,
      });

      await expect(deleteOrphanedImages(api.deps)).rejects.toBeInstanceOf(Response);
    });

    it('should throw on network error', async (t) => {
      const api = createApiFakes(t);
      api.rejectOnce(new Error('Network error'));

      await expect(deleteOrphanedImages(api.deps)).rejects.toThrow('Network error');
    });
  });
});
