import {
  Entity,
  type BaseEntity,
  type BookEntity,
  type GameEntity,
  type MovieEntity,
  type MusicEntity,
} from '../models.ts';

export interface Column<T> {
  label: string;
  /** API sort field. */
  field: string;
  /** Visibility classes; below the breakpoint the column is hidden. */
  class: string;
  cell: (entity: T) => string | boolean | number | undefined;
  /** Render a check mark when truthy instead of text. */
  check?: boolean;
}

export interface ListConfig<T extends BaseEntity> {
  entity: Entity;
  /** Secondary line in the mobile row. */
  summary: (entity: T) => string;
  titleOf: (entity: T) => string;
  columns: Column<T>[];
}

const join = (values?: string[], separator = ', ') => values?.map((value) => value.trimEnd()).join(separator);
const dotted = (values: Array<string | undefined | null>) => values.filter(Boolean).join(' · ');

export const booksList: ListConfig<BookEntity> = {
  entity: Entity.Books,
  titleOf: (book) => `${book.title ?? ''}${book.subtitle ? `: ${book.subtitle}` : ''}`,
  summary: (book) => dotted([join(book.authors), book.format]),
  columns: [
    { label: 'Authors', field: 'authors', class: 'hidden xs:table-cell', cell: (book) => join(book.authors, ',') },
    { label: 'Format', field: 'format', class: 'hidden md:table-cell', cell: (book) => book.format },
    { label: 'Pages', field: 'pages', class: 'hidden lg:table-cell', cell: (book) => book.pages },
  ],
};

export const moviesList: ListConfig<MovieEntity> = {
  entity: Entity.Movies,
  titleOf: (movie) => movie.title ?? '',
  summary: (movie) =>
    dotted([movie.format, movie.runtime ? `${movie.runtime} min` : null, movie.isTvSeries ? 'TV Show' : null]),
  columns: [
    { label: 'Format', field: 'format', class: 'hidden md:table-cell', cell: (movie) => movie.format },
    { label: 'Runtime', field: 'runtime', class: 'hidden lg:table-cell', cell: (movie) => movie.runtime ?? 0 },
    {
      label: 'TV Show',
      field: 'isTvSeries',
      class: 'hidden xl:table-cell',
      cell: (movie) => movie.isTvSeries,
      check: true,
    },
  ],
};

export const gamesList: ListConfig<GameEntity> = {
  entity: Entity.Games,
  titleOf: (game) => game.title ?? '',
  summary: (game) => dotted([game.platform, game.format]),
  columns: [
    { label: 'Platform', field: 'platform', class: 'hidden md:table-cell', cell: (game) => game.platform },
    { label: 'Format', field: 'format', class: 'hidden lg:table-cell', cell: (game) => game.format },
    {
      label: 'Developers',
      field: 'developers',
      class: 'hidden xl:table-cell',
      cell: (game) => join(game.developers),
    },
  ],
};

export const musicsList: ListConfig<MusicEntity> = {
  entity: Entity.Musics,
  titleOf: (music) => music.title ?? '',
  summary: (music) => dotted([music.artist, music.format]),
  columns: [
    { label: 'Artist', field: 'artist', class: 'hidden md:table-cell', cell: (music) => music.artist },
    { label: 'Format', field: 'format', class: 'hidden lg:table-cell', cell: (music) => music.format },
    { label: 'Tracks', field: 'tracks', class: 'hidden xl:table-cell', cell: (music) => music.tracks },
  ],
};
