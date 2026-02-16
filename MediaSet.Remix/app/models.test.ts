import { describe, it, expect } from 'vitest';
import {
  Entity,
  type BaseEntity,
  type BookEntity,
  type MovieEntity,
  type GameEntity,
  type MusicEntity,
  type Book,
  type Movie,
  type Game,
  type Music,
  type Option,
  type FormProps,
  type Disc,
  type BookLookupResponse,
  type MovieLookupResponse,
  type GameLookupResponse,
  type MusicLookupResponse,
  type LookupError,
  type LookupResult,
  type IdentifierType,
} from './models';

describe('models.ts', () => {
  describe('Entity enum', () => {
    it('should have all required entity types', () => {
      expect(Entity.Books).toBe('Books');
      expect(Entity.Movies).toBe('Movies');
      expect(Entity.Games).toBe('Games');
      expect(Entity.Musics).toBe('Musics');
    });

    it('should have exactly 4 entity types', () => {
      const entities = Object.values(Entity);
      expect(entities).toHaveLength(4);
    });

    it('should allow direct access to entity values', () => {
      const entityArray = [Entity.Books, Entity.Movies, Entity.Games, Entity.Musics];
      expect(entityArray).toContain(Entity.Books);
      expect(entityArray).toContain(Entity.Movies);
      expect(entityArray).toContain(Entity.Games);
      expect(entityArray).toContain(Entity.Musics);
    });
  });

  describe('FormProps type', () => {
    it('should allow empty form props', () => {
      const props: FormProps = {};
      expect(props).toEqual({});
    });

    it('should allow isSubmitting as boolean', () => {
      const props: FormProps = { isSubmitting: true };
      expect(props.isSubmitting).toBe(true);
    });

    it('should allow isSubmitting as false', () => {
      const props: FormProps = { isSubmitting: false };
      expect(props.isSubmitting).toBe(false);
    });

    it('should allow isSubmitting to be undefined', () => {
      const props: FormProps = { isSubmitting: undefined };
      expect(props.isSubmitting).toBeUndefined();
    });
  });

  describe('Option type', () => {
    it('should create option with required fields', () => {
      const option: Option = { label: 'Test', value: 'test' };
      expect(option).toEqual({ label: 'Test', value: 'test' });
    });

    it('should create option with isNew flag', () => {
      const option: Option = { label: 'New Item', value: 'new', isNew: true };
      expect(option.isNew).toBe(true);
    });

    it('should allow isNew to be undefined', () => {
      const option: Option = { label: 'Item', value: 'item', isNew: undefined };
      expect(option.isNew).toBeUndefined();
    });

    it('should have string label and value', () => {
      const option: Option = { label: 'Label', value: 'value' };
      expect(typeof option.label).toBe('string');
      expect(typeof option.value).toBe('string');
    });
  });

  describe('BaseEntity interface', () => {
    it('should create base entity with required type field', () => {
      const entity: BaseEntity = { type: Entity.Books };
      expect(entity.type).toBe(Entity.Books);
    });

    it('should allow optional id field', () => {
      const entity: BaseEntity = { type: Entity.Movies, id: '123' };
      expect(entity.id).toBe('123');
    });

    it('should allow optional title field', () => {
      const entity: BaseEntity = { type: Entity.Games, title: 'Game Title' };
      expect(entity.title).toBe('Game Title');
    });

    it('should allow optional format field', () => {
      const entity: BaseEntity = { type: Entity.Musics, format: 'CD' };
      expect(entity.format).toBe('CD');
    });

    it('should allow all optional fields together', () => {
      const entity: BaseEntity = {
        type: Entity.Books,
        id: '456',
        title: 'Book Title',
        format: 'Hardcover',
      };
      expect(entity).toEqual({
        type: Entity.Books,
        id: '456',
        title: 'Book Title',
        format: 'Hardcover',
      });
    });
  });

  describe('BookEntity interface', () => {
    it('should create book entity with all fields', () => {
      const book: BookEntity = {
        type: Entity.Books,
        id: '1',
        title: 'The Great Book',
        isbn: '978-3-16-148410-0',
        pages: 300,
        publicationDate: '2023-01-15',
        authors: ['Author One', 'Author Two'],
        publisher: 'Great Publisher',
        genres: ['Fiction', 'Mystery'],
        plot: 'A captivating story',
        subtitle: 'A Subtitle',
        format: 'Hardcover',
      };
      expect(book.type).toBe(Entity.Books);
      expect(book.isbn).toBe('978-3-16-148410-0');
      expect(book.authors).toHaveLength(2);
    });

    it('should allow book with only required fields', () => {
      const book: BookEntity = { type: Entity.Books };
      expect(book.type).toBe(Entity.Books);
      expect(book.isbn).toBeUndefined();
    });

    it('should allow undefined arrays', () => {
      const book: BookEntity = {
        type: Entity.Books,
        authors: undefined,
        genres: undefined,
      };
      expect(book.authors).toBeUndefined();
      expect(book.genres).toBeUndefined();
    });

    it('should allow empty string arrays', () => {
      const book: BookEntity = {
        type: Entity.Books,
        authors: [],
        genres: [],
      };
      expect(book.authors).toHaveLength(0);
      expect(book.genres).toHaveLength(0);
    });

    it('should accept numeric pages field', () => {
      const book: BookEntity = { type: Entity.Books, pages: 250 };
      expect(book.pages).toBe(250);
    });
  });

  describe('Book type (UI model)', () => {
    it('should create book with authors as string', () => {
      const book: Book = {
        type: Entity.Books,
        authors: 'Author One, Author Two',
        genres: 'Fiction',
      };
      expect(book.authors).toBe('Author One, Author Two');
      expect(typeof book.authors).toBe('string');
    });

    it('should create book with genres as string', () => {
      const book: Book = {
        type: Entity.Books,
        authors: 'Author',
        genres: 'Fiction,Mystery',
      };
      expect(book.genres).toBe('Fiction,Mystery');
    });

    it('should override backend array fields with string fields', () => {
      const book: Book = {
        type: Entity.Books,
        title: 'Test',
        authors: 'Single Author',
        genres: 'Genre1,Genre2',
      };
      expect(typeof book.authors).toBe('string');
      expect(typeof book.genres).toBe('string');
    });
  });

  describe('MovieEntity interface', () => {
    it('should create movie entity with all fields', () => {
      const movie: MovieEntity = {
        type: Entity.Movies,
        id: '2',
        title: 'The Great Movie',
        barcode: '123456789',
        releaseDate: '2023-06-15',
        rating: '8.5',
        runtime: 120,
        studios: ['Studio A', 'Studio B'],
        genres: ['Action', 'Thriller'],
        plot: 'An epic adventure',
        isTvSeries: false,
        format: 'Blu-ray',
      };
      expect(movie.type).toBe(Entity.Movies);
      expect(movie.runtime).toBe(120);
      expect(movie.isTvSeries).toBe(false);
    });

    it('should allow movie with only required fields', () => {
      const movie: MovieEntity = { type: Entity.Movies };
      expect(movie.type).toBe(Entity.Movies);
      expect(movie.runtime).toBeUndefined();
    });

    it('should accept boolean isTvSeries field', () => {
      const movie: MovieEntity = {
        type: Entity.Movies,
        isTvSeries: true,
      };
      expect(movie.isTvSeries).toBe(true);
    });

    it('should accept null runtime', () => {
      const movie: MovieEntity = { type: Entity.Movies, runtime: null as unknown as number };
      expect(movie.runtime).toBeNull();
    });

    it('should allow undefined arrays', () => {
      const movie: MovieEntity = {
        type: Entity.Movies,
        studios: undefined,
        genres: undefined,
      };
      expect(movie.studios).toBeUndefined();
      expect(movie.genres).toBeUndefined();
    });
  });

  describe('Movie type (UI model)', () => {
    it('should create movie with studios as string', () => {
      const movie: Movie = {
        type: Entity.Movies,
        studios: 'Studio A, Studio B',
        genres: 'Action',
      };
      expect(movie.studios).toBe('Studio A, Studio B');
      expect(typeof movie.studios).toBe('string');
    });

    it('should create movie with genres as string', () => {
      const movie: Movie = {
        type: Entity.Movies,
        studios: 'Studio',
        genres: 'Action,Drama',
      };
      expect(movie.genres).toBe('Action,Drama');
    });
  });

  describe('GameEntity interface', () => {
    it('should create game entity with all fields', () => {
      const game: GameEntity = {
        type: Entity.Games,
        id: '3',
        title: 'The Great Game',
        barcode: '987654321',
        releaseDate: '2023-09-20',
        rating: '9.0',
        platform: 'PlayStation 5',
        developers: ['Dev Studio A', 'Dev Studio B'],
        publishers: ['Publisher A', 'Publisher B'],
        genres: ['Action', 'RPG'],
        description: 'An amazing gaming experience',
        format: 'Disc',
      };
      expect(game.type).toBe(Entity.Games);
      expect(game.platform).toBe('PlayStation 5');
      expect(game.developers).toHaveLength(2);
    });

    it('should allow game with only required fields', () => {
      const game: GameEntity = { type: Entity.Games };
      expect(game.type).toBe(Entity.Games);
      expect(game.platform).toBeUndefined();
    });

    it('should allow empty arrays', () => {
      const game: GameEntity = {
        type: Entity.Games,
        developers: [],
        publishers: [],
        genres: [],
      };
      expect(game.developers).toHaveLength(0);
      expect(game.publishers).toHaveLength(0);
      expect(game.genres).toHaveLength(0);
    });
  });

  describe('Game type (UI model)', () => {
    it('should create game with developers as string', () => {
      const game: Game = {
        type: Entity.Games,
        developers: 'Dev A, Dev B',
        publishers: 'Pub',
        genres: 'Action',
      };
      expect(game.developers).toBe('Dev A, Dev B');
      expect(typeof game.developers).toBe('string');
    });

    it('should create game with publishers as string', () => {
      const game: Game = {
        type: Entity.Games,
        developers: 'Dev',
        publishers: 'Pub A, Pub B',
        genres: 'Action',
      };
      expect(game.publishers).toBe('Pub A, Pub B');
      expect(typeof game.publishers).toBe('string');
    });

    it('should create game with genres as string', () => {
      const game: Game = {
        type: Entity.Games,
        developers: 'Dev',
        publishers: 'Pub',
        genres: 'Action,RPG',
      };
      expect(game.genres).toBe('Action,RPG');
    });
  });

  describe('Disc interface', () => {
    it('should create disc with all fields', () => {
      const disc: Disc = {
        trackNumber: 1,
        title: 'Track One',
        duration: 180000,
      };
      expect(disc.trackNumber).toBe(1);
      expect(disc.title).toBe('Track One');
      expect(disc.duration).toBe(180000);
    });

    it('should allow null duration', () => {
      const disc: Disc = {
        trackNumber: 2,
        title: 'Track Two',
        duration: null,
      };
      expect(disc.duration).toBeNull();
    });

    it('should accept track numbers as integers', () => {
      const disc: Disc = { trackNumber: 99, title: 'Track', duration: 1000 };
      expect(disc.trackNumber).toBe(99);
      expect(typeof disc.trackNumber).toBe('number');
    });

    it('should accept zero duration', () => {
      const disc: Disc = { trackNumber: 1, title: 'Track', duration: 0 };
      expect(disc.duration).toBe(0);
    });
  });

  describe('MusicEntity interface', () => {
    it('should create music entity with all fields', () => {
      const music: MusicEntity = {
        type: Entity.Musics,
        id: '4',
        title: 'Greatest Album',
        artist: 'Great Artist',
        releaseDate: '2023-03-10',
        genres: ['Rock', 'Pop'],
        duration: 2700000,
        label: 'Great Label',
        barcode: '111222333',
        tracks: 12,
        discs: 1,
        discList: [
          { trackNumber: 1, title: 'Track 1', duration: 225000 },
          { trackNumber: 2, title: 'Track 2', duration: 255000 },
        ],
        format: 'CD',
      };
      expect(music.type).toBe(Entity.Musics);
      expect(music.artist).toBe('Great Artist');
      expect(music.discList).toHaveLength(2);
    });

    it('should allow music with only required fields', () => {
      const music: MusicEntity = { type: Entity.Musics };
      expect(music.type).toBe(Entity.Musics);
      expect(music.artist).toBeUndefined();
    });

    it('should allow null duration', () => {
      const music: MusicEntity = {
        type: Entity.Musics,
        duration: null as unknown as string,
      };
      expect(music.duration).toBeNull();
    });

    it('should allow null tracks and discs', () => {
      const music: MusicEntity = {
        type: Entity.Musics,
        tracks: null as unknown as number,
        discs: null as unknown as number,
      };
      expect(music.tracks).toBeNull();
      expect(music.discs).toBeNull();
    });

    it('should allow undefined discList', () => {
      const music: MusicEntity = {
        type: Entity.Musics,
        discList: undefined,
      };
      expect(music.discList).toBeUndefined();
    });

    it('should allow empty discList array', () => {
      const music: MusicEntity = {
        type: Entity.Musics,
        discList: [],
      };
      expect(music.discList).toHaveLength(0);
    });
  });

  describe('Music type (UI model)', () => {
    it('should create music with genres as string', () => {
      const music: Music = {
        type: Entity.Musics,
        genres: 'Rock,Pop',
      };
      expect(music.genres).toBe('Rock,Pop');
      expect(typeof music.genres).toBe('string');
    });
  });

  describe('IdentifierType type', () => {
    it('should allow valid identifier types', () => {
      const identifiers: IdentifierType[] = ['isbn', 'lccn', 'oclc', 'olid', 'upc', 'ean'];
      expect(identifiers).toContain('isbn');
      expect(identifiers).toContain('ean');
    });

    it('should have all 6 identifier types', () => {
      const types: IdentifierType[] = ['isbn', 'lccn', 'oclc', 'olid', 'upc', 'ean'];
      expect(types).toHaveLength(6);
    });
  });

  describe('BookLookupResponse interface', () => {
    it('should create valid lookup response with all fields', () => {
      const response: BookLookupResponse = {
        title: 'Test Book',
        subtitle: 'A Subtitle',
        authors: [
          { name: 'Author One', url: 'http://example.com/author1' },
          { name: 'Author Two', url: 'http://example.com/author2' },
        ],
        numberOfPages: 300,
        publishers: [{ name: 'Publisher One' }],
        publishDate: '2023-01-15',
        subjects: [{ name: 'Fiction', url: 'http://example.com/fiction' }],
        format: 'Hardcover',
      };
      expect(response.title).toBe('Test Book');
      expect(response.authors).toHaveLength(2);
      expect(response.format).toBe('Hardcover');
    });

    it('should allow undefined format', () => {
      const response: BookLookupResponse = {
        title: 'Test',
        subtitle: 'Sub',
        authors: [],
        numberOfPages: 100,
        publishers: [],
        publishDate: '2023-01-01',
        subjects: [],
      };
      expect(response.format).toBeUndefined();
    });

    it('should allow empty arrays', () => {
      const response: BookLookupResponse = {
        title: 'Test',
        subtitle: '',
        authors: [],
        numberOfPages: 0,
        publishers: [],
        publishDate: '2023-01-01',
        subjects: [],
      };
      expect(response.authors).toHaveLength(0);
      expect(response.publishers).toHaveLength(0);
      expect(response.subjects).toHaveLength(0);
    });
  });

  describe('MovieLookupResponse interface', () => {
    it('should create valid movie lookup response', () => {
      const response: MovieLookupResponse = {
        title: 'Test Movie',
        genres: ['Action', 'Thriller'],
        studios: ['Studio A'],
        releaseDate: '2023-06-15',
        rating: '8.5',
        runtime: 120,
        plot: 'An epic story',
        format: 'Blu-ray',
      };
      expect(response.title).toBe('Test Movie');
      expect(response.runtime).toBe(120);
      expect(response.format).toBe('Blu-ray');
    });

    it('should allow null runtime', () => {
      const response: MovieLookupResponse = {
        title: 'Test',
        genres: [],
        studios: [],
        releaseDate: '2023-01-01',
        rating: '0',
        runtime: null,
        plot: 'Test',
      };
      expect(response.runtime).toBeNull();
    });

    it('should allow undefined format', () => {
      const response: MovieLookupResponse = {
        title: 'Test',
        genres: [],
        studios: [],
        releaseDate: '2023-01-01',
        rating: '0',
        runtime: 100,
        plot: 'Test',
      };
      expect(response.format).toBeUndefined();
    });
  });

  describe('GameLookupResponse interface', () => {
    it('should create valid game lookup response', () => {
      const response: GameLookupResponse = {
        title: 'Test Game',
        platform: 'PS5',
        genres: ['Action', 'RPG'],
        developers: ['Dev A'],
        publishers: ['Pub A'],
        releaseDate: '2023-09-20',
        rating: '9.0',
        description: 'Amazing game',
        format: 'Disc',
      };
      expect(response.title).toBe('Test Game');
      expect(response.platform).toBe('PS5');
      expect(response.format).toBe('Disc');
    });

    it('should allow undefined format', () => {
      const response: GameLookupResponse = {
        title: 'Test',
        platform: 'PC',
        genres: [],
        developers: [],
        publishers: [],
        releaseDate: '2023-01-01',
        rating: '0',
        description: 'Test',
      };
      expect(response.format).toBeUndefined();
    });
  });

  describe('MusicLookupResponse interface', () => {
    it('should create valid music lookup response', () => {
      const response: MusicLookupResponse = {
        title: 'Test Album',
        artist: 'Test Artist',
        releaseDate: '2023-03-10',
        genres: ['Rock', 'Pop'],
        duration: 180000,
        label: 'Test Label',
        tracks: 12,
        discs: 1,
        discList: [{ trackNumber: 1, title: 'Track 1', duration: 225000 }],
        format: 'CD',
      };
      expect(response.title).toBe('Test Album');
      expect(response.discList).toHaveLength(1);
      expect(response.format).toBe('CD');
    });

    it('should allow null duration, tracks, and discs', () => {
      const response: MusicLookupResponse = {
        title: 'Test',
        artist: 'Artist',
        releaseDate: '2023-01-01',
        genres: [],
        duration: null,
        label: 'Label',
        tracks: null,
        discs: null,
        discList: [],
      };
      expect(response.duration).toBeNull();
      expect(response.tracks).toBeNull();
      expect(response.discs).toBeNull();
    });

    it('should allow undefined format', () => {
      const response: MusicLookupResponse = {
        title: 'Test',
        artist: 'Artist',
        releaseDate: '2023-01-01',
        genres: [],
        duration: 100,
        label: 'Label',
        tracks: 10,
        discs: 1,
        discList: [],
      };
      expect(response.format).toBeUndefined();
    });
  });

  describe('LookupError interface', () => {
    it('should create lookup error with message and status code', () => {
      const error: LookupError = {
        message: 'Not found',
        statusCode: 404,
      };
      expect(error.message).toBe('Not found');
      expect(error.statusCode).toBe(404);
    });

    it('should accept various status codes', () => {
      const errors: LookupError[] = [
        { message: 'Not found', statusCode: 404 },
        { message: 'Server error', statusCode: 500 },
        { message: 'Bad request', statusCode: 400 },
      ];
      expect(errors[0].statusCode).toBe(404);
      expect(errors[1].statusCode).toBe(500);
      expect(errors[2].statusCode).toBe(400);
    });

    it('should handle empty error messages', () => {
      const error: LookupError = { message: '', statusCode: 500 };
      expect(error.message).toBe('');
      expect(error.statusCode).toBe(500);
    });
  });

  describe('LookupResult union type', () => {
    describe('with valid responses', () => {
      it('should accept BookLookupResponse', () => {
        const result: LookupResult<BookLookupResponse> = {
          title: 'Test Book',
          subtitle: 'Sub',
          authors: [],
          numberOfPages: 100,
          publishers: [],
          publishDate: '2023-01-01',
          subjects: [],
        };
        expect(result.title).toBe('Test Book');
      });

      it('should accept MovieLookupResponse', () => {
        const result: LookupResult<MovieLookupResponse> = {
          title: 'Test Movie',
          genres: [],
          studios: [],
          releaseDate: '2023-01-01',
          rating: '0',
          runtime: 100,
          plot: 'Test',
        };
        expect(result.title).toBe('Test Movie');
      });

      it('should accept GameLookupResponse', () => {
        const result: LookupResult<GameLookupResponse> = {
          title: 'Test Game',
          platform: 'PS5',
          genres: [],
          developers: [],
          publishers: [],
          releaseDate: '2023-01-01',
          rating: '0',
          description: 'Test',
        };
        expect(result.title).toBe('Test Game');
      });

      it('should accept MusicLookupResponse', () => {
        const result: LookupResult<MusicLookupResponse> = {
          title: 'Test Album',
          artist: 'Artist',
          releaseDate: '2023-01-01',
          genres: [],
          duration: 100,
          label: 'Label',
          tracks: 10,
          discs: 1,
          discList: [],
        };
        expect(result.title).toBe('Test Album');
      });
    });

    describe('with error responses', () => {
      it('should accept LookupError for BookLookupResponse', () => {
        const result: LookupResult<BookLookupResponse> = {
          message: 'Failed to fetch',
          statusCode: 500,
        };
        expect(result.message).toBe('Failed to fetch');
        expect(result.statusCode).toBe(500);
      });

      it('should accept LookupError for MovieLookupResponse', () => {
        const result: LookupResult<MovieLookupResponse> = {
          message: 'Not found',
          statusCode: 404,
        };
        expect(result.statusCode).toBe(404);
      });

      it('should accept LookupError for GameLookupResponse', () => {
        const result: LookupResult<GameLookupResponse> = {
          message: 'Invalid request',
          statusCode: 400,
        };
        expect(result.statusCode).toBe(400);
      });

      it('should accept LookupError for MusicLookupResponse', () => {
        const result: LookupResult<MusicLookupResponse> = {
          message: 'Service unavailable',
          statusCode: 503,
        };
        expect(result.message).toBe('Service unavailable');
      });
    });
  });

  describe('Type validation - Edge cases', () => {
    it('should validate entity with minimal required fields only', () => {
      const minimalBook: BookEntity = { type: Entity.Books };
      const minimalMovie: MovieEntity = { type: Entity.Movies };
      const minimalGame: GameEntity = { type: Entity.Games };
      const minimalMusic: MusicEntity = { type: Entity.Musics };

      expect(minimalBook.type).toBe(Entity.Books);
      expect(minimalMovie.type).toBe(Entity.Movies);
      expect(minimalGame.type).toBe(Entity.Games);
      expect(minimalMusic.type).toBe(Entity.Musics);
    });

    it('should handle entities with null values in optional fields', () => {
      const book: BookEntity = {
        type: Entity.Books,
        isbn: null as unknown as string,
        pages: null as unknown as number,
      };
      expect(book.isbn).toBeNull();
      expect(book.pages).toBeNull();
    });

    it('should differentiate between backend and UI models', () => {
      const backendBook: BookEntity = {
        type: Entity.Books,
        authors: ['Author 1', 'Author 2'],
        genres: ['Fiction', 'Mystery'],
      };
      const uiBook: Book = {
        type: Entity.Books,
        authors: 'Author 1, Author 2',
        genres: 'Fiction, Mystery',
      };

      expect(Array.isArray(backendBook.authors)).toBe(true);
      expect(typeof uiBook.authors).toBe('string');
    });

    it('should handle disc list with varying track counts', () => {
      const singleTrackAlbum: MusicEntity = {
        type: Entity.Musics,
        discList: [{ trackNumber: 1, title: 'Only Track', duration: 180000 }],
      };

      const multiTrackAlbum: MusicEntity = {
        type: Entity.Musics,
        discList: Array.from({ length: 20 }, (_, i) => ({
          trackNumber: i + 1,
          title: `Track ${i + 1}`,
          duration: Math.random() * 300000,
        })),
      };

      expect(singleTrackAlbum.discList).toHaveLength(1);
      expect(multiTrackAlbum.discList).toHaveLength(20);
    });

    it('should validate override type transformations', () => {
      const book: Book = {
        type: Entity.Books,
        authors: 'Test',
        genres: 'Test',
        publisher: 'Test Publisher' as unknown as string[],
      };
      expect(typeof book.authors).toBe('string');
      expect(typeof book.genres).toBe('string');
    });
  });

  describe('Type structure validation', () => {
    it('should validate that all Entity types are strings', () => {
      Object.values(Entity).forEach((entity) => {
        expect(typeof entity).toBe('string');
      });
    });

    it('should validate that all lookup responses have required string fields', () => {
      const bookResponse: BookLookupResponse = {
        title: '',
        subtitle: '',
        authors: [],
        numberOfPages: 0,
        publishers: [],
        publishDate: '',
        subjects: [],
      };
      expect(typeof bookResponse.title).toBe('string');
      expect(typeof bookResponse.publishDate).toBe('string');
    });

    it('should validate that array fields can be empty', () => {
      const book: BookEntity = {
        type: Entity.Books,
        authors: [],
        genres: [],
      };
      const movie: MovieEntity = {
        type: Entity.Movies,
        studios: [],
        genres: [],
      };
      expect(book.authors).toHaveLength(0);
      expect(movie.studios).toHaveLength(0);
    });

    it('should validate that optional fields can be omitted', () => {
      const entity: BaseEntity = { type: Entity.Books };
      expect('id' in entity || entity.id === undefined).toBe(true);
      expect('title' in entity || entity.title === undefined).toBe(true);
    });
  });
});
