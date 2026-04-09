import { describe, it, expect, beforeEach, vi } from 'vitest';
import { meta, loader } from './route';
import * as entityData from '~/api/entity-data';
import * as helpers from '~/utils/helpers';
import { Entity, BookEntity, MovieEntity, GameEntity, MusicEntity } from '~/models';

// Mock modules
vi.mock('~/api/entity-data');
vi.mock('~/utils/helpers');

const mockPagedSearchEntities = vi.mocked(entityData.pagedSearchEntities);
const mockGetEntityFromParams = vi.mocked(helpers.getEntityFromParams);

function makePagedResult<T>(items: T[]) {
  return { items, totalCount: items.length, page: 1, pageSize: 25, totalPages: 1 };
}

describe('$entity._index route', () => {
  describe('meta function', () => {
    it('should return correct title and description for Books', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      const result = meta({ params: { entity: 'books' } } as unknown as Parameters<typeof loader>[0]);
      expect(result).toContainEqual(expect.objectContaining({ title: expect.stringContaining('List') }));
      expect(result).toContainEqual(expect.objectContaining({ name: 'description' }));
    });

    it('should return correct title and description for Movies', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      const result = meta({ params: { entity: 'movies' } } as unknown as Parameters<typeof loader>[0]);
      expect(result).toContainEqual(expect.objectContaining({ title: expect.stringContaining('List') }));
    });

    it('should return correct title and description for Games', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      const result = meta({ params: { entity: 'games' } } as unknown as Parameters<typeof loader>[0]);
      expect(result).toContainEqual(expect.objectContaining({ title: expect.stringContaining('List') }));
    });

    it('should return correct title and description for Musics', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      const result = meta({ params: { entity: 'musics' } } as unknown as Parameters<typeof loader>[0]);
      expect(result).toContainEqual(expect.objectContaining({ title: expect.stringContaining('List') }));
    });

    it('should have 2 meta tags', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      const result = meta({ params: { entity: 'books' } } as unknown as Parameters<typeof loader>[0]);
      expect(result).toHaveLength(2);
    });
  });

  describe('loader function', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should load books with search text', async () => {
      const mockBooks: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'Test Book',
          authors: ['Author One'],
        },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult(mockBooks));

      const mockRequest = new Request('http://localhost/books?searchText=test');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Books, 'test', 'title:asc', 1);
      expect(result).toEqual({
        entities: mockBooks,
        pagination: { page: 1, totalPages: 1, totalCount: mockBooks.length },
        entityType: Entity.Books,
        searchText: 'test',
        orderBy: 'title:asc',
        apiUrl: expect.any(String),
      });
    });

    it('should load movies without search text', async () => {
      const mockMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Test Movie',
        },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult(mockMovies));

      const mockRequest = new Request('http://localhost/movies');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'movies' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Movies, null, 'title:asc', 1);
      expect(result).toEqual({
        entities: mockMovies,
        pagination: { page: 1, totalPages: 1, totalCount: mockMovies.length },
        entityType: Entity.Movies,
        searchText: null,
        orderBy: 'title:asc',
        apiUrl: expect.any(String),
      });
    });

    it('should load games with search text', async () => {
      const mockGames: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
        },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult(mockGames));

      const mockRequest = new Request('http://localhost/games?searchText=action');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'games' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Games, 'action', 'title:asc', 1);
      expect(result).toEqual({
        entities: mockGames,
        pagination: { page: 1, totalPages: 1, totalCount: mockGames.length },
        entityType: Entity.Games,
        searchText: 'action',
        orderBy: 'title:asc',
        apiUrl: expect.any(String),
      });
    });

    it('should load musics with search text', async () => {
      const mockMusics: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: '1',
          title: 'Test Album',
          artist: 'Test Artist',
        },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult(mockMusics));

      const mockRequest = new Request('http://localhost/musics?searchText=rock');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'musics' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Musics, 'rock', 'title:asc', 1);
      expect(result).toEqual({
        entities: mockMusics,
        pagination: { page: 1, totalPages: 1, totalCount: mockMusics.length },
        entityType: Entity.Musics,
        searchText: 'rock',
        orderBy: 'title:asc',
        apiUrl: expect.any(String),
      });
    });

    it('should handle empty search results', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/books?searchText=nonexistent');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(result).toEqual({
        entities: [],
        pagination: { page: 1, totalPages: 1, totalCount: 0 },
        entityType: Entity.Books,
        searchText: 'nonexistent',
        orderBy: 'title:asc',
        apiUrl: expect.any(String),
      });
    });

    it('should throw error if entity param is missing', async () => {
      const mockRequest = new Request('http://localhost/test');

      await expect(
        loader({
          request: mockRequest,
          params: {},
        } as unknown as Parameters<typeof loader>[0])
      ).rejects.toThrow();
    });

    it('should handle multiple results from search', async () => {
      const mockBooks: BookEntity[] = [
        { type: Entity.Books, id: '1', title: 'Book 1', authors: ['Author 1'] },
        { type: Entity.Books, id: '2', title: 'Book 2', authors: ['Author 2'] },
        { type: Entity.Books, id: '3', title: 'Book 3', authors: ['Author 3'] },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult(mockBooks));

      const mockRequest = new Request('http://localhost/books?searchText=book');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(result.entities).toHaveLength(3);
      expect(result.entities).toEqual(mockBooks);
    });

    it('should pass correct entity type to searchEntities', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/games?searchText=zelda');
      await loader({
        request: mockRequest,
        params: { entity: 'games' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockGetEntityFromParams).toHaveBeenCalledWith({ entity: 'games' });
      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Games, 'zelda', 'title:asc', 1);
    });

    it('should extract search text from URL query parameters', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/books?searchText=multiple%20words');
      await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Books, 'multiple words', 'title:asc', 1);
    });

    it('should handle null search text when not provided', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/movies');
      await loader({
        request: mockRequest,
        params: { entity: 'movies' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Movies, null, 'title:asc', 1);
    });

    it('should pass orderBy from URL to searchEntities', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/books?orderBy=format:desc');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Books, null, 'format:desc', 1);
      expect(result).toMatchObject({ orderBy: 'format:desc' });
    });

    it('should default orderBy to title:asc when not provided', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockPagedSearchEntities.mockResolvedValue(makePagedResult([]));

      const mockRequest = new Request('http://localhost/books?searchText=test');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as unknown as Parameters<typeof loader>[0]);

      expect(mockPagedSearchEntities).toHaveBeenCalledWith(Entity.Books, 'test', 'title:asc', 1);
      expect(result).toMatchObject({ orderBy: 'title:asc' });
    });
  });
});
