import { expect } from 'remix/assert';
import { describe, it } from 'remix/test';

import { createApiFakes, expectFetched } from '../test/api-fakes.ts';

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
} from './metadata-data.ts';
import { Entity } from '../models.ts';

describe('metadata-data.ts', () => {
  describe('getMetadata - generic metadata fetching', () => {
    describe('getAuthors', () => {
      it('should fetch and return authors with label and value', async (t) => {
        const api = createApiFakes(t);
        const mockAuthors = ['Author One', 'Author Two', 'Author Three'];

        api.respondOnce({ ok: true, json: async () => mockAuthors });

        const result = await getAuthors(api.deps);

        expect(result).toEqual([
          { label: 'Author One', value: 'Author One' },
          { label: 'Author Two', value: 'Author Two' },
          { label: 'Author Three', value: 'Author Three' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Books}/authors`);
        expect(result).toHaveLength(3);
      });

      it('should handle empty authors list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getAuthors(api.deps);

        expect(result).toEqual([]);
        expect(result).toHaveLength(0);
      });

      it('should handle single author', async (t) => {
        const api = createApiFakes(t);
        const mockAuthors = ['Single Author'];

        api.respondOnce({ ok: true, json: async () => mockAuthors });

        const result = await getAuthors(api.deps);

        expect(result).toEqual([{ label: 'Single Author', value: 'Single Author' }]);
        expect(result).toHaveLength(1);
      });

      it('should preserve author names with special characters', async (t) => {
        const api = createApiFakes(t);
        const mockAuthors = ["O'Brien, Patrick", 'José García', "D'Amato"];

        api.respondOnce({ ok: true, json: async () => mockAuthors });

        const result = await getAuthors(api.deps);

        expect(result).toEqual([
          { label: "O'Brien, Patrick", value: "O'Brien, Patrick" },
          { label: 'José García', value: 'José García' },
          { label: "D'Amato", value: "D'Amato" },
        ]);
      });
    });

    describe('getPublishers', () => {
      it('should fetch and return publishers with label and value', async (t) => {
        const api = createApiFakes(t);
        const mockPublishers = ['Publisher One', 'Publisher Two'];

        api.respondOnce({ ok: true, json: async () => mockPublishers });

        const result = await getPublishers(api.deps);

        expect(result).toEqual([
          { label: 'Publisher One', value: 'Publisher One' },
          { label: 'Publisher Two', value: 'Publisher Two' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Books}/publisher`);
      });

      it('should handle empty publishers list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getPublishers(api.deps);

        expect(result).toEqual([]);
      });

      it('should preserve publisher names', async (t) => {
        const api = createApiFakes(t);
        const mockPublishers = ['Penguin Books', 'Simon & Schuster', 'HarperCollins'];

        api.respondOnce({ ok: true, json: async () => mockPublishers });

        const result = await getPublishers(api.deps);

        expect(result).toHaveLength(3);
        expect(result[1]).toEqual({ label: 'Simon & Schuster', value: 'Simon & Schuster' });
      });
    });

    describe('getGenres', () => {
      it('should fetch and return genres for books', async (t) => {
        const api = createApiFakes(t);
        const mockGenres = ['Fiction', 'Mystery', 'Science Fiction'];

        api.respondOnce({ ok: true, json: async () => mockGenres });

        const result = await getGenres(Entity.Books, api.deps);

        expect(result).toEqual([
          { label: 'Fiction', value: 'Fiction' },
          { label: 'Mystery', value: 'Mystery' },
          { label: 'Science Fiction', value: 'Science Fiction' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Books}/genres`);
      });

      it('should fetch and return genres for movies', async (t) => {
        const api = createApiFakes(t);
        const mockGenres = ['Action', 'Drama', 'Comedy'];

        api.respondOnce({ ok: true, json: async () => mockGenres });

        const result = await getGenres(Entity.Movies, api.deps);

        expect(result).toEqual([
          { label: 'Action', value: 'Action' },
          { label: 'Drama', value: 'Drama' },
          { label: 'Comedy', value: 'Comedy' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Movies}/genres`);
      });

      it('should fetch and return genres for games', async (t) => {
        const api = createApiFakes(t);
        const mockGenres = ['RPG', 'Action', 'Strategy'];

        api.respondOnce({ ok: true, json: async () => mockGenres });

        const result = await getGenres(Entity.Games, api.deps);

        expectFetched(api.apiFetch, `/${Entity.Games}/genres`);
        expect(result).toHaveLength(3);
      });

      it('should fetch and return genres for music', async (t) => {
        const api = createApiFakes(t);
        const mockGenres = ['Rock', 'Pop', 'Jazz'];

        api.respondOnce({ ok: true, json: async () => mockGenres });

        const result = await getGenres(Entity.Musics, api.deps);

        expectFetched(api.apiFetch, `/${Entity.Musics}/genres`);
        expect(result).toHaveLength(3);
      });

      it('should handle empty genres list for any entity', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getGenres(Entity.Books, api.deps);

        expect(result).toEqual([]);
      });
    });

    describe('getFormats', () => {
      it('should fetch and return formats for books', async (t) => {
        const api = createApiFakes(t);
        const mockFormats = ['Hardcover', 'Paperback', 'eBook'];

        api.respondOnce({ ok: true, json: async () => mockFormats });

        const result = await getFormats(Entity.Books, api.deps);

        expect(result).toEqual([
          { label: 'Hardcover', value: 'Hardcover' },
          { label: 'Paperback', value: 'Paperback' },
          { label: 'eBook', value: 'eBook' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Books}/format`);
      });

      it('should fetch and return formats for movies', async (t) => {
        const api = createApiFakes(t);
        const mockFormats = ['Blu-ray', 'DVD', '4K Ultra HD'];

        api.respondOnce({ ok: true, json: async () => mockFormats });

        const result = await getFormats(Entity.Movies, api.deps);

        expectFetched(api.apiFetch, `/${Entity.Movies}/format`);
        expect(result).toHaveLength(3);
      });

      it('should fetch and return formats for games', async (t) => {
        const api = createApiFakes(t);
        const mockFormats = ['Disc', 'Digital', 'Cartridge'];

        api.respondOnce({ ok: true, json: async () => mockFormats });

        await getFormats(Entity.Games, api.deps);

        expectFetched(api.apiFetch, `/${Entity.Games}/format`);
      });

      it('should fetch and return formats for music', async (t) => {
        const api = createApiFakes(t);
        const mockFormats = ['CD', 'Vinyl', 'Cassette', 'Digital'];

        api.respondOnce({ ok: true, json: async () => mockFormats });

        await getFormats(Entity.Musics, api.deps);

        expectFetched(api.apiFetch, `/${Entity.Musics}/format`);
      });

      it('should handle empty formats list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getFormats(Entity.Books, api.deps);

        expect(result).toEqual([]);
      });
    });

    describe('getStudios', () => {
      it('should fetch and return movie studios', async (t) => {
        const api = createApiFakes(t);
        const mockStudios = ['Warner Bros', 'Universal Pictures', 'Disney'];

        api.respondOnce({ ok: true, json: async () => mockStudios });

        const result = await getStudios(api.deps);

        expect(result).toEqual([
          { label: 'Warner Bros', value: 'Warner Bros' },
          { label: 'Universal Pictures', value: 'Universal Pictures' },
          { label: 'Disney', value: 'Disney' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Movies}/studios`);
      });

      it('should handle empty studios list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getStudios(api.deps);

        expect(result).toEqual([]);
      });

      it('should preserve studio names with special characters', async (t) => {
        const api = createApiFakes(t);
        const mockStudios = ['20th Century Studios', 'A24 Films', 'Focus Features'];

        api.respondOnce({ ok: true, json: async () => mockStudios });

        const result = await getStudios(api.deps);

        expect(result).toHaveLength(3);
        expect(result[0]).toEqual({ label: '20th Century Studios', value: '20th Century Studios' });
      });
    });

    describe('getDevelopers', () => {
      it('should fetch and return game developers', async (t) => {
        const api = createApiFakes(t);
        const mockDevelopers = ['FromSoftware', 'Rockstar Games', 'Naughty Dog'];

        api.respondOnce({ ok: true, json: async () => mockDevelopers });

        const result = await getDevelopers(api.deps);

        expect(result).toEqual([
          { label: 'FromSoftware', value: 'FromSoftware' },
          { label: 'Rockstar Games', value: 'Rockstar Games' },
          { label: 'Naughty Dog', value: 'Naughty Dog' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Games}/developers`);
      });

      it('should handle empty developers list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getDevelopers(api.deps);

        expect(result).toEqual([]);
      });

      it('should handle single developer', async (t) => {
        const api = createApiFakes(t);
        const mockDevelopers = ['Nintendo'];

        api.respondOnce({ ok: true, json: async () => mockDevelopers });

        const result = await getDevelopers(api.deps);

        expect(result).toHaveLength(1);
      });
    });

    describe('getPlatforms', () => {
      it('should fetch and return game platforms', async (t) => {
        const api = createApiFakes(t);
        const mockPlatforms = ['PlayStation 5', 'Xbox Series X', 'Nintendo Switch', 'PC'];

        api.respondOnce({ ok: true, json: async () => mockPlatforms });

        const result = await getPlatforms(api.deps);

        expect(result).toEqual([
          { label: 'PlayStation 5', value: 'PlayStation 5' },
          { label: 'Xbox Series X', value: 'Xbox Series X' },
          { label: 'Nintendo Switch', value: 'Nintendo Switch' },
          { label: 'PC', value: 'PC' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Games}/platform`);
      });

      it('should handle empty platforms list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getPlatforms(api.deps);

        expect(result).toEqual([]);
      });

      it('should handle multiple platforms', async (t) => {
        const api = createApiFakes(t);
        const mockPlatforms = Array.from({ length: 10 }, (_, i) => `Platform ${i + 1}`);

        api.respondOnce({ ok: true, json: async () => mockPlatforms });

        const result = await getPlatforms(api.deps);

        expect(result).toHaveLength(10);
        expect(result[0]).toEqual({ label: 'Platform 1', value: 'Platform 1' });
      });
    });

    describe('getLabels', () => {
      it('should fetch and return music labels', async (t) => {
        const api = createApiFakes(t);
        const mockLabels = ['Atlantic Records', 'Sony Music', 'Universal Music Group'];

        api.respondOnce({ ok: true, json: async () => mockLabels });

        const result = await getLabels(api.deps);

        expect(result).toEqual([
          { label: 'Atlantic Records', value: 'Atlantic Records' },
          { label: 'Sony Music', value: 'Sony Music' },
          { label: 'Universal Music Group', value: 'Universal Music Group' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Musics}/label`);
      });

      it('should handle empty labels list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getLabels(api.deps);

        expect(result).toEqual([]);
      });

      it('should preserve label names', async (t) => {
        const api = createApiFakes(t);
        const mockLabels = ['4AD', 'Sub Pop', 'Matador Records'];

        api.respondOnce({ ok: true, json: async () => mockLabels });

        const result = await getLabels(api.deps);

        expect(result).toHaveLength(3);
        expect(result.some((option) => option.label === '4AD' && option.value === '4AD')).toBe(true);
      });
    });

    describe('getGamePublishers', () => {
      it('should fetch and return game publishers', async (t) => {
        const api = createApiFakes(t);
        const mockPublishers = ['Nintendo', 'Sony Interactive Entertainment', 'Microsoft'];

        api.respondOnce({ ok: true, json: async () => mockPublishers });

        const result = await getGamePublishers(api.deps);

        expect(result).toEqual([
          { label: 'Nintendo', value: 'Nintendo' },
          { label: 'Sony Interactive Entertainment', value: 'Sony Interactive Entertainment' },
          { label: 'Microsoft', value: 'Microsoft' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Games}/publishers`);
      });

      it('should handle empty publishers list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getGamePublishers(api.deps);

        expect(result).toEqual([]);
      });

      it('should handle single publisher', async (t) => {
        const api = createApiFakes(t);
        const mockPublishers = ['EA Games'];

        api.respondOnce({ ok: true, json: async () => mockPublishers });

        const result = await getGamePublishers(api.deps);

        expect(result).toHaveLength(1);
        expect(result[0].value).toBe('EA Games');
      });
    });

    describe('getArtist', () => {
      it('should fetch and return music artists', async (t) => {
        const api = createApiFakes(t);
        const mockArtists = ['The Beatles', 'Queen', 'David Bowie'];

        api.respondOnce({ ok: true, json: async () => mockArtists });

        const result = await getArtist(api.deps);

        expect(result).toEqual([
          { label: 'The Beatles', value: 'The Beatles' },
          { label: 'Queen', value: 'Queen' },
          { label: 'David Bowie', value: 'David Bowie' },
        ]);
        expectFetched(api.apiFetch, `/${Entity.Musics}/artist`);
      });

      it('should handle empty artists list', async (t) => {
        const api = createApiFakes(t);
        api.respondOnce({ ok: true, json: async () => [] });

        const result = await getArtist(api.deps);

        expect(result).toEqual([]);
      });

      it('should preserve artist names with special characters', async (t) => {
        const api = createApiFakes(t);
        const mockArtists = ['AC/DC', "Guns N' Roses", 'Wu-Tang Clan'];

        api.respondOnce({ ok: true, json: async () => mockArtists });

        const result = await getArtist(api.deps);

        expect(result).toHaveLength(3);
        expect(result[0]).toEqual({ label: 'AC/DC', value: 'AC/DC' });
      });

      it('should handle many artists', async (t) => {
        const api = createApiFakes(t);
        const mockArtists = Array.from({ length: 50 }, (_, i) => `Artist ${i + 1}`);

        api.respondOnce({ ok: true, json: async () => mockArtists });

        const result = await getArtist(api.deps);

        expect(result).toHaveLength(50);
      });
    });
  });

  describe('Data transformation - label/value mapping', () => {
    it('should create proper label-value pair structure', async (t) => {
      const api = createApiFakes(t);
      const mockData = ['Item 1', 'Item 2'];

      api.respondOnce({ ok: true, json: async () => mockData });

      const result = await getAuthors(api.deps);

      result.forEach((item, index) => {
        expect(item).toHaveProperty('label');
        expect(item).toHaveProperty('value');
        expect(item.label).toBe(mockData[index]);
        expect(item.value).toBe(mockData[index]);
      });
    });

    it('should use same value for both label and value field', async (t) => {
      const api = createApiFakes(t);
      const mockData = ['Unique Item'];

      api.respondOnce({ ok: true, json: async () => mockData });

      const result = await getPublishers(api.deps);

      expect(result[0].label).toBe(result[0].value);
    });

    it('should preserve exact text from API response', async (t) => {
      const api = createApiFakes(t);
      const mockData = ['Item With Spaces', 'item_with_underscore', 'ITEM-WITH-DASH'];

      api.respondOnce({ ok: true, json: async () => mockData });

      const result = await getGenres(Entity.Books, api.deps);

      expect(result[0]).toEqual({ label: 'Item With Spaces', value: 'Item With Spaces' });
      expect(result[1]).toEqual({ label: 'item_with_underscore', value: 'item_with_underscore' });
      expect(result[2]).toEqual({ label: 'ITEM-WITH-DASH', value: 'ITEM-WITH-DASH' });
    });

    it('should maintain order of items from API response', async (t) => {
      const api = createApiFakes(t);
      const mockData = ['Z', 'A', 'M', 'B'];

      api.respondOnce({ ok: true, json: async () => mockData });

      const result = await getFormats(Entity.Movies, api.deps);

      expect(result[0].value).toBe('Z');
      expect(result[1].value).toBe('A');
      expect(result[2].value).toBe('M');
      expect(result[3].value).toBe('B');
    });
  });

  describe('Error cases', () => {
    it('should throw error when fetch fails', async (t) => {
      const api = createApiFakes(t);
      api.rejectOnce(new Error('Network error'));

      await expect(getAuthors(api.deps)).rejects.toThrow('Network error');
    });

    it('should handle fetch failure for all metadata functions', async (t) => {
      const api = createApiFakes(t);
      api.rejectOnce(new Error('Connection failed'));

      await expect(getPublishers(api.deps)).rejects.toThrow('Connection failed');

      api.clearCalls();
      api.rejectOnce(new Error('Connection failed'));

      await expect(getStudios(api.deps)).rejects.toThrow('Connection failed');

      api.clearCalls();
      api.rejectOnce(new Error('Connection failed'));

      await expect(getDevelopers(api.deps)).rejects.toThrow('Connection failed');
    });

    it('should handle JSON parsing errors', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({
        ok: true,
        json: async () => {
          throw new Error('Invalid JSON');
        },
      });

      await expect(getGenres(Entity.Books, api.deps)).rejects.toThrow('Invalid JSON');
    });

    it('should throw error when API returns undefined', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => undefined });

      await expect(getAuthors(api.deps)).rejects.toThrow();
    });

    it('should throw error when API returns null', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => null });

      await expect(getPublishers(api.deps)).rejects.toThrow();
    });

    it('should throw error if fetch is called without response being ok (implicit)', async (t) => {
      const api = createApiFakes(t);
      // The current implementation doesn't explicitly check response.ok,
      // but it should gracefully handle invalid JSON responses
      api.respondOnce({
        ok: true,
        json: async () => {
          throw new TypeError('Failed to parse JSON');
        },
      });

      await expect(getStudios(api.deps)).rejects.toThrow();
    });
  });

  describe('API endpoint construction', () => {
    it('should construct correct endpoint for book authors', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getAuthors(api.deps);

      expectFetched(api.apiFetch, `${Entity.Books}/authors`);
    });

    it('should construct correct endpoint for book publishers', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getPublishers(api.deps);

      expectFetched(api.apiFetch, `${Entity.Books}/publisher`);
    });

    it('should construct correct endpoints for different entity types', async (t) => {
      const api = createApiFakes(t);
      const testCases = [
        { fn: () => getGenres(Entity.Books, api.deps), entity: Entity.Books, property: 'genres' },
        { fn: () => getGenres(Entity.Movies, api.deps), entity: Entity.Movies, property: 'genres' },
        { fn: () => getGenres(Entity.Games, api.deps), entity: Entity.Games, property: 'genres' },
        { fn: () => getGenres(Entity.Musics, api.deps), entity: Entity.Musics, property: 'genres' },
        { fn: () => getFormats(Entity.Books, api.deps), entity: Entity.Books, property: 'format' },
        { fn: () => getFormats(Entity.Movies, api.deps), entity: Entity.Movies, property: 'format' },
        { fn: () => getFormats(Entity.Games, api.deps), entity: Entity.Games, property: 'format' },
        { fn: () => getFormats(Entity.Musics, api.deps), entity: Entity.Musics, property: 'format' },
      ];

      for (const testCase of testCases) {
        api.respondOnce({ ok: true, json: async () => [] });

        await testCase.fn();

        expectFetched(api.apiFetch, `${testCase.entity}/${testCase.property}`);

        api.clearCalls();
      }
    });

    it('should use baseUrl from constants in fetch call', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getAuthors(api.deps);

      const calls = api.apiFetch.mock.calls;
      expect(calls[0].arguments[0]).toContain('metadata');
      expect(calls[0].arguments[0]).toContain(Entity.Books);
    });

    it('should construct endpoints for game-specific functions', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getDevelopers(api.deps);

      expectFetched(api.apiFetch, `${Entity.Games}/developers`);

      api.clearCalls();
      api.respondOnce({ ok: true, json: async () => [] });

      await getPlatforms(api.deps);

      expectFetched(api.apiFetch, `${Entity.Games}/platform`);

      api.clearCalls();
      api.respondOnce({ ok: true, json: async () => [] });

      await getGamePublishers(api.deps);

      expectFetched(api.apiFetch, `${Entity.Games}/publishers`);
    });

    it('should construct endpoints for music-specific functions', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getLabels(api.deps);

      expectFetched(api.apiFetch, `${Entity.Musics}/label`);

      api.clearCalls();
      api.respondOnce({ ok: true, json: async () => [] });

      await getArtist(api.deps);

      expectFetched(api.apiFetch, `${Entity.Musics}/artist`);
    });

    it('should construct endpoints for movie-specific functions', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => [] });

      await getStudios(api.deps);

      expectFetched(api.apiFetch, `${Entity.Movies}/studios`);
    });
  });

  describe('Integration scenarios', () => {
    it('should fetch metadata for all functions in sequence', async (t) => {
      const api = createApiFakes(t);
      const functions = [
        { fn: () => getAuthors(api.deps), name: 'getAuthors' },
        { fn: () => getPublishers(api.deps), name: 'getPublishers' },
        { fn: () => getGenres(Entity.Books, api.deps), name: 'getGenres(Books)' },
        { fn: () => getStudios(api.deps), name: 'getStudios' },
        { fn: () => getDevelopers(api.deps), name: 'getDevelopers' },
        { fn: () => getPlatforms(api.deps), name: 'getPlatforms' },
        { fn: () => getLabels(api.deps), name: 'getLabels' },
        { fn: () => getGamePublishers(api.deps), name: 'getGamePublishers' },
        { fn: () => getArtist(api.deps), name: 'getArtist' },
      ];

      for (const testCase of functions) {
        api.respondOnce({ ok: true, json: async () => ['Item 1', 'Item 2'] });

        const result = await testCase.fn();

        expect(result).toHaveLength(2);
        expect(result[0]).toHaveProperty('label');
        expect(result[0]).toHaveProperty('value');

        api.clearCalls();
      }
    });

    it('should handle mixed success and error scenarios', async (t) => {
      const api = createApiFakes(t);
      api.respondOnce({ ok: true, json: async () => ['Author 1'] });

      const authorsResult = await getAuthors(api.deps);
      expect(authorsResult).toHaveLength(1);

      api.clearCalls();
      api.rejectOnce(new Error('API Error'));

      await expect(getPublishers(api.deps)).rejects.toThrow('API Error');
    });

    it('should return consistently formatted responses', async (t) => {
      const api = createApiFakes(t);
      const mockData = ['Data 1', 'Data 2', 'Data 3'];

      api.respondOnce({ ok: true, json: async () => mockData });

      const result1 = await getAuthors(api.deps);

      api.clearCalls();
      api.respondOnce({ ok: true, json: async () => mockData });

      const result2 = await getStudios(api.deps);

      expect(result1).toEqual(result2);
    });
  });
});
