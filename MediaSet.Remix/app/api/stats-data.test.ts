import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Create a global mock function before mocking
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

// Now import after mocking
import { getStats } from '~/api/stats-data';

describe('stats-data.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Set up default mock for any apiFetch calls (e.g., from serverLogger)
    // to prevent actual network requests during tests
    mockApiFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({}),
    });
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('getStats', () => {
    it('should fetch and return complete stats object', async () => {
      const mockStats = {
        bookStats: {
          total: 42,
          totalFormats: 3,
          formats: ['Hardcover', 'Paperback', 'eBook'],
          uniqueAuthors: 28,
          totalPages: 12504,
        },
        movieStats: {
          total: 156,
          totalFormats: 2,
          formats: ['Blu-ray', 'DVD'],
          totalTvSeries: 12,
        },
        gameStats: {
          total: 87,
          totalFormats: 3,
          formats: ['Disc', 'Digital', 'Physical'],
          totalPlatforms: 5,
          platforms: ['PS5', 'Xbox Series X', 'Switch', 'PC', 'PS4'],
        },
        musicStats: {
          total: 234,
          totalFormats: 2,
          formats: ['CD', 'Vinyl'],
          uniqueArtists: 156,
          totalTracks: 2847,
        },
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockStats,
      });

      const result = await getStats();

      expect(result).toEqual(mockStats);
      expect(mockApiFetch).toHaveBeenCalledWith(expect.stringContaining('/stats'));
    });

    it('should handle zero statistics', async () => {
      const emptyStats = {
        bookStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          uniqueAuthors: 0,
          totalPages: 0,
        },
        movieStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          totalTvSeries: 0,
        },
        gameStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          totalPlatforms: 0,
          platforms: [],
        },
        musicStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          uniqueArtists: 0,
          totalTracks: 0,
        },
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => emptyStats,
      });

      const result = await getStats();

      expect(result.bookStats.total).toBe(0);
      expect(result.movieStats.total).toBe(0);
      expect(result.gameStats.total).toBe(0);
      expect(result.musicStats.total).toBe(0);
    });

    it('should have correct structure for all stats categories', async () => {
      const mockStats = {
        bookStats: { total: 10, totalFormats: 1, formats: ['Book'], uniqueAuthors: 5, totalPages: 1000 },
        movieStats: { total: 20, totalFormats: 1, formats: ['Movie'], totalTvSeries: 0 },
        gameStats: { total: 15, totalFormats: 1, formats: ['Game'], totalPlatforms: 1, platforms: ['PC'] },
        musicStats: { total: 25, totalFormats: 1, formats: ['Music'], uniqueArtists: 10, totalTracks: 100 },
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockStats,
      });

      const result = await getStats();

      expect(result).toHaveProperty('bookStats');
      expect(result).toHaveProperty('movieStats');
      expect(result).toHaveProperty('gameStats');
      expect(result).toHaveProperty('musicStats');
      expect(result.bookStats.total).toBe(10);
      expect(result.movieStats.totalTvSeries).toBe(0);
      expect(result.gameStats.platforms).toHaveLength(1);
      expect(result.musicStats.totalTracks).toBe(100);
    });

    it('should throw error when fetch fails', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      try {
        await getStats();
        expect.fail('Should have thrown');
      } catch (error) {
        expect(error).toBeInstanceOf(Response);
      }
    });

    it('should handle network errors', async () => {
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(getStats()).rejects.toThrow('Network error');
    });

    it('should parse and return JSON response', async () => {
      const mockStats = {
        bookStats: { total: 5, totalFormats: 1, formats: ['Hardcover'], uniqueAuthors: 3, totalPages: 500 },
        movieStats: { total: 10, totalFormats: 1, formats: ['DVD'], totalTvSeries: 1 },
        gameStats: { total: 8, totalFormats: 2, formats: ['Disc', 'Digital'], totalPlatforms: 2, platforms: ['PS5', 'PC'] },
        musicStats: { total: 15, totalFormats: 1, formats: ['CD'], uniqueArtists: 10, totalTracks: 150 },
      };

      const mockResponse = {
        ok: true,
        json: vi.fn(async () => mockStats),
      };

      mockApiFetch.mockResolvedValueOnce(mockResponse);

      const result = await getStats();

      expect(mockResponse.json).toHaveBeenCalled();
      expect(result).toEqual(mockStats);
    });

    it('should preserve numeric and array types', async () => {
      const mockStats = {
        bookStats: { total: 42, totalFormats: 3, formats: ['A', 'B', 'C'], uniqueAuthors: 28, totalPages: 12504 },
        movieStats: { total: 156, totalFormats: 2, formats: ['X', 'Y'], totalTvSeries: 12 },
        gameStats: { total: 87, totalFormats: 3, formats: [], totalPlatforms: 5, platforms: ['P1', 'P2'] },
        musicStats: { total: 234, totalFormats: 2, formats: ['F1'], uniqueArtists: 156, totalTracks: 2847 },
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockStats,
      });

      const result = await getStats();

      expect(typeof result.bookStats.total).toBe('number');
      expect(typeof result.musicStats.totalTracks).toBe('number');
      expect(Array.isArray(result.bookStats.formats)).toBe(true);
      expect(Array.isArray(result.gameStats.platforms)).toBe(true);
    });

    it('should handle large numbers', async () => {
      const mockStats = {
        bookStats: {
          total: 9999,
          totalFormats: 10,
          formats: Array.from({ length: 10 }, (_, i) => `Format${i}`),
          uniqueAuthors: 5000,
          totalPages: 5000000,
        },
        movieStats: { total: 5000, totalFormats: 5, formats: [], totalTvSeries: 500 },
        gameStats: {
          total: 3000,
          totalFormats: 8,
          formats: [],
          totalPlatforms: 20,
          platforms: Array.from({ length: 20 }, (_, i) => `Platform${i}`),
        },
        musicStats: { total: 10000, totalFormats: 5, formats: [], uniqueArtists: 8000, totalTracks: 100000 },
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockStats,
      });

      const result = await getStats();

      expect(result.bookStats.total).toBe(9999);
      expect(result.bookStats.totalPages).toBe(5000000);
      expect(result.musicStats.totalTracks).toBe(100000);
      expect(result.gameStats.platforms).toHaveLength(20);
    });

    it('should handle concurrent calls', async () => {
      const mockStats = {
        bookStats: { total: 10, totalFormats: 1, formats: ['A'], uniqueAuthors: 5, totalPages: 500 },
        movieStats: { total: 20, totalFormats: 1, formats: ['B'], totalTvSeries: 0 },
        gameStats: { total: 15, totalFormats: 1, formats: ['C'], totalPlatforms: 1, platforms: ['P'] },
        musicStats: { total: 25, totalFormats: 1, formats: ['D'], uniqueArtists: 10, totalTracks: 100 },
      };

      mockApiFetch
        .mockResolvedValueOnce({ ok: true, json: async () => mockStats })
        .mockResolvedValueOnce({ ok: true, json: async () => mockStats });

      const [result1, result2] = await Promise.all([getStats(), getStats()]);

      expect(result1).toEqual(mockStats);
      expect(result2).toEqual(mockStats);
      expect(mockApiFetch).toHaveBeenCalledTimes(2);
    });

    it('should handle JSON parsing errors', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => {
          throw new Error('JSON parse error');
        },
      });

      await expect(getStats()).rejects.toThrow('JSON parse error');
    });
  });
});
