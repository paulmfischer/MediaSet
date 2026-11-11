import { expect } from 'remix/assert';
import { describe, it } from 'remix/test';

import { mockFetch, expectFetched } from '../test/fetch-fakes.ts';
import { searchEntities, getEntity, updateEntity, addEntity, deleteEntity } from './entity-data.ts';
import { Entity, type BookEntity, type MovieEntity, type GameEntity, type MusicEntity } from '../models.ts';

describe('entity-data.ts', () => {
  describe('searchEntities', () => {
    it('should fetch and return search results for books', async (t) => {
      const api = mockFetch(t);
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

      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'test');

      expect(result).toEqual(mockBooks);
      expectFetched(api.apiFetch, `/${Entity.Books}/search?searchText=test`);
      expect(result).toHaveLength(2);
    });

    it('should fetch and return search results for movies', async (t) => {
      const api = mockFetch(t);
      const mockMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Test Movie',
          studios: ['Studio A'],
          barcode: '123456789',
        },
      ];

      api.respondOnce({
        ok: true,
        json: async () => mockMovies,
      });

      const result = await searchEntities<MovieEntity>(Entity.Movies, 'action');

      expect(result).toEqual(mockMovies);
      expectFetched(api.apiFetch, `/${Entity.Movies}/search?searchText=action`);
    });

    it('should fetch and return search results for games', async (t) => {
      const api = mockFetch(t);
      const mockGames: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
          developers: ['Dev Studio'],
        },
      ];

      api.respondOnce({
        ok: true,
        json: async () => mockGames,
      });

      const result = await searchEntities<GameEntity>(Entity.Games, 'rpg');

      expect(result).toEqual(mockGames);
      expectFetched(api.apiFetch, `/${Entity.Games}/search?searchText=rpg`);
    });

    it('should fetch and return search results for music', async (t) => {
      const api = mockFetch(t);
      const mockMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: '1',
          title: 'Test Album',
          artist: 'Test Artist',
          barcode: '111222333',
        },
      ];

      api.respondOnce({
        ok: true,
        json: async () => mockMusic,
      });

      const result = await searchEntities<MusicEntity>(Entity.Musics, 'rock');

      expect(result).toEqual(mockMusic);
      expectFetched(api.apiFetch, `/${Entity.Musics}/search?searchText=rock`);
    });

    it('should handle null searchText by using empty string', async (t) => {
      const api = mockFetch(t);
      const mockBooks: BookEntity[] = [];

      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, null);

      expectFetched(api.apiFetch, 'searchText=');
    });

    it('should include orderBy parameter when provided', async (t) => {
      const api = mockFetch(t);
      const mockBooks: BookEntity[] = [];

      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, 'test', 'title');

      expectFetched(api.apiFetch, 'orderBy=title');
    });

    it('should handle empty orderBy parameter', async (t) => {
      const api = mockFetch(t);
      const mockBooks: BookEntity[] = [];

      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });

      await searchEntities<BookEntity>(Entity.Books, 'test', '');

      expectFetched(api.apiFetch, 'orderBy=');
    });

    it('should throw error when response is not ok', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 500,
      });

      await expect(searchEntities<BookEntity>(Entity.Books, 'test')).rejects.toThrow();
    });

    it('should return empty array when no results found', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
        json: async () => [],
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'nonexistent');

      expect(result).toEqual([]);
      expect(result).toHaveLength(0);
    });

    it('should handle multiple search results', async (t) => {
      const api = mockFetch(t);
      const mockBooks: BookEntity[] = Array.from({ length: 10 }, (_, i) => ({
        type: Entity.Books,
        id: String(i + 1),
        title: `Book ${i + 1}`,
      }));

      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });

      const result = await searchEntities<BookEntity>(Entity.Books, 'book');

      expect(result).toHaveLength(10);
    });
  });

  describe('getEntity', () => {
    it('should fetch and return a specific book entity', async (t) => {
      const api = mockFetch(t);
      const mockBook: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test Book',
        isbn: '978-1234567890',
        authors: ['Author'],
      };

      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockBook,
      });

      const result = await getEntity<BookEntity>(Entity.Books, '123');

      expect(result).toEqual(mockBook);
      expectFetched(api.apiFetch, `/${Entity.Books}/123`);
    });

    it('should fetch and return a specific movie entity', async (t) => {
      const api = mockFetch(t);
      const mockMovie: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Test Movie',
        barcode: '123456789',
        runtime: 120,
      };

      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockMovie,
      });

      const result = await getEntity<MovieEntity>(Entity.Movies, '456');

      expect(result).toEqual(mockMovie);
    });

    it('should fetch and return a specific game entity', async (t) => {
      const api = mockFetch(t);
      const mockGame: GameEntity = {
        type: Entity.Games,
        id: '789',
        title: 'Test Game',
        platform: 'PS5',
      };

      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockGame,
      });

      const result = await getEntity<GameEntity>(Entity.Games, '789');

      expect(result).toEqual(mockGame);
    });

    it('should fetch and return a specific music entity', async (t) => {
      const api = mockFetch(t);
      const mockMusic: MusicEntity = {
        type: Entity.Musics,
        id: '101',
        title: 'Test Album',
        artist: 'Test Artist',
      };

      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockMusic,
      });

      const result = await getEntity<MusicEntity>(Entity.Musics, '101');

      expect(result).toEqual(mockMusic);
    });

    it('should throw 404 error when entity not found', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        status: 404,
      });

      await expect(getEntity<BookEntity>(Entity.Books, 'nonexistent')).rejects.toThrow();
    });

    it('should throw specific entity not found message for each type', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        status: 404,
      });

      await expect(getEntity<MovieEntity>(Entity.Movies, '999')).rejects.toThrow();
    });

    it('should handle entity with all fields populated', async (t) => {
      const api = mockFetch(t);
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

      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => fullBook,
      });

      const result = await getEntity<BookEntity>(Entity.Books, '1');

      expect(result.authors).toHaveLength(2);
      expect(result.pages).toBe(300);
      expect(result.format).toBe('Hardcover');
    });

    it('should handle entity with minimal fields', async (t) => {
      const api = mockFetch(t);
      const minimalGame: GameEntity = {
        type: Entity.Games,
        id: '1',
      };

      api.respondOnce({
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
    it('should return the image error code when the API saved the entity without its cover image', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({ ok: true, status: 200, json: async () => ({ imageError: 'upload_too_large' }) });

      const result = await updateEntity<BookEntity>('123', { type: Entity.Books, id: '123', title: 'B' });

      expect(result).toEqual({ imageError: 'upload_too_large' });
    });

    it('should return no image error when the response body is empty or not JSON', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => {
          throw new SyntaxError('Unexpected end of JSON input');
        },
      });

      const result = await updateEntity<BookEntity>('123', { type: Entity.Books, id: '123', title: 'B' });

      expect(result.imageError).toBeUndefined();
    });

    it('should update a book entity', async (t) => {
      const api = mockFetch(t);
      const bookToUpdate: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Updated Book',
        isbn: '978-1234567890',
      };

      api.respondOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', bookToUpdate);

      expectFetched(
        api.apiFetch,
        `/${Entity.Books}/123`,
        expect.objectContaining({
          method: 'PUT',
          headers: { 'content-type': 'application/json' },
        })
      );
    });

    it('should update a movie entity', async (t) => {
      const api = mockFetch(t);
      const movieToUpdate: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Updated Movie',
        barcode: '987654321',
      };

      api.respondOnce({
        ok: true,
      });

      await updateEntity<MovieEntity>('456', movieToUpdate);

      expectFetched(api.apiFetch, `/${Entity.Movies}/456`, expect.objectContaining({ method: 'PUT' }));
    });

    it('should update a game entity', async (t) => {
      const api = mockFetch(t);
      const gameToUpdate: GameEntity = {
        type: Entity.Games,
        id: '789',
        title: 'Updated Game',
        platform: 'Xbox Series X',
      };

      api.respondOnce({
        ok: true,
      });

      await updateEntity<GameEntity>('789', gameToUpdate);

      expectFetched(api.apiFetch, `/${Entity.Games}/789`, expect.objectContaining({ method: 'PUT' }));
    });

    it('should update a music entity', async (t) => {
      const api = mockFetch(t);
      const musicToUpdate: MusicEntity = {
        type: Entity.Musics,
        id: '101',
        title: 'Updated Album',
        artist: 'Updated Artist',
      };

      api.respondOnce({
        ok: true,
      });

      await updateEntity<MusicEntity>('101', musicToUpdate);

      expectFetched(api.apiFetch, `/${Entity.Musics}/101`, expect.objectContaining({ method: 'PUT' }));
    });

    it('should send entity data as JSON', async (t) => {
      const api = mockFetch(t);
      const book: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test',
        authors: ['Author'],
      };

      api.respondOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', book);

      const callArgs = api.apiFetch.mock.calls[0].arguments;
      const body = callArgs[1].body;
      const parsedBody = JSON.parse(body);

      expect(parsedBody.type).toBe(Entity.Books);
      expect(parsedBody.title).toBe('Test');
      expect(parsedBody.authors).toEqual(['Author']);
    });

    it('should keep the status and message when the API rejects the update', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({ ok: false, status: 400, text: async () => ' ISBN already exists ' });

      const error = await updateEntity<BookEntity>('123', { type: Entity.Books }).catch((e: unknown) => e);

      expect(error).toBeInstanceOf(Response);
      expect((error as Response).status).toBe(400);
      expect(await (error as Response).text()).toBe('ISBN already exists');
    });

    it('should show the detail of a problem-details rejection of the update', async (t) => {
      const api = mockFetch(t);
      const problem = { title: 'Invalid ExternalId', status: 400, detail: "ExternalId must be 'source:value'." };
      api.respondOnce({ ok: false, status: 400, text: async () => JSON.stringify(problem) });

      const error = await updateEntity<MovieEntity>('123', { type: Entity.Movies }).catch((e: unknown) => e);

      expect((error as Response).status).toBe(400);
      expect(await (error as Response).text()).toBe("ExternalId must be 'source:value'.");
    });

    it('should throw error when update fails', async (t) => {
      const api = mockFetch(t);
      const book: BookEntity = {
        type: Entity.Books,
        id: '123',
        title: 'Test',
      };

      api.respondOnce({
        ok: false,
        status: 500,
      });

      await expect(updateEntity<BookEntity>('123', book)).rejects.toThrow();
    });

    it('should throw error with correct entity type in message', async (t) => {
      const api = mockFetch(t);
      const movie: MovieEntity = {
        type: Entity.Movies,
        id: '456',
        title: 'Test',
      };

      api.respondOnce({
        ok: false,
        status: 400,
      });

      await expect(updateEntity<MovieEntity>('456', movie)).rejects.toThrow();
    });

    it('should handle complex entity updates', async (t) => {
      const api = mockFetch(t);
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

      api.respondOnce({
        ok: true,
      });

      await updateEntity<BookEntity>('123', complexBook);

      const callArgs = api.apiFetch.mock.calls[0].arguments;
      const body = JSON.parse(callArgs[1].body);

      expect(body.authors).toHaveLength(3);
      expect(body.genres).toHaveLength(3);
      expect(body.pages).toBe(500);
    });
  });

  describe('addEntity', () => {
    it('should return the image error code separately from the saved entity', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
        status: 201,
        json: async () => ({ id: 'new-1', type: Entity.Books, title: 'B', imageError: 'url_unreachable' }),
      });

      const { entity, imageError } = await addEntity<BookEntity>({ type: Entity.Books, title: 'B' });

      expect(imageError).toBe('url_unreachable');
      expect(entity).toEqual({ id: 'new-1', type: Entity.Books, title: 'B' });
    });

    it('should return no image error when the create response has none', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({ ok: true, status: 201, json: async () => ({ id: 'new-1', type: Entity.Books, title: 'B' }) });

      const result = await addEntity<BookEntity>({ type: Entity.Books, title: 'B' });

      expect(result.imageError).toBeUndefined();
    });

    it('should show the detail of a problem-details rejection of the create', async (t) => {
      const api = mockFetch(t);
      const problem = { title: 'Invalid ExternalId', status: 400, detail: "ExternalId must be 'source:value'." };
      api.respondOnce({ ok: false, status: 400, text: async () => JSON.stringify(problem) });

      const error = await addEntity<MovieEntity>({ type: Entity.Movies, title: 'T' }).catch((e: unknown) => e);

      expect((error as Response).status).toBe(400);
      expect(await (error as Response).text()).toBe("ExternalId must be 'source:value'.");
    });

    it('should add a new book entity', async (t) => {
      const api = mockFetch(t);
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
        authors: ['Author'],
      };

      const responseBook: BookEntity = {
        ...newBook,
        id: 'new-id-123',
      };

      api.respondOnce({
        ok: true,
        json: async () => responseBook,
      });

      const { entity: result } = await addEntity<BookEntity>(newBook);

      expect(result).toEqual(responseBook);
      expect(result.id).toBe('new-id-123');
      expectFetched(
        api.apiFetch,
        `/${Entity.Books}`,
        expect.objectContaining({
          method: 'POST',
          headers: { 'content-type': 'application/json' },
        })
      );
    });

    it('should add a new movie entity', async (t) => {
      const api = mockFetch(t);
      const newMovie: MovieEntity = {
        type: Entity.Movies,
        title: 'New Movie',
        barcode: '123456789',
      };

      const responseMovie: MovieEntity = {
        ...newMovie,
        id: 'movie-456',
      };

      api.respondOnce({
        ok: true,
        json: async () => responseMovie,
      });

      const { entity: result } = await addEntity<MovieEntity>(newMovie);

      expect(result.id).toBe('movie-456');
      expect(result.type).toBe(Entity.Movies);
    });

    it('should add a new game entity', async (t) => {
      const api = mockFetch(t);
      const newGame: GameEntity = {
        type: Entity.Games,
        title: 'New Game',
        platform: 'PS5',
      };

      const responseGame: GameEntity = {
        ...newGame,
        id: 'game-789',
      };

      api.respondOnce({
        ok: true,
        json: async () => responseGame,
      });

      const { entity: result } = await addEntity<GameEntity>(newGame);

      expect(result.id).toBe('game-789');
    });

    it('should add a new music entity', async (t) => {
      const api = mockFetch(t);
      const newMusic: MusicEntity = {
        type: Entity.Musics,
        title: 'New Album',
        artist: 'New Artist',
      };

      const responseMusic: MusicEntity = {
        ...newMusic,
        id: 'music-101',
      };

      api.respondOnce({
        ok: true,
        json: async () => responseMusic,
      });

      const { entity: result } = await addEntity<MusicEntity>(newMusic);

      expect(result.id).toBe('music-101');
    });

    it('should send entity data as JSON in POST request', async (t) => {
      const api = mockFetch(t);
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'Test Book',
        isbn: '123',
      };

      api.respondOnce({
        ok: true,
        json: async () => ({ ...newBook, id: '1' }),
      });

      await addEntity<BookEntity>(newBook);

      const callArgs = api.apiFetch.mock.calls[0].arguments;
      const body = JSON.parse(callArgs[1].body);

      expect(body.type).toBe(Entity.Books);
      expect(body.title).toBe('Test Book');
      expect(body.isbn).toBe('123');
    });

    it('should throw error when adding entity fails', async (t) => {
      const api = mockFetch(t);
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
      };

      api.respondOnce({
        ok: false,
        status: 500,
      });

      await expect(addEntity<BookEntity>(newBook)).rejects.toThrow();
    });

    it('should throw error with correct entity type in message', async (t) => {
      const api = mockFetch(t);
      const newMovie: MovieEntity = {
        type: Entity.Movies,
        title: 'New Movie',
      };

      api.respondOnce({
        ok: false,
        status: 400,
      });

      await expect(addEntity<MovieEntity>(newMovie)).rejects.toThrow();
    });

    it('should handle complex entity additions', async (t) => {
      const api = mockFetch(t);
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

      api.respondOnce({
        ok: true,
        json: async () => responseGame,
      });

      const { entity: result } = await addEntity<GameEntity>(complexGame);

      expect(result.developers).toHaveLength(2);
      expect(result.genres).toHaveLength(2);
      expect(result.id).toBe('game-999');
    });

    it('should return entity with assigned ID from response', async (t) => {
      const api = mockFetch(t);
      const bookWithoutId: BookEntity = {
        type: Entity.Books,
        title: 'Book',
      };

      const responseBook: BookEntity = {
        ...bookWithoutId,
        id: 'assigned-id-12345',
      };

      api.respondOnce({
        ok: true,
        json: async () => responseBook,
      });

      const { entity: result } = await addEntity<BookEntity>(bookWithoutId);

      expect(result.id).toBe('assigned-id-12345');
      expect(result.title).toBe('Book');
    });
  });

  describe('deleteEntity', () => {
    it('should delete a book entity', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Books, '123');

      expectFetched(api.apiFetch, `/${Entity.Books}/123`, expect.objectContaining({ method: 'DELETE' }));
    });

    it('should delete a movie entity', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Movies, '456');

      expectFetched(api.apiFetch, `/${Entity.Movies}/456`, expect.objectContaining({ method: 'DELETE' }));
    });

    it('should delete a game entity', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Games, '789');

      expectFetched(api.apiFetch, `/${Entity.Games}/789`, expect.objectContaining({ method: 'DELETE' }));
    });

    it('should delete a music entity', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Musics, '101');

      expectFetched(api.apiFetch, `/${Entity.Musics}/101`, expect.objectContaining({ method: 'DELETE' }));
    });

    it('should throw error when deletion fails', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 500,
      });

      await expect(deleteEntity(Entity.Books, '123')).rejects.toThrow();
    });

    it('should report a missing entity as 404 and other failures as 500', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({ ok: false, status: 404 });
      const missing = await deleteEntity(Entity.Books, '1').catch((e: unknown) => e);
      api.respondOnce({ ok: false, status: 503 });
      const failed = await deleteEntity(Entity.Books, '1').catch((e: unknown) => e);

      expect((missing as Response).status).toBe(404);
      expect((failed as Response).status).toBe(500);
    });

    it('should throw error with correct entity type and id in message', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 404,
      });

      await expect(deleteEntity(Entity.Movies, '999')).rejects.toThrow();
    });

    it('should handle deletion of multiple entities sequentially', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({ ok: true });
      api.respondOnce({ ok: true });
      api.respondOnce({ ok: true });

      await deleteEntity(Entity.Books, '1');
      await deleteEntity(Entity.Books, '2');
      await deleteEntity(Entity.Books, '3');

      expect(api.apiFetch).toHaveBeenCalledTimes(3);
    });

    it('should send correct DELETE request', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Games, '123');

      const callArgs = api.apiFetch.mock.calls[0].arguments;
      expect(callArgs[1].method).toBe('DELETE');
    });

    it('should handle deletion with numeric-like string IDs', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: true,
      });

      await deleteEntity(Entity.Musics, '999999');

      expectFetched(api.apiFetch, '/999999', { method: 'DELETE' });
    });
  });

  describe('Entity type handling across all functions', () => {
    it('should correctly handle all entity types', async (t) => {
      const api = mockFetch(t);
      const entities = [Entity.Books, Entity.Movies, Entity.Games, Entity.Musics];

      for (const entity of entities) {
        api.respondOnce({
          ok: true,
          json: async () => [{ type: entity, id: '1' }],
        });

        const result = await searchEntities(entity, 'test');
        expect(result).toHaveLength(1);
        expect(result[0].type).toBe(entity);
        api.clearCalls();
      }
    });

    it('should maintain entity type throughout CRUD operations', async (t) => {
      const api = mockFetch(t);
      const book: BookEntity = { type: Entity.Books, title: 'Test' };

      // Search
      api.respondOnce({
        ok: true,
        json: async () => [book],
      });
      const result = await searchEntities<BookEntity>(Entity.Books, 'test');
      expect(result[0].type).toBe(Entity.Books);

      // Get
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => ({ ...book, id: '1' }),
      });
      const getResult = await getEntity<BookEntity>(Entity.Books, '1');
      expect(getResult.type).toBe(Entity.Books);
    });
  });

  describe('Error handling', () => {
    it('should handle network errors during search', async (t) => {
      const api = mockFetch(t);
      api.rejectOnce(new Error('Network error'));

      await expect(searchEntities<BookEntity>(Entity.Books, 'test')).rejects.toThrow('Network error');
    });

    it('should handle network errors during get', async (t) => {
      const api = mockFetch(t);
      api.rejectOnce(new Error('Network error'));

      await expect(getEntity<BookEntity>(Entity.Books, '1')).rejects.toThrow('Network error');
    });

    it('should handle network errors during update', async (t) => {
      const api = mockFetch(t);
      const book: BookEntity = { type: Entity.Books, id: '1', title: 'Test' };
      api.rejectOnce(new Error('Network error'));

      await expect(updateEntity<BookEntity>('1', book)).rejects.toThrow('Network error');
    });

    it('should handle network errors during add', async (t) => {
      const api = mockFetch(t);
      const book: BookEntity = { type: Entity.Books, title: 'Test' };
      api.rejectOnce(new Error('Network error'));

      await expect(addEntity<BookEntity>(book)).rejects.toThrow('Network error');
    });

    it('should handle network errors during delete', async (t) => {
      const api = mockFetch(t);
      api.rejectOnce(new Error('Network error'));

      await expect(deleteEntity(Entity.Books, '1')).rejects.toThrow('Network error');
    });

    it('should handle 5xx errors appropriately', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 503,
      });

      await expect(searchEntities<BookEntity>(Entity.Books, 'test')).rejects.toThrow();
    });

    it('should handle 4xx errors appropriately', async (t) => {
      const api = mockFetch(t);
      api.respondOnce({
        ok: false,
        status: 400,
      });

      await expect(searchEntities<BookEntity>(Entity.Books, 'test')).rejects.toThrow();
    });
  });

  describe('Integration-like scenarios', () => {
    it('should perform full CRUD cycle for a book', async (t) => {
      const api = mockFetch(t);
      const newBook: BookEntity = {
        type: Entity.Books,
        title: 'New Book',
        authors: ['Author'],
      };

      // Create
      api.respondOnce({
        ok: true,
        json: async () => ({ ...newBook, id: '1' }),
      });
      const { entity: created } = await addEntity<BookEntity>(newBook);
      expect(created.id).toBe('1');

      // Read
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => created,
      });
      const retrieved = await getEntity<BookEntity>(Entity.Books, '1');
      expect(retrieved.title).toBe('New Book');

      // Update
      const updated: BookEntity = { ...retrieved, title: 'Updated Book' };
      api.respondOnce({
        ok: true,
      });
      await updateEntity<BookEntity>('1', updated);

      // Delete
      api.respondOnce({
        ok: true,
      });
      await deleteEntity(Entity.Books, '1');

      expect(api.apiFetch).toHaveBeenCalledTimes(4);
    });

    it('should search and then retrieve individual items', async (t) => {
      const api = mockFetch(t);
      const mockBooks: BookEntity[] = [
        { type: Entity.Books, id: '1', title: 'Book 1' },
        { type: Entity.Books, id: '2', title: 'Book 2' },
      ];

      // Search
      api.respondOnce({
        ok: true,
        json: async () => mockBooks,
      });
      const searchResults = await searchEntities<BookEntity>(Entity.Books, 'book');
      expect(searchResults).toHaveLength(2);

      // Get specific item
      api.respondOnce({
        ok: true,
        status: 200,
        json: async () => mockBooks[0],
      });
      const singleBook = await getEntity<BookEntity>(Entity.Books, '1');
      expect(singleBook.title).toBe('Book 1');
    });
  });
});
