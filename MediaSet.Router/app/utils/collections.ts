import { Entity, type BookEntity, type MovieEntity, type GameEntity, type MusicEntity } from '~/models';

export type CollectionEntity = BookEntity & MovieEntity & GameEntity & MusicEntity;
export interface CollectionField {
  key: keyof CollectionEntity;
  label: string;
  className?: string;
}
export interface CollectionContract {
  type: Entity;
  columns: CollectionField[];
  fields: CollectionField[];
  summary: (entity: CollectionEntity) => string;
}

function fields(entries: [keyof CollectionEntity, string][]): CollectionField[] {
  return entries.map(([key, label]) => ({ key, label }));
}
const title: CollectionField = { key: 'title', label: 'Title' };
const format: CollectionField = { key: 'format', label: 'Format' };
export const collections: Record<string, CollectionContract> = {
  books: {
    type: Entity.Books,
    columns: [
      title,
      { key: 'authors', label: 'Authors', className: 'hidden xs:table-cell' },
      { ...format, className: 'hidden md:table-cell' },
      { key: 'pages', label: 'Pages', className: 'hidden lg:table-cell' },
    ],
    fields: fields([
      ['format', 'Format'],
      ['pages', 'Pages'],
      ['publicationDate', 'Publication Date'],
      ['authors', 'Authors'],
      ['genres', 'Genres'],
      ['publisher', 'Publisher'],
      ['isbn', 'ISBN'],
      ['plot', 'Plot'],
    ]),
    summary: (entity) =>
      [entity.authors?.map((author) => author.trimEnd()).join(', '), entity.format].filter(Boolean).join(' · '),
  },
  movies: {
    type: Entity.Movies,
    columns: [
      title,
      { ...format, className: 'hidden md:table-cell' },
      { key: 'runtime', label: 'Runtime', className: 'hidden lg:table-cell' },
      { key: 'isTvSeries', label: 'TV Show', className: 'hidden xl:table-cell' },
    ],
    fields: fields([
      ['format', 'Format'],
      ['runtime', 'Runtime'],
      ['releaseDate', 'Release Date'],
      ['isTvSeries', 'Is TV Show'],
      ['genres', 'Genres'],
      ['studios', 'Studios'],
      ['barcode', 'Barcode'],
      ['plot', 'Plot'],
    ]),
    summary: (entity) => [entity.format, entity.runtime ? `${entity.runtime} min` : ''].filter(Boolean).join(' · '),
  },
  games: {
    type: Entity.Games,
    columns: [
      title,
      { key: 'platform', label: 'Platform', className: 'hidden md:table-cell' },
      { ...format, className: 'hidden lg:table-cell' },
      { key: 'developers', label: 'Developers', className: 'hidden xl:table-cell' },
    ],
    fields: fields([
      ['platform', 'Platform'],
      ['format', 'Format'],
      ['releaseDate', 'Release Date'],
      ['rating', 'Rating'],
      ['developers', 'Developers'],
      ['publishers', 'Publishers'],
      ['genres', 'Genres'],
      ['barcode', 'Barcode'],
      ['description', 'Description'],
    ]),
    summary: (entity) => [entity.platform, entity.format].filter(Boolean).join(' · '),
  },
  musics: {
    type: Entity.Musics,
    columns: [
      title,
      { key: 'artist', label: 'Artist', className: 'hidden md:table-cell' },
      { ...format, className: 'hidden lg:table-cell' },
      { key: 'tracks', label: 'Tracks', className: 'hidden xl:table-cell' },
    ],
    fields: fields([
      ['artist', 'Artist'],
      ['format', 'Format'],
      ['releaseDate', 'Release Date'],
      ['genres', 'Genres'],
      ['duration', 'Duration'],
      ['label', 'Label'],
      ['barcode', 'Barcode'],
      ['tracks', 'Tracks'],
      ['discs', 'Discs'],
    ]),
    summary: (entity) => [entity.artist, entity.format].filter(Boolean).join(' · '),
  },
};

export function getCollection(entity: string | undefined): CollectionContract {
  if (!entity || !Object.hasOwn(collections, entity)) throw new Response('Unknown collection', { status: 404 });
  return collections[entity];
}

export function collectionQuery(url: URL, collection: CollectionContract) {
  const searchText = url.searchParams.get('searchText') ?? '';
  const requestedOrder = url.searchParams.get('orderBy') ?? 'title:asc';
  const [field, direction] = requestedOrder.split(':');
  const orderBy =
    collection.columns.some((column) => column.key === field) &&
    /^(asc|desc)$/.test(direction) &&
    requestedOrder.split(':').length === 2
      ? requestedOrder
      : 'title:asc';
  const requestedPage = Number(url.searchParams.get('page') ?? 1);
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  return { searchText, orderBy, page };
}

export function duration(milliseconds: number | null | undefined): string {
  if (!milliseconds || milliseconds <= 0) return '';
  const seconds = Math.floor(milliseconds / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

export function fieldValue(entity: CollectionEntity, key: keyof CollectionEntity): string {
  const value = entity[key];
  if (key === 'duration') return duration(entity.duration);
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (Array.isArray(value)) return value.map((item) => (typeof item === 'string' ? item.trimEnd() : '')).join(', ');
  return value == null || typeof value === 'object' ? '' : String(value);
}
