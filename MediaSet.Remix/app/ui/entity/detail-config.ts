import {
  Entity,
  type BaseEntity,
  type BookEntity,
  type Disc,
  type GameEntity,
  type MovieEntity,
  type MusicEntity,
} from '../../models.ts';
import { millisecondsToMinutesSeconds } from '../../utils/helpers.ts';

export interface DetailField<T> {
  label: string;
  value: (entity: T) => string | number | undefined;
  /** Render a check mark when the value is truthy instead of text. */
  check?: (entity: T) => boolean | undefined;
}

export interface DetailConfig<T extends BaseEntity> {
  entity: Entity;
  /** Singular name, e.g. `Book`, for page titles and messages. */
  singular: string;
  titleOf: (entity: T) => string;
  subtitleOf?: (entity: T) => string | undefined;
  coverAlt: string;
  fields: DetailField<T>[];
  /** Per-disc track list shown below the fields. */
  discList?: (entity: T) => Disc[] | undefined;
}

const join = (values?: string[]) => values?.map((value) => value.trimEnd()).join(', ');

export const bookDetail: DetailConfig<BookEntity> = {
  entity: Entity.Books,
  singular: 'Book',
  titleOf: (book) => book.title ?? '',
  subtitleOf: (book) => book.subtitle,
  coverAlt: 'Book cover',
  fields: [
    { label: 'Format', value: (book) => book.format },
    { label: 'Pages', value: (book) => book.pages },
    { label: 'Publication Date', value: (book) => book.publicationDate },
    { label: 'Authors', value: (book) => join(book.authors) },
    { label: 'Genres', value: (book) => join(book.genres) },
    { label: 'Publisher', value: (book) => book.publisher },
    { label: 'ISBN', value: (book) => book.isbn },
    { label: 'Plot', value: (book) => book.plot },
  ],
};

export const movieDetail: DetailConfig<MovieEntity> = {
  entity: Entity.Movies,
  singular: 'Movie',
  titleOf: (movie) => movie.title ?? '',
  coverAlt: 'Movie poster',
  fields: [
    { label: 'Format', value: (movie) => movie.format },
    { label: 'Runtime', value: (movie) => movie.runtime },
    { label: 'Release Date', value: (movie) => movie.releaseDate },
    { label: 'Is TV Show', value: () => undefined, check: (movie) => movie.isTvSeries },
    { label: 'Genres', value: (movie) => join(movie.genres) },
    { label: 'Studios', value: (movie) => join(movie.studios) },
    { label: 'Barcode', value: (movie) => movie.barcode },
    { label: 'Plot', value: (movie) => movie.plot },
  ],
};

export const gameDetail: DetailConfig<GameEntity> = {
  entity: Entity.Games,
  singular: 'Game',
  titleOf: (game) => game.title ?? '',
  coverAlt: 'Game cover art',
  fields: [
    { label: 'Platform', value: (game) => game.platform },
    { label: 'Format', value: (game) => game.format },
    { label: 'Release Date', value: (game) => game.releaseDate },
    { label: 'Rating', value: (game) => game.rating },
    { label: 'Developers', value: (game) => join(game.developers) },
    { label: 'Publishers', value: (game) => join(game.publishers) },
    { label: 'Genres', value: (game) => join(game.genres) },
    { label: 'Barcode', value: (game) => game.barcode },
    { label: 'Description', value: (game) => game.description },
  ],
};

export const musicDetail: DetailConfig<MusicEntity> = {
  entity: Entity.Musics,
  singular: 'Music',
  titleOf: (music) => music.title ?? '',
  coverAlt: 'Album cover',
  fields: [
    { label: 'Artist', value: (music) => music.artist },
    { label: 'Format', value: (music) => music.format },
    { label: 'Release Date', value: (music) => music.releaseDate },
    { label: 'Genres', value: (music) => join(music.genres) },
    { label: 'Duration', value: (music) => millisecondsToMinutesSeconds(music.duration) },
    { label: 'Label', value: (music) => music.label },
    { label: 'Barcode', value: (music) => music.barcode },
    { label: 'Tracks', value: (music) => music.tracks },
    { label: 'Discs', value: (music) => music.discs },
  ],
  discList: (music) => music.discList,
};
