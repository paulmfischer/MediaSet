import { describe, it, expect, beforeEach, vi } from 'vitest';
import { meta, loader } from './route';
import * as entityData from '~/entity-data';
import * as helpers from '~/helpers';
import { Entity, BookEntity, MovieEntity, GameEntity, MusicEntity } from '~/models';

// Mock modules
vi.mock('~/entity-data');
vi.mock('~/helpers');

const mockSearchEntities = vi.mocked(entityData.searchEntities);
const mockGetEntityFromParams = vi.mocked(helpers.getEntityFromParams);

describe('$entity._index route', () => {
  describe('meta function', () => {
    it('should return correct title and description for Books', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      const result = meta({ params: { entity: 'books' } } as any);
      expect(result).toContainEqual(
        expect.objectContaining({ title: expect.stringContaining('List') })
      );
      expect(result).toContainEqual(
        expect.objectContaining({ name: 'description' })
      );
    });

    it('should return correct title and description for Movies', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      const result = meta({ params: { entity: 'movies' } } as any);
      expect(result).toContainEqual(
        expect.objectContaining({ title: expect.stringContaining('List') })
      );
    });

    it('should return correct title and description for Games', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      const result = meta({ params: { entity: 'games' } } as any);
      expect(result).toContainEqual(
        expect.objectContaining({ title: expect.stringContaining('List') })
      );
    });

    it('should return correct title and description for Musics', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      const result = meta({ params: { entity: 'musics' } } as any);
      expect(result).toContainEqual(
        expect.objectContaining({ title: expect.stringContaining('List') })
      );
    });

    it('should have 2 meta tags', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      const result = meta({ params: { entity: 'books' } } as any);
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
      mockSearchEntities.mockResolvedValue(mockBooks);

      const mockRequest = new Request('http://localhost/books?searchText=test');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Books, 'test');
      expect(result).toEqual({ entities: mockBooks, entityType: Entity.Books });
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
      mockSearchEntities.mockResolvedValue(mockMovies);

      const mockRequest = new Request('http://localhost/movies');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'movies' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Movies, null);
      expect(result).toEqual({ entities: mockMovies, entityType: Entity.Movies });
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
      mockSearchEntities.mockResolvedValue(mockGames);

      const mockRequest = new Request('http://localhost/games?searchText=action');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'games' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Games, 'action');
      expect(result).toEqual({ entities: mockGames, entityType: Entity.Games });
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
      mockSearchEntities.mockResolvedValue(mockMusics);

      const mockRequest = new Request('http://localhost/musics?searchText=rock');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'musics' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Musics, 'rock');
      expect(result).toEqual({ entities: mockMusics, entityType: Entity.Musics });
    });

    it('should handle empty search results', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSearchEntities.mockResolvedValue([]);

      const mockRequest = new Request('http://localhost/books?searchText=nonexistent');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as any);

      expect(result).toEqual({ entities: [], entityType: Entity.Books });
    });

    it('should throw error if entity param is missing', async () => {
      const mockRequest = new Request('http://localhost/test');
      
      await expect(
        loader({
          request: mockRequest,
          params: {},
        } as any)
      ).rejects.toThrow();
    });

    it('should handle multiple results from search', async () => {
      const mockBooks: BookEntity[] = [
        { type: Entity.Books, id: '1', title: 'Book 1', authors: ['Author 1'] },
        { type: Entity.Books, id: '2', title: 'Book 2', authors: ['Author 2'] },
        { type: Entity.Books, id: '3', title: 'Book 3', authors: ['Author 3'] },
      ];

      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSearchEntities.mockResolvedValue(mockBooks);

      const mockRequest = new Request('http://localhost/books?searchText=book');
      const result = await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as any);

      expect(result.entities).toHaveLength(3);
      expect(result.entities).toEqual(mockBooks);
    });

    it('should pass correct entity type to searchEntities', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockSearchEntities.mockResolvedValue([]);

      const mockRequest = new Request('http://localhost/games?searchText=zelda');
      await loader({
        request: mockRequest,
        params: { entity: 'games' },
      } as any);

      expect(mockGetEntityFromParams).toHaveBeenCalledWith({ entity: 'games' });
      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Games, 'zelda');
    });

    it('should extract search text from URL query parameters', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSearchEntities.mockResolvedValue([]);

      const mockRequest = new Request('http://localhost/books?searchText=multiple%20words');
      await loader({
        request: mockRequest,
        params: { entity: 'books' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Books, 'multiple words');
    });

    it('should handle null search text when not provided', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockSearchEntities.mockResolvedValue([]);

      const mockRequest = new Request('http://localhost/movies');
      await loader({
        request: mockRequest,
        params: { entity: 'movies' },
      } as any);

      expect(mockSearchEntities).toHaveBeenCalledWith(Entity.Movies, null);
    });
  });
});

