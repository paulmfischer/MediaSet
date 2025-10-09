type Override<Type, NewType extends { [key in keyof Type]?: NewType[key] }> = Omit<Type, keyof NewType> & NewType;

export type FormProps = {
  isSubmitting?: boolean;
};

export enum Entity {
  Books = "Books",
  Movies = "Movies",
};

export interface BaseEntity {
  type: Entity;
  id?: string;
  title?: string;
  format?: string;
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
export type Book = Override<BookEntity, { authors: string; genres: string; }>

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
export type Movie = Override<MovieEntity, { studios: string; genres: string; }>