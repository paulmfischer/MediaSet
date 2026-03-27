import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mockApiResponse } from '~/test/mocks';

// Now import after mocking
import { lookup, getIdentifierTypeForField, isLookupError } from '~/api/lookup-data.server';
import { Entity, BookLookupResponse, MovieLookupResponse, GameLookupResponse, MusicLookupResponse } from '~/models';

describe('lookup-data.server.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('getIdentifierTypeForField', () => {
    it('returns "entity" for title field', () => {
      expect(getIdentifierTypeForField(Entity.Books, 'title')).toBe('entity');
      expect(getIdentifierTypeForField(Entity.Movies, 'title')).toBe('entity');
    });

    it('returns "isbn" for isbn field on books', () => {
      expect(getIdentifierTypeForField(Entity.Books, 'isbn')).toBe('isbn');
    });

    it('returns "upc" for barcode field on movies, games, and music', () => {
      expect(getIdentifierTypeForField(Entity.Movies, 'barcode')).toBe('upc');
      expect(getIdentifierTypeForField(Entity.Games, 'barcode')).toBe('upc');
      expect(getIdentifierTypeForField(Entity.Musics, 'barcode')).toBe('upc');
    });
  });

  describe('isLookupError', () => {
    it('returns true for objects with message and statusCode', () => {
      expect(isLookupError({ message: 'Not found', statusCode: 404 })).toBe(true);
    });

    it('returns false for valid entity results', () => {
      expect(isLookupError({ type: Entity.Books, title: 'A Book' })).toBe(false);
      expect(isLookupError(null)).toBe(false);
      expect(isLookupError('error')).toBe(false);
    });
  });

  describe('lookup', () => {
    describe('error handling', () => {
      it('returns a LookupError when the response is not ok', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce(new Response('Not found', { status: 404 }));

        const result = await lookup(Entity.Books, 'isbn', { isbn: '9781234567890' });

        expect(isLookupError(result)).toBe(true);
        if (isLookupError(result)) {
          expect(result.statusCode).toBe(404);
        }
      });

      it('returns a LookupError with a fallback message when body is empty', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce(new Response('', { status: 500 }));

        const result = await lookup(Entity.Movies, 'upc', { barcode: '123456789' });

        expect(isLookupError(result)).toBe(true);
        if (isLookupError(result)) {
          expect(result.statusCode).toBe(500);
          expect(result.message).toContain('Movie');
        }
      });

      it('returns a LookupError for unsupported entity type', async () => {
        const mockResponse: BookLookupResponse[] = [];
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockResponse));

        // Cast to force an unsupported entity type through
        const result = await lookup('unsupported' as Entity, 'isbn', { isbn: '123' });

        expect(isLookupError(result)).toBe(true);
        if (isLookupError(result)) {
          expect(result.statusCode).toBe(400);
        }
      });
    });

    describe('book isbn lookup', () => {
      it('retains the isbn value in the returned entity', async () => {
        const isbnValue = '9781234567890';
        const mockResponse: BookLookupResponse[] = [
          {
            title: 'Test Book',
            subtitle: '',
            authors: [{ name: 'Author One', url: '' }],
            numberOfPages: 300,
            publishers: [{ name: 'Publisher A' }],
            publishDate: '2020',
            subjects: [{ name: 'Fiction', url: '' }],
            format: 'Hardcover',
            imageUrl: 'http://example.com/cover.jpg',
          },
        ];
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockResponse));

        const result = await lookup(Entity.Books, 'isbn', { isbn: isbnValue });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect(result).toHaveLength(1);
          expect(result[0].type).toBe(Entity.Books);
          expect((result[0] as { isbn?: string }).isbn).toBe(isbnValue);
        }
      });

      it('does not set isbn for entity (title) searches', async () => {
        const mockResponse: BookLookupResponse[] = [
          {
            title: 'Test Book',
            subtitle: '',
            authors: [],
            numberOfPages: 0,
            publishers: [],
            publishDate: '',
            subjects: [],
          },
        ];
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockResponse));

        const result = await lookup(Entity.Books, 'entity', { title: 'Test Book' });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect((result[0] as { isbn?: string }).isbn).toBeUndefined();
        }
      });
    });

    describe('movie barcode lookup', () => {
      const mockMovieResponse: MovieLookupResponse[] = [
        {
          title: 'Test Movie',
          genres: ['Action'],
          studios: ['Studio A'],
          releaseDate: '2021-01-01',
          rating: 'PG-13',
          runtime: 120,
          plot: 'A test movie.',
          format: 'Blu-ray',
          imageUrl: 'http://example.com/poster.jpg',
        },
      ];

      it('retains the barcode value when searchParams uses "barcode" key', async () => {
        const barcodeValue = '012345678901';
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockMovieResponse));

        const result = await lookup(Entity.Movies, 'upc', { barcode: barcodeValue });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect(result).toHaveLength(1);
          expect(result[0].type).toBe(Entity.Movies);
          expect((result[0] as { barcode?: string }).barcode).toBe(barcodeValue);
        }
      });

      it('retains the barcode value when searchParams uses "upc" key', async () => {
        const upcValue = '012345678901';
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockMovieResponse));

        const result = await lookup(Entity.Movies, 'upc', { upc: upcValue });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect((result[0] as { barcode?: string }).barcode).toBe(upcValue);
        }
      });

      it('does not set barcode for entity (title) searches', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockMovieResponse));

        const result = await lookup(Entity.Movies, 'entity', { title: 'Test Movie' });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect((result[0] as { barcode?: string }).barcode).toBeUndefined();
        }
      });
    });

    describe('game barcode lookup', () => {
      const mockGameResponse: GameLookupResponse[] = [
        {
          title: 'Test Game',
          platform: 'PS5',
          genres: ['RPG'],
          developers: ['Dev Studio'],
          publishers: ['Publisher'],
          releaseDate: '2022-06-15',
          rating: 'T',
          description: 'A test game.',
          format: 'Physical',
        },
      ];

      it('retains the barcode value when searchParams uses "barcode" key', async () => {
        const barcodeValue = '711719541486';
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockGameResponse));

        const result = await lookup(Entity.Games, 'upc', { barcode: barcodeValue });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect(result).toHaveLength(1);
          expect(result[0].type).toBe(Entity.Games);
          expect((result[0] as { barcode?: string }).barcode).toBe(barcodeValue);
        }
      });

      it('does not set barcode for entity (title) searches', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockGameResponse));

        const result = await lookup(Entity.Games, 'entity', { title: 'Test Game' });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect((result[0] as { barcode?: string }).barcode).toBeUndefined();
        }
      });
    });

    describe('music barcode lookup', () => {
      const mockMusicResponse: MusicLookupResponse[] = [
        {
          title: 'Test Album',
          artist: 'Test Artist',
          releaseDate: '2023-03-01',
          genres: ['Rock'],
          duration: 3600,
          label: 'Record Label',
          tracks: 12,
          discs: 1,
          discList: [],
          format: 'CD',
        },
      ];

      it('retains the barcode value when searchParams uses "barcode" key', async () => {
        const barcodeValue = '093624936428';
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockMusicResponse));

        const result = await lookup(Entity.Musics, 'upc', { barcode: barcodeValue });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect(result).toHaveLength(1);
          expect(result[0].type).toBe(Entity.Musics);
          expect((result[0] as { barcode?: string }).barcode).toBe(barcodeValue);
        }
      });

      it('does not set barcode for entity (title) searches', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce(mockApiResponse(mockMusicResponse));

        const result = await lookup(Entity.Musics, 'entity', { title: 'Test Album' });

        expect(Array.isArray(result)).toBe(true);
        if (Array.isArray(result)) {
          expect((result[0] as { barcode?: string }).barcode).toBeUndefined();
        }
      });
    });
  });
});
