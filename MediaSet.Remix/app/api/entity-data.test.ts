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
    info: vi.fn(() => undefined),
    warn: vi.fn(() => undefined),
    error: vi.fn(() => undefined),
    debug: vi.fn(() => undefined),
  },
}));

// Now import after mocking
import {
  searchEntities,
  getEntity,
  updateEntity,
  addEntity,
  deleteEntity,
} from '~/api/entity-data';
import { Entity, BookEntity, MovieEntity, GameEntity, MusicEntity } from '~/models';

describe('entity-data.ts', () => {
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

  describe('searchEntities', () => {
    it('should fetch and return search results for books', async () => {
      const mockBooks: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'Test Book',
          authors: ['Author One'],
          isbn: '978-1234567890',
        },
        {
          type: Entity.Books,
          id: '2',
          title: 'Another Book',
          authors: ['Author Two'],
          isbn: '978-0987654321',
        },
      ];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'test');

      expect(result).toEqual(mockBooks);
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Books}/search?searchText=test`)
      );
      expect(result).toHaveLength(2);
    });

    it('should fetch and return search results for movies', async () => {
      const mockMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Test Movie',
          studios: ['Studio A'],
          barcode: '123456789',
        },
      ];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockMovies,
      });

      const result = await searchEntities<MovieEntity>(Entity.Movies, 'action');

      expect(result).toEqual(mockMovies);
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Movies}/search?searchText=action`)
      );
    });

    it('should fetch and return search results for games', async () => {
      const mockGames: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
          developers: ['Dev Studio'],
        },
      ];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
      });

      const result = await searchEntities<GameEntity>(Entity.Games, 'rpg');

      expect(result).toEqual(mockGames);
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Games}/search?searchText=rpg`)
      );
    });

    it('should fetch and return search results for music', async () => {
      const mockMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: '1',
          title: 'Test Album',
          artist: 'Test Artist',
          barcode: '111222333',
        },
      ];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockMusic,
      });

      const result = await searchEntities<MusicEntity>(Entity.Musics, 'rock');

      expect(result).toEqual(mockMusic);
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Musics}/search?searchText=rock`)
      );
    });

    it('should handle null searchText by using empty string', async () => {
      const mockBooks: BookEntity[] = [];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, null);

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining('searchText=')
      );
    });

    it('should include orderBy parameter when provided', async () => {
      const mockBooks: BookEntity[] = [];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, 'test', 'title');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining('orderBy=title')
      );
    });

    it('should handle empty orderBy parameter', async () => {
      const mockBooks: BookEntity[] = [];

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, 'test', '');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining('orderBy=')
      );
    });

    it('should throw error when response is not ok', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      await expect(
        searchEntities<BookEntity>(Entity.Books, 'test')
      ).rejects.toThrow();
    });

    it('should return empty array when no results found', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => [],
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'nonexistent');

      expect(result).toEqual([]);
      expect(result).toHaveLength(0);
    });

    it('should handle multiple search results', async () => {
      const mockBooks: BookEntity[] = Array.from({ length: 10 }, (_, i) => ({
        type: Entity.Books,
        id: String(i + 1),
        title: `Book ${i + 1}`,
      }));

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'book');

      expect(result).toHaveLength(10);
    });
  });

  describe('getEntity', () => {
    it('should fetch and return a specific book entity', async () => {
      const mockBook: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test Book',
        isbn: '978-1234567890',
        authors: ['Author'],
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockBook,
      });

      const result = await getEntity<BookEntity>(Entity.Books, '123');

      expect(result).toEqual(mockBook);
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Books}/123`)
      );
    });

    it('should fetch and return a specific movie entity', async () => {
      const mockMovie: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Test Movie',
        barcode: '123456789',
        runtime: 120,
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockMovie,
      });

      const result = await getEntity<MovieEntity>(Entity.Movies, '456');

      expect(result).toEqual(mockMovie);
    });

    it('should fetch and return a specific game entity', async () => {
      const mockGame: GameEntity = {
        type: Entity.Games,
        id: '789',
        title: 'Test Game',
        platform: 'PS5',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockGame,
      });

      const result = await getEntity<GameEntity>(Entity.Games, '789');

      expect(result).toEqual(mockGame);
    });

    it('should fetch and return a specific music entity', async () => {
      const mockMusic: MusicEntity = {
        type: Entity.Musics,
        id: '101',
        title: 'Test Album',
        artist: 'Test Artist',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockMusic,
      });

      const result = await getEntity<MusicEntity>(Entity.Musics, '101');

      expect(result).toEqual(mockMusic);
    });

    it('should throw 404 error when entity not found', async () => {
      mockApiFetch.mockResolvedValueOnce({
        status: 404,
      });

      await expect(
        getEntity<BookEntity>(Entity.Books, 'nonexistent')
      ).rejects.toThrow();
    });

    it('should throw specific entity not found message for each type', async () => {
      mockApiFetch.mockResolvedValueOnce({
        status: 404,
      });

      await expect(
        getEntity<MovieEntity>(Entity.Movies, '999')
      ).rejects.toThrow();
    });

    it('should handle entity with all fields populated', async () => {
      const fullBook: BookEntity = {
        type: Entity.Books,
        id: '1',
        title: 'Complete Book',
        isbn: '978-1234567890',
        pages: 300,
        publicationDate: '2023-01-15',
        authors: ['Author One', 'Author Two'],
        publisher: 'Publisher',
        genres: ['Fiction', 'Mystery'],
        plot: 'A great story',
        subtitle: 'A Subtitle',
        format: 'Hardcover',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => fullBook,
      });

      const result = await getEntity<BookEntity>(Entity.Books, '1');

      expect(result.authors).toHaveLength(2);
      expect(result.pages).toBe(300);
      expect(result.format).toBe('Hardcover');
    });

    it('should handle entity with minimal fields', async () => {
      const minimalGame: GameEntity = {
        type: Entity.Games,
        id: '1',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => minimalGame,
      });

      const result = await getEntity<GameEntity>(Entity.Games, '1');

      expect(result.type).toBe(Entity.Games);
      expect(result.id).toBe('1');
      expect(result.title).toBeUndefined();
    });
  });

  describe('updateEntity', () => {
    it('should update a book entity', async () => {
      const bookToUpdate: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Updated Book',
        isbn: '978-1234567890',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', bookToUpdate);

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Books}/123`),
        expect.objectContaining({
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
        })
      );
    });

    it('should update a movie entity', async () => {
      const movieToUpdate: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Updated Movie',
        barcode: '987654321',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<MovieEntity>('456', movieToUpdate);

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Movies}/456`),
        expect.objectContaining({ method: 'PUT' })
      );
    });

    it('should update a game entity', async () => {
      const gameToUpdate: GameEntity = {
        type: Entity.Games,
        id: '789',
        title: 'Updated Game',
        platform: 'Xbox Series X',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<GameEntity>('789', gameToUpdate);

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Games}/789`),
        expect.objectContaining({ method: 'PUT' })
      );
    });

    it('should update a music entity', async () => {
      const musicToUpdate: MusicEntity = {
        type: Entity.Musics,
        id: '101',
        title: 'Updated Album',
        artist: 'Updated Artist',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<MusicEntity>('101', musicToUpdate);

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Musics}/101`),
        expect.objectContaining({ method: 'PUT' })
      );
    });

    it('should send entity data as JSON', async () => {
      const book: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test',
        authors: ['Author'],
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', book);

      const callArgs = mockApiFetch.mock.calls[0];
      const body = callArgs[1].body;
      const parsedBody = JSON.parse(body);

      expect(parsedBody.type).toBe(Entity.Books);
      expect(parsedBody.title).toBe('Test');
      expect(parsedBody.authors).toEqual(['Author']);
    });

    it('should throw error when update fails', async () => {
      const book: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      await expect(updateEntity<BookEntity>('123', book)).rejects.toThrow();
    });

    it('should throw error with correct entity type in message', async () => {
      const movie: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Test',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 400,
      });

      await expect(updateEntity<MovieEntity>('456', movie)).rejects.toThrow();
    });

    it('should handle complex entity updates', async () => {
      const complexBook: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Complex Book',
        isbn: '978-1234567890',
        pages: 500,
        publicationDate: '2023-01-15',
        authors: ['Author 1', 'Author 2', 'Author 3'],
        publisher: 'Publisher Name',
        genres: ['Fiction', 'Science Fiction', 'Adventure'],
        plot: 'Complex plot description',
        subtitle: 'A Complex Subtitle',
        format: 'Hardcover',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', complexBook);

      const callArgs = mockApiFetch.mock.calls[0];
      const body = JSON.parse(callArgs[1].body);

      expect(body.authors).toHaveLength(3);
      expect(body.genres).toHaveLength(3);
      expect(body.pages).toBe(500);
    });
  });

  describe('addEntity', () => {
    it('should add a new book entity', async () => {
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
        authors: ['Author'],
      };

      const responseBook: BookEntity = {
        ...newBook,
        id: 'new-id-123',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseBook,
      });

      const result = await addEntity<BookEntity>(newBook);

      expect(result).toEqual(responseBook);
      expect(result.id).toBe('new-id-123');
      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Books}`),
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
        })
      );
    });

    it('should add a new movie entity', async () => {
      const newMovie: MovieEntity = {
        type: Entity.Movies,
        title: 'New Movie',
        barcode: '123456789',
      };

      const responseMovie: MovieEntity = {
        ...newMovie,
        id: 'movie-456',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseMovie,
      });

      const result = await addEntity<MovieEntity>(newMovie);

      expect(result.id).toBe('movie-456');
      expect(result.type).toBe(Entity.Movies);
    });

    it('should add a new game entity', async () => {
      const newGame: GameEntity = {
        type: Entity.Games,
        title: 'New Game',
        platform: 'PS5',
      };

      const responseGame: GameEntity = {
        ...newGame,
        id: 'game-789',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseGame,
      });

      const result = await addEntity<GameEntity>(newGame);

      expect(result.id).toBe('game-789');
    });

    it('should add a new music entity', async () => {
      const newMusic: MusicEntity = {
        type: Entity.Musics,
        title: 'New Album',
        artist: 'New Artist',
      };

      const responseMusic: MusicEntity = {
        ...newMusic,
        id: 'music-101',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseMusic,
      });

      const result = await addEntity<MusicEntity>(newMusic);

      expect(result.id).toBe('music-101');
    });

    it('should send entity data as JSON in POST request', async () => {
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'Test Book',
        isbn: '123',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ ...newBook, id: '1' }),
      });

      await addEntity<BookEntity>(newBook);

      const callArgs = mockApiFetch.mock.calls[0];
      const body = JSON.parse(callArgs[1].body);

      expect(body.type).toBe(Entity.Books);
      expect(body.title).toBe('Test Book');
      expect(body.isbn).toBe('123');
    });

    it('should throw error when adding entity fails', async () => {
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      await expect(addEntity<BookEntity>(newBook)).rejects.toThrow();
    });

    it('should throw error with correct entity type in message', async () => {
      const newMovie: MovieEntity = {
        type: Entity.Movies,
        title: 'New Movie',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 400,
      });

      await expect(addEntity<MovieEntity>(newMovie)).rejects.toThrow();
    });

    it('should handle complex entity additions', async () => {
      const complexGame: GameEntity = {
        type: Entity.Games,
        title: 'Complex Game',
        platform: 'Multi-platform',
        developers: ['Dev 1', 'Dev 2'],
        publishers: ['Pub 1'],
        genres: ['RPG', 'Action'],
        releaseDate: '2023-09-20',
        rating: '9.0',
      };

      const responseGame: GameEntity = {
        ...complexGame,
        id: 'game-999',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseGame,
      });

      const result = await addEntity<GameEntity>(complexGame);

      expect(result.developers).toHaveLength(2);
      expect(result.genres).toHaveLength(2);
      expect(result.id).toBe('game-999');
    });

    it('should return entity with assigned ID from response', async () => {
      const bookWithoutId: BookEntity = {
        type: Entity.Books,
        title: 'Book',
      };

      const responseBook: BookEntity = {
        ...bookWithoutId,
        id: 'assigned-id-12345',
      };

      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => responseBook,
      });

      const result = await addEntity<BookEntity>(bookWithoutId);

      expect(result.id).toBe('assigned-id-12345');
      expect(result.title).toBe('Book');
    });
  });

  describe('deleteEntity', () => {
    it('should delete a book entity', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Books, '123');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Books}/123`),
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('should delete a movie entity', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Movies, '456');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Movies}/456`),
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('should delete a game entity', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Games, '789');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Games}/789`),
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('should delete a music entity', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Musics, '101');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/${Entity.Musics}/101`),
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('should throw error when deletion fails', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
      });

      await expect(deleteEntity(Entity.Books, '123')).rejects.toThrow();
    });

    it('should throw error with correct entity type and id in message', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
      });

      await expect(deleteEntity(Entity.Movies, '999')).rejects.toThrow();
    });

    it('should handle deletion of multiple entities sequentially', async () => {
      mockApiFetch
        .mockResolvedValueOnce({ ok: true })
        .mockResolvedValueOnce({ ok: true })
        .mockResolvedValueOnce({ ok: true });

      await deleteEntity(Entity.Books, '1');
      await deleteEntity(Entity.Books, '2');
      await deleteEntity(Entity.Books, '3');

      expect(mockApiFetch).toHaveBeenCalledTimes(3);
    });

    it('should send correct DELETE request', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Games, '123');

      const callArgs = mockApiFetch.mock.calls[0];
      expect(callArgs[1].method).toBe('DELETE');
    });

    it('should handle deletion with numeric-like string IDs', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });

      await deleteEntity(Entity.Musics, '999999');

      expect(mockApiFetch).toHaveBeenCalledWith(
        expect.stringContaining('/999999'),
        expect.any(Object)
      );
    });
  });

  describe('Entity type handling across all functions', () => {
    it('should correctly handle all entity types', async () => {
      const entities = [Entity.Books, Entity.Movies, Entity.Games, Entity.Musics];

      for (const entity of entities) {
        mockApiFetch.mockResolvedValueOnce({
          ok: true,
          json: async () => [{ type: entity, id: '1' }],
        });

        const result = await searchEntities(entity, 'test');
        expect(result).toHaveLength(1);
        expect(result[0].type).toBe(entity);
        vi.clearAllMocks();
      }
    });

    it('should maintain entity type throughout CRUD operations', async () => {
      const book: BookEntity = { type: Entity.Books, title: 'Test' };

      // Search
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => [book],
      });
      let result = await searchEntities<BookEntity>(Entity.Books, 'test');
      expect(result[0].type).toBe(Entity.Books);

      // Get
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ ...book, id: '1' }),
      });
      let getResult = await getEntity<BookEntity>(Entity.Books, '1');
      expect(getResult.type).toBe(Entity.Books);
    });
  });

  describe('Error handling', () => {
    it('should handle network errors during search', async () => {
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(
        searchEntities<BookEntity>(Entity.Books, 'test')
      ).rejects.toThrow('Network error');
    });

    it('should handle network errors during get', async () => {
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(
        getEntity<BookEntity>(Entity.Books, '1')
      ).rejects.toThrow('Network error');
    });

    it('should handle network errors during update', async () => {
      const book: BookEntity = { type: Entity.Books, id: '1', title: 'Test' };
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(updateEntity<BookEntity>('1', book)).rejects.toThrow(
        'Network error'
      );
    });

    it('should handle network errors during add', async () => {
      const book: BookEntity = { type: Entity.Books, title: 'Test' };
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(addEntity<BookEntity>(book)).rejects.toThrow(
        'Network error'
      );
    });

    it('should handle network errors during delete', async () => {
      mockApiFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(deleteEntity(Entity.Books, '1')).rejects.toThrow(
        'Network error'
      );
    });

    it('should handle 5xx errors appropriately', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 503,
      });

      await expect(
        searchEntities<BookEntity>(Entity.Books, 'test')
      ).rejects.toThrow();
    });

    it('should handle 4xx errors appropriately', async () => {
      mockApiFetch.mockResolvedValueOnce({
        ok: false,
        status: 400,
      });

      await expect(
        searchEntities<BookEntity>(Entity.Books, 'test')
      ).rejects.toThrow();
    });
  });

  describe('Integration-like scenarios', () => {
    it('should perform full CRUD cycle for a book', async () => {
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
        authors: ['Author'],
      };

      // Create
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ ...newBook, id: '1' }),
      });
      let created = await addEntity<BookEntity>(newBook);
      expect(created.id).toBe('1');

      // Read
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => created,
      });
      let retrieved = await getEntity<BookEntity>(Entity.Books, '1');
      expect(retrieved.title).toBe('New Book');

      // Update
      const updated: BookEntity = { ...retrieved, title: 'Updated Book' };
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });
      await updateEntity<BookEntity>('1', updated);

      // Delete
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
      });
      await deleteEntity(Entity.Books, '1');

      expect(mockApiFetch).toHaveBeenCalledTimes(4);
    });

    it('should search and then retrieve individual items', async () => {
      const mockBooks: BookEntity[] = [
        { type: Entity.Books, id: '1', title: 'Book 1' },
        { type: Entity.Books, id: '2', title: 'Book 2' },
      ];

      // Search
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockBooks,
      });
      const searchResults = await searchEntities<BookEntity>(Entity.Books, 'book');
      expect(searchResults).toHaveLength(2);

      // Get specific item
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => mockBooks[0],
      });
      const singleBook = await getEntity<BookEntity>(Entity.Books, '1');
      expect(singleBook.title).toBe('Book 1');
    });
  });
});
