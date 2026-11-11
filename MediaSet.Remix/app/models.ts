type Override<Type, NewType extends { [key in keyof Type]?: NewType[key] }> = Omit<Type, keyof NewType> & NewType;

export type FormProps = {
  isSubmitting?: boolean;
};

export type Option = {
  label: string;
  value: string;
  isNew?: boolean | undefined;
};

export type ImageData = {
  fileName: string;
  contentType: string;
  fileSize: number;
  filePath: string;
  createdAt: string;
  updatedAt: string;
};

export type ImageLookupData = {
  lookupAttemptedAt: string;
  failureReason?: string;
  permanentFailure: boolean;
};

export enum Entity {
  Books = 'Books',
  Movies = 'Movies',
  Games = 'Games',
  Musics = 'Musics',
}

export interface BaseEntity {
  type: Entity;
  id?: string;
  title?: string;
  format?: string;
  coverImage?: ImageData;
  imageUrl?: string;
  imageLookup?: ImageLookupData;
}

// Backend model
export interface BookEntity extends BaseEntity {
  type: Entity.Books;
  isbn?: string;
  pages?: number | null;
  publicationDate?: string;
  authors?: Array<string>;
  publisher?: string;
  genres?: Array<string>;
  plot?: string;
  subtitle?: string;
  format?: string;
}
// UI model
export type Book = Override<BookEntity, { authors: string; genres: string }>;

// Backend model
export interface MovieEntity extends BaseEntity {
  type: Entity.Movies;
  barcode?: string;
  releaseDate?: string;
  rating?: string;
  runtime?: number | null;
  studios?: Array<string>;
  genres?: Array<string>;
  plot?: string;
  isTvSeries?: boolean;
  /** `source:value`, e.g. `tmdbid:1234`. Carried through saves; not shown or edited in the UI. */
  externalId?: string;
  /** Stored Plex data, present only for a linked movie when Plex is enabled; the API never sends the file path. */
  plex?: PlexItem;
}
// UI model
export type Movie = Override<MovieEntity, { studios: string; genres: string }>;

// Backend model
export interface GameEntity extends BaseEntity {
  type: Entity.Games;
  barcode?: string;
  releaseDate?: string;
  rating?: string;
  platform?: string;
  developers?: Array<string>;
  publishers?: Array<string>;
  genres?: Array<string>;
  description?: string;
}
// UI model
export type Game = Override<GameEntity, { developers: string; publishers: string; genres: string }>;

export interface Disc {
  trackNumber: number;
  title: string;
  duration: number | null;
}

// Backend model
export interface MusicEntity extends BaseEntity {
  type: Entity.Musics;
  artist?: string;
  releaseDate?: string;
  genres?: Array<string>;
  duration?: number;
  label?: string;
  barcode?: string;
  tracks?: number | null;
  discs?: number | null;
  discList?: Array<Disc>;
}
// UI model
export type Music = Override<MusicEntity, { genres: string }>;

export type IdentifierType = 'isbn' | 'lccn' | 'oclc' | 'olid' | 'upc' | 'ean' | 'entity';

export interface BookLookupResponse {
  title: string;
  subtitle: string;
  authors: Array<{ name: string; url: string }>;
  numberOfPages: number;
  publishers: Array<{ name: string }>;
  publishDate: string;
  subjects: Array<{ name: string; url: string }>;
  format?: string;
  imageUrl?: string;
}

export interface MovieLookupResponse {
  title: string;
  genres: string[];
  studios: string[];
  releaseDate: string;
  rating: string;
  runtime: number | null;
  plot: string;
  format?: string;
  imageUrl?: string;
  externalId?: string;
}

export interface GameLookupResponse {
  title: string;
  platform: string;
  genres: string[];
  developers: string[];
  publishers: string[];
  releaseDate: string;
  rating: string;
  description: string;
  format?: string;
  imageUrl?: string;
}

export interface MusicLookupResponse {
  title: string;
  artist: string;
  releaseDate: string;
  genres: string[];
  duration: number | null;
  label: string;
  tracks: number | null;
  discs: number | null;
  discList: Disc[];
  format?: string;
  imageUrl?: string;
}

export interface LookupError {
  message: string;
  statusCode: number;
}

export type LookupResult<T> = T | LookupError;

export type PlexAudioTrack = { codec?: string | null; channels?: number | null; language?: string | null };

export type PlexRollup = {
  episodesPresent: number;
  episodesExpected: number;
  watchedCount: number;
  watched: boolean;
  lastWatchedAt?: string | null;
  resolution?: string | null;
  resolutionMixed: boolean;
  hdr: boolean;
  audioLanguages: string[];
  audioLanguagesDiffer: boolean;
  subtitleLanguages: string[];
  subtitleLanguagesDiffer: boolean;
};

export type PlexScope = { kind: string; from?: number | null; to?: number | null };

/** The Plex data the movie lookup returns (the API's `PlexItemDataDto` without its file path). */
export type PlexItem = {
  movieId: string;
  plexItemId: string;
  onPlex: boolean;
  /** `auto` (set by a sync) or `manual` (linked by hand). */
  linkSource: string;
  broken: boolean;
  library?: string | null;
  resolution?: string | null;
  hdr: boolean;
  videoCodec?: string | null;
  bestAudioTrack?: PlexAudioTrack | null;
  subtitleLanguages: string[];
  watched: boolean;
  lastWatchedAt?: string | null;
  viewCount: number;
  dateAdded?: string | null;
  deepLink?: string | null;
  durationSeconds?: number | null;
  container?: string | null;
  sizeBytes?: number | null;
  lastSyncedAt: string;
  scope?: PlexScope | null;
  rollup?: PlexRollup | null;
};
