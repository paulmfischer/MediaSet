type Override<Type, NewType extends { [key in keyof Type]?: NewType[key] }> = Omit<Type, keyof NewType> & NewType;

export interface Entity {
  id?: string;
  title?: string;
  isbn?: string;
  format?: string;
}

export interface BookEntity extends Entity {
  pages?: number;
  publicationDate?: string;
  authors?: Array<string>;
  publisher?: string;
  genres?: Array<string>;
  plot?: string;
  subtitle?: string;
}
export type Book = Override<BookEntity, { authors: string; genres: string; }>

export interface MovieEntity extends Entity {
  releaseDate?: string;
  rating?: string;
  runtime?: number;
  studios?: Array<string>;
  genres?: Array<string>;
  plot?: string;
}
export type Movie = Override<MovieEntity, { studios: string; genres: string; }>