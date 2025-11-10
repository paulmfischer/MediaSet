import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  getAuthors,
  getPublishers,
  getGenres,
  getFormats,
  getStudios,
  getDevelopers,
  getPlatforms,
  getLabels,
  getGamePublishers,
  getArtist,
} from './metadata-data';
import { Entity } from './models';

// Mock fetch globally using vi.stubGlobal
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

describe('metadata-data.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('getMetadata - generic metadata fetching', () => {
    describe('getAuthors', () => {
      it('should fetch and return authors with label and value', async () => {
        const mockAuthors = ['Author One', 'Author Two', 'Author Three'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockAuthors,
        });

        const result = await getAuthors();

        expect(result).toEqual([
          { label: 'Author One', value: 'Author One' },
          { label: 'Author Two', value: 'Author Two' },
          { label: 'Author Three', value: 'Author Three' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Books}/authors`)
        );
        expect(result).toHaveLength(3);
      });

      it('should handle empty authors list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getAuthors();

        expect(result).toEqual([]);
        expect(result).toHaveLength(0);
      });

      it('should handle single author', async () => {
        const mockAuthors = ['Single Author'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockAuthors,
        });

        const result = await getAuthors();

        expect(result).toEqual([{ label: 'Single Author', value: 'Single Author' }]);
        expect(result).toHaveLength(1);
      });

      it('should preserve author names with special characters', async () => {
        const mockAuthors = ['O\'Brien, Patrick', 'José García', "D'Amato"];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockAuthors,
        });

        const result = await getAuthors();

        expect(result).toEqual([
          { label: 'O\'Brien, Patrick', value: 'O\'Brien, Patrick' },
          { label: 'José García', value: 'José García' },
          { label: "D'Amato", value: "D'Amato" },
        ]);
      });
    });

    describe('getPublishers', () => {
      it('should fetch and return publishers with label and value', async () => {
        const mockPublishers = ['Publisher One', 'Publisher Two'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPublishers,
        });

        const result = await getPublishers();

        expect(result).toEqual([
          { label: 'Publisher One', value: 'Publisher One' },
          { label: 'Publisher Two', value: 'Publisher Two' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Books}/publisher`)
        );
      });

      it('should handle empty publishers list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getPublishers();

        expect(result).toEqual([]);
      });

      it('should preserve publisher names', async () => {
        const mockPublishers = ['Penguin Books', 'Simon & Schuster', 'HarperCollins'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPublishers,
        });

        const result = await getPublishers();

        expect(result).toHaveLength(3);
        expect(result[1]).toEqual({ label: 'Simon & Schuster', value: 'Simon & Schuster' });
      });
    });

    describe('getGenres', () => {
      it('should fetch and return genres for books', async () => {
        const mockGenres = ['Fiction', 'Mystery', 'Science Fiction'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockGenres,
        });

        const result = await getGenres(Entity.Books);

        expect(result).toEqual([
          { label: 'Fiction', value: 'Fiction' },
          { label: 'Mystery', value: 'Mystery' },
          { label: 'Science Fiction', value: 'Science Fiction' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Books}/genres`)
        );
      });

      it('should fetch and return genres for movies', async () => {
        const mockGenres = ['Action', 'Drama', 'Comedy'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockGenres,
        });

        const result = await getGenres(Entity.Movies);

        expect(result).toEqual([
          { label: 'Action', value: 'Action' },
          { label: 'Drama', value: 'Drama' },
          { label: 'Comedy', value: 'Comedy' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Movies}/genres`)
        );
      });

      it('should fetch and return genres for games', async () => {
        const mockGenres = ['RPG', 'Action', 'Strategy'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockGenres,
        });

        const result = await getGenres(Entity.Games);

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Games}/genres`)
        );
        expect(result).toHaveLength(3);
      });

      it('should fetch and return genres for music', async () => {
        const mockGenres = ['Rock', 'Pop', 'Jazz'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockGenres,
        });

        const result = await getGenres(Entity.Musics);

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Musics}/genres`)
        );
        expect(result).toHaveLength(3);
      });

      it('should handle empty genres list for any entity', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getGenres(Entity.Books);

        expect(result).toEqual([]);
      });
    });

    describe('getFormats', () => {
      it('should fetch and return formats for books', async () => {
        const mockFormats = ['Hardcover', 'Paperback', 'eBook'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockFormats,
        });

        const result = await getFormats(Entity.Books);

        expect(result).toEqual([
          { label: 'Hardcover', value: 'Hardcover' },
          { label: 'Paperback', value: 'Paperback' },
          { label: 'eBook', value: 'eBook' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Books}/format`)
        );
      });

      it('should fetch and return formats for movies', async () => {
        const mockFormats = ['Blu-ray', 'DVD', '4K Ultra HD'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockFormats,
        });

        const result = await getFormats(Entity.Movies);

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Movies}/format`)
        );
        expect(result).toHaveLength(3);
      });

      it('should fetch and return formats for games', async () => {
        const mockFormats = ['Disc', 'Digital', 'Cartridge'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockFormats,
        });

        const result = await getFormats(Entity.Games);

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Games}/format`)
        );
      });

      it('should fetch and return formats for music', async () => {
        const mockFormats = ['CD', 'Vinyl', 'Cassette', 'Digital'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockFormats,
        });

        const result = await getFormats(Entity.Musics);

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Musics}/format`)
        );
      });

      it('should handle empty formats list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getFormats(Entity.Books);

        expect(result).toEqual([]);
      });
    });

    describe('getStudios', () => {
      it('should fetch and return movie studios', async () => {
        const mockStudios = ['Warner Bros', 'Universal Pictures', 'Disney'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockStudios,
        });

        const result = await getStudios();

        expect(result).toEqual([
          { label: 'Warner Bros', value: 'Warner Bros' },
          { label: 'Universal Pictures', value: 'Universal Pictures' },
          { label: 'Disney', value: 'Disney' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Movies}/studios`)
        );
      });

      it('should handle empty studios list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getStudios();

        expect(result).toEqual([]);
      });

      it('should preserve studio names with special characters', async () => {
        const mockStudios = ['20th Century Studios', 'A24 Films', 'Focus Features'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockStudios,
        });

        const result = await getStudios();

        expect(result).toHaveLength(3);
        expect(result[0]).toEqual({ label: '20th Century Studios', value: '20th Century Studios' });
      });
    });

    describe('getDevelopers', () => {
      it('should fetch and return game developers', async () => {
        const mockDevelopers = ['FromSoftware', 'Rockstar Games', 'Naughty Dog'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockDevelopers,
        });

        const result = await getDevelopers();

        expect(result).toEqual([
          { label: 'FromSoftware', value: 'FromSoftware' },
          { label: 'Rockstar Games', value: 'Rockstar Games' },
          { label: 'Naughty Dog', value: 'Naughty Dog' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Games}/developers`)
        );
      });

      it('should handle empty developers list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getDevelopers();

        expect(result).toEqual([]);
      });

      it('should handle single developer', async () => {
        const mockDevelopers = ['Nintendo'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockDevelopers,
        });

        const result = await getDevelopers();

        expect(result).toHaveLength(1);
      });
    });

    describe('getPlatforms', () => {
      it('should fetch and return game platforms', async () => {
        const mockPlatforms = ['PlayStation 5', 'Xbox Series X', 'Nintendo Switch', 'PC'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPlatforms,
        });

        const result = await getPlatforms();

        expect(result).toEqual([
          { label: 'PlayStation 5', value: 'PlayStation 5' },
          { label: 'Xbox Series X', value: 'Xbox Series X' },
          { label: 'Nintendo Switch', value: 'Nintendo Switch' },
          { label: 'PC', value: 'PC' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Games}/platform`)
        );
      });

      it('should handle empty platforms list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getPlatforms();

        expect(result).toEqual([]);
      });

      it('should handle multiple platforms', async () => {
        const mockPlatforms = Array.from({ length: 10 }, (_, i) => `Platform ${i + 1}`);

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPlatforms,
        });

        const result = await getPlatforms();

        expect(result).toHaveLength(10);
        expect(result[0]).toEqual({ label: 'Platform 1', value: 'Platform 1' });
      });
    });

    describe('getLabels', () => {
      it('should fetch and return music labels', async () => {
        const mockLabels = ['Atlantic Records', 'Sony Music', 'Universal Music Group'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockLabels,
        });

        const result = await getLabels();

        expect(result).toEqual([
          { label: 'Atlantic Records', value: 'Atlantic Records' },
          { label: 'Sony Music', value: 'Sony Music' },
          { label: 'Universal Music Group', value: 'Universal Music Group' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Musics}/label`)
        );
      });

      it('should handle empty labels list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getLabels();

        expect(result).toEqual([]);
      });

      it('should preserve label names', async () => {
        const mockLabels = ['4AD', 'Sub Pop', 'Matador Records'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockLabels,
        });

        const result = await getLabels();

        expect(result).toHaveLength(3);
        expect(result).toContainEqual({ label: '4AD', value: '4AD' });
      });
    });

    describe('getGamePublishers', () => {
      it('should fetch and return game publishers', async () => {
        const mockPublishers = ['Nintendo', 'Sony Interactive Entertainment', 'Microsoft'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPublishers,
        });

        const result = await getGamePublishers();

        expect(result).toEqual([
          { label: 'Nintendo', value: 'Nintendo' },
          { label: 'Sony Interactive Entertainment', value: 'Sony Interactive Entertainment' },
          { label: 'Microsoft', value: 'Microsoft' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Games}/publishers`)
        );
      });

      it('should handle empty publishers list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getGamePublishers();

        expect(result).toEqual([]);
      });

      it('should handle single publisher', async () => {
        const mockPublishers = ['EA Games'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockPublishers,
        });

        const result = await getGamePublishers();

        expect(result).toHaveLength(1);
        expect(result[0].value).toBe('EA Games');
      });
    });

    describe('getArtist', () => {
      it('should fetch and return music artists', async () => {
        const mockArtists = ['The Beatles', 'Queen', 'David Bowie'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockArtists,
        });

        const result = await getArtist();

        expect(result).toEqual([
          { label: 'The Beatles', value: 'The Beatles' },
          { label: 'Queen', value: 'Queen' },
          { label: 'David Bowie', value: 'David Bowie' },
        ]);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`/${Entity.Musics}/artist`)
        );
      });

      it('should handle empty artists list', async () => {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        const result = await getArtist();

        expect(result).toEqual([]);
      });

      it('should preserve artist names with special characters', async () => {
        const mockArtists = ['AC/DC', 'Guns N\' Roses', 'Wu-Tang Clan'];

        mockFetch.mockResolvedValueOnce({
          json: async () => mockArtists,
        });

        const result = await getArtist();

        expect(result).toHaveLength(3);
        expect(result[0]).toEqual({ label: 'AC/DC', value: 'AC/DC' });
      });

      it('should handle many artists', async () => {
        const mockArtists = Array.from({ length: 50 }, (_, i) => `Artist ${i + 1}`);

        mockFetch.mockResolvedValueOnce({
          json: async () => mockArtists,
        });

        const result = await getArtist();

        expect(result).toHaveLength(50);
      });
    });
  });

  describe('Data transformation - label/value mapping', () => {
    it('should create proper label-value pair structure', async () => {
      const mockData = ['Item 1', 'Item 2'];

      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result = await getAuthors();

      result.forEach((item, index) => {
        expect(item).toHaveProperty('label');
        expect(item).toHaveProperty('value');
        expect(item.label).toBe(mockData[index]);
        expect(item.value).toBe(mockData[index]);
      });
    });

    it('should use same value for both label and value field', async () => {
      const mockData = ['Unique Item'];

      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result = await getPublishers();

      expect(result[0].label).toBe(result[0].value);
    });

    it('should preserve exact text from API response', async () => {
      const mockData = ['Item With Spaces', 'item_with_underscore', 'ITEM-WITH-DASH'];

      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result = await getGenres(Entity.Books);

      expect(result[0]).toEqual({ label: 'Item With Spaces', value: 'Item With Spaces' });
      expect(result[1]).toEqual({ label: 'item_with_underscore', value: 'item_with_underscore' });
      expect(result[2]).toEqual({ label: 'ITEM-WITH-DASH', value: 'ITEM-WITH-DASH' });
    });

    it('should maintain order of items from API response', async () => {
      const mockData = ['Z', 'A', 'M', 'B'];

      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result = await getFormats(Entity.Movies);

      expect(result[0].value).toBe('Z');
      expect(result[1].value).toBe('A');
      expect(result[2].value).toBe('M');
      expect(result[3].value).toBe('B');
    });
  });

  describe('Error cases', () => {
    it('should throw error when fetch fails', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(getAuthors()).rejects.toThrow('Network error');
    });

    it('should handle fetch failure for all metadata functions', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Connection failed'));

      await expect(getPublishers()).rejects.toThrow('Connection failed');

      vi.clearAllMocks();
      mockFetch.mockRejectedValueOnce(new Error('Connection failed'));

      await expect(getStudios()).rejects.toThrow('Connection failed');

      vi.clearAllMocks();
      mockFetch.mockRejectedValueOnce(new Error('Connection failed'));

      await expect(getDevelopers()).rejects.toThrow('Connection failed');
    });

    it('should handle JSON parsing errors', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => {
          throw new Error('Invalid JSON');
        },
      });

      await expect(getGenres(Entity.Books)).rejects.toThrow('Invalid JSON');
    });

    it('should throw error when API returns undefined', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => undefined,
      });

      await expect(getAuthors()).rejects.toThrow();
    });

    it('should throw error when API returns null', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => null,
      });

      await expect(getPublishers()).rejects.toThrow();
    });

    it('should throw error if fetch is called without response being ok (implicit)', async () => {
      // The current implementation doesn't explicitly check response.ok,
      // but it should gracefully handle invalid JSON responses
      mockFetch.mockResolvedValueOnce({
        json: async () => {
          throw new TypeError('Failed to parse JSON');
        },
      });

      await expect(getStudios()).rejects.toThrow();
    });
  });

  describe('API endpoint construction', () => {
    it('should construct correct endpoint for book authors', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getAuthors();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Books}/authors`)
      );
    });

    it('should construct correct endpoint for book publishers', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getPublishers();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Books}/publisher`)
      );
    });

    it('should construct correct endpoints for different entity types', async () => {
      const testCases = [
        { fn: () => getGenres(Entity.Books), entity: Entity.Books, property: 'genres' },
        { fn: () => getGenres(Entity.Movies), entity: Entity.Movies, property: 'genres' },
        { fn: () => getGenres(Entity.Games), entity: Entity.Games, property: 'genres' },
        { fn: () => getGenres(Entity.Musics), entity: Entity.Musics, property: 'genres' },
        { fn: () => getFormats(Entity.Books), entity: Entity.Books, property: 'format' },
        { fn: () => getFormats(Entity.Movies), entity: Entity.Movies, property: 'format' },
        { fn: () => getFormats(Entity.Games), entity: Entity.Games, property: 'format' },
        { fn: () => getFormats(Entity.Musics), entity: Entity.Musics, property: 'format' },
      ];

      for (const testCase of testCases) {
        mockFetch.mockResolvedValueOnce({
          json: async () => [],
        });

        await testCase.fn();

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining(`${testCase.entity}/${testCase.property}`)
        );

        vi.clearAllMocks();
      }
    });

    it('should use baseUrl from constants in fetch call', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getAuthors();

      const calls = mockFetch.mock.calls;
      expect(calls[0][0]).toContain('metadata');
      expect(calls[0][0]).toContain(Entity.Books);
    });

    it('should construct endpoints for game-specific functions', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getDevelopers();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Games}/developers`)
      );

      vi.clearAllMocks();
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getPlatforms();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Games}/platform`)
      );

      vi.clearAllMocks();
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getGamePublishers();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Games}/publishers`)
      );
    });

    it('should construct endpoints for music-specific functions', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getLabels();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Musics}/label`)
      );

      vi.clearAllMocks();
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getArtist();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Musics}/artist`)
      );
    });

    it('should construct endpoints for movie-specific functions', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      });

      await getStudios();

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`${Entity.Movies}/studios`)
      );
    });
  });

  describe('Integration scenarios', () => {
    it('should fetch metadata for all functions in sequence', async () => {
      const functions = [
        { fn: getAuthors, name: 'getAuthors' },
        { fn: getPublishers, name: 'getPublishers' },
        { fn: () => getGenres(Entity.Books), name: 'getGenres(Books)' },
        { fn: getStudios, name: 'getStudios' },
        { fn: getDevelopers, name: 'getDevelopers' },
        { fn: getPlatforms, name: 'getPlatforms' },
        { fn: getLabels, name: 'getLabels' },
        { fn: getGamePublishers, name: 'getGamePublishers' },
        { fn: getArtist, name: 'getArtist' },
      ];

      for (const testCase of functions) {
        mockFetch.mockResolvedValueOnce({
          json: async () => ['Item 1', 'Item 2'],
        });

        const result = await testCase.fn();

        expect(result).toHaveLength(2);
        expect(result[0]).toHaveProperty('label');
        expect(result[0]).toHaveProperty('value');

        vi.clearAllMocks();
      }
    });

    it('should handle mixed success and error scenarios', async () => {
      mockFetch.mockResolvedValueOnce({
        json: async () => ['Author 1'],
      });

      const authorsResult = await getAuthors();
      expect(authorsResult).toHaveLength(1);

      vi.clearAllMocks();
      mockFetch.mockRejectedValueOnce(new Error('API Error'));

      await expect(getPublishers()).rejects.toThrow('API Error');
    });

    it('should return consistently formatted responses', async () => {
      const mockData = ['Data 1', 'Data 2', 'Data 3'];

      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result1 = await getAuthors();

      vi.clearAllMocks();
      mockFetch.mockResolvedValueOnce({
        json: async () => mockData,
      });

      const result2 = await getStudios();

      expect(result1).toEqual(result2);
    });
  });
});
