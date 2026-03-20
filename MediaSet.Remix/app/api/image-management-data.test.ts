import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const { mockApiFetch } = vi.hoisted(() => ({
  mockApiFetch: vi.fn(),
}));

vi.mock('~/utils/apiFetch.server', () => ({
  apiFetch: mockApiFetch,
}));

vi.mock('~/utils/serverLogger', () => ({
  serverLogger: {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
  },
}));

import { deleteOrphanedImages } from '~/api/image-management-data';

describe('image-management-data.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ deleted: 0 }),
    });
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('deleteOrphanedImages', () => {
    it('should return the count of deleted files', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ deleted: 5 }),
      });

      const result = await deleteOrphanedImages();

      expect(result).toEqual({ deleted: 5 });
      expect(mockApiFetch).toHaveBeenCalledWith(expect.stringContaining('/images/orphaned'), { method: 'DELETE' });
    });

    it('should return zero when no orphaned files exist', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ deleted: 0 }),
      });

      const result = await deleteOrphanedImages();

      expect(result).toEqual({ deleted: 0 });
    });

    it('should throw Response on non-ok status', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => null,
      });

      await expect(deleteOrphanedImages()).rejects.toBeInstanceOf(Response);
    });

    it('should throw on network error', async () => {
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(deleteOrphanedImages()).rejects.toThrow('Network error');
    });
  });
});
