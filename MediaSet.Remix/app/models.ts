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

export enum Entity {
  Books = "Books",
  Movies = "Movies",
  Games = "Games",
  Musics = "Musics",
}

export interface BaseEntity {
  type: Entity;
  id?: string;
  title?: string;
  format?: string;
  coverImage?: ImageData;
  imageUrl?: string;
}

// Backend model
export interface BookEntity extends BaseEntity {
  isbn?: string;
  pages?: number;
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
  barcode?: string;
  releaseDate?: string;
  rating?: string;
  runtime?: number;
  studios?: Array<string>;
  genres?: Array<string>;
  plot?: string;
  isTvSeries?: boolean;
}
// UI model
export type Movie = Override<MovieEntity, { studios: string; genres: string }>;

// Backend model
export interface GameEntity extends BaseEntity {
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
  artist?: string;
  releaseDate?: string;
  genres?: Array<string>;
  duration?: number;
  label?: string;
  barcode?: string;
  tracks?: number;
  discs?: number;
  discList?: Array<Disc>;
}
// UI model
export type Music = Override<MusicEntity, { genres: string }>;

export type IdentifierType = "isbn" | "lccn" | "oclc" | "olid" | "upc" | "ean";

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
