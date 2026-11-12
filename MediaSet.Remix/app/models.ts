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

/** One title-search match from Plex: a movie or a show. */
export type PlexSearchResult = {
  title: string;
  plexItemId: string;
  year?: number | null;
  library: string;
  /** `movie` or `show`. */
  type: string;
};

export type PlexSeason = {
  index: number;
  plexSeasonId: string;
  episodeCount: number;
  dateAdded?: string | null;
  deepLink?: string | null;
  rollup?: PlexRollup | null;
};

export type PlexEpisode = {
  seasonNumber: number;
  episodeNumber: number;
  title: string;
  plexEpisodeId: string;
  watched: boolean;
  lastWatchedAt?: string | null;
  resolution?: string | null;
  hdr: boolean;
  sizeBytes?: number | null;
  /** False when Plex no longer returns the episode (its data is the last synced). */
  onPlex: boolean;
};

/** The small Plex summary the paged movie search attaches to a linked movie (no file path); absent when unlinked. */
export type PlexListSummary = Pick<PlexItem, 'onPlex' | 'broken' | 'watched'> & { resolution?: string | null };

/** The movie list's `plex` filter values. */
export const PLEX_FILTERS = ['onPlex', 'notOnPlex', 'unwatched', 'broken'] as const;
export type PlexFilter = (typeof PLEX_FILTERS)[number];

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
  /** TV entries only: the covered seasons and every stored episode. */
  seasons?: PlexSeason[] | null;
  episodes?: PlexEpisode[] | null;
};

/** One Plex sync run, as the status and sync endpoints return it. */
export type PlexSyncRun = {
  id?: string | null;
  entityType: string;
  trigger: string;
  /** `queued`, `running`, `succeeded`, `failed` or `interrupted`. */
  state: string;
  startedAt: string;
  endedAt?: string | null;
  itemsScanned?: number | null;
  matchedCount?: number | null;
  unmatchedCount?: number | null;
  erroredCount?: number | null;
  externalIdsBackfilled?: number | null;
  backfillSkipped?: number | null;
  message?: string | null;
};

/** Sync status of one entity type (`GET /plex/status`). */
export type PlexEntityStatus = {
  entityType: string;
  enabled: boolean;
  lastRun?: PlexSyncRun | null;
  nextRun?: string | null;
  schedulePolicy: string;
};

/** One labelled count of a Plex statistics breakdown, in display order. */
export type PlexCount = { name: string; count: number };

/** One kind of entry (`movies` or `tv`) in `GET /plex/statistics`. The API sends no file path. */
export type PlexKindStatistics = {
  kind: string;
  coverage: { total: number; onPlex: number; notOnPlex: number; broken: number };
  /** On-Plex movies, or on-Plex episodes for TV; what the breakdowns count. */
  units: number;
  resolution: PlexCount[];
  videoCodec: PlexCount[];
  hdr: PlexCount[];
  subtitles: PlexCount[];
  watched: PlexCount[];
  audioCodec: PlexCount[];
  audioChannels: PlexCount[];
  storage: { totalBytes: number; averageBytes: number; unitsWithSize: number };
  runtime: { totalSeconds: number; averageSeconds: number; unitsWithRuntime: number };
  mostWatched: Array<{ id: string; title: string; viewCount: number }>;
  /** TV only. */
  completeness?: {
    episodesPresent: number;
    episodesExpected: number;
    entriesComplete: number;
    entriesIncomplete: number;
  } | null;
};

export type PlexStatistics = { movies: PlexKindStatistics; tv: PlexKindStatistics };
