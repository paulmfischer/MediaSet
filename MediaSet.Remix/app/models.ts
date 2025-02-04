type Override<Type, NewType extends { [key in keyof Type]?: NewType[key] }> = Omit<Type, keyof NewType> & NewType;

export type FormProps = {
  isSubmitting?: boolean;
};

export const entities = {
  books: "Books",
  movies: "Movies",
};

export enum Entity {
  Books = "Books",
  Movies = "Movies",
};

export interface BaseEntity {
  id?: string;
  title?: string;
  isbn?: string;
  format?: string;
}

export interface BookEntity extends BaseEntity {
  pages?: number;
  publicationDate?: string;
  authors?: Array<string>;
  publisher?: string;
  genres?: Array<string>;
  plot?: string;
  subtitle?: string;
}
export type Book = Override<BookEntity, { authors: string; genres: string; }>

export interface MovieEntity extends BaseEntity {
  releaseDate?: string;
  rating?: string;
  runtime?: number;
  studios?: Array<string>;
  genres?: Array<string>;
  plot?: string;
}
export type Movie = Override<MovieEntity, { studios: string; genres: string; }>