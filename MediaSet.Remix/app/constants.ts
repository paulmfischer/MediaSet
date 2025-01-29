// export const baseUrl = 'http://localhost:7130';
export const baseUrl = process.env.apiUrl;

export const entities = {
  books: "Books",
  movies: "Movies",
};

export enum Entities {
  Books = "Books",
  Movies = "Movies",
};

export interface Entity {
  id?: string;
  title: string;
  iSBN: string;
  format: string;
}

export interface Book extends Entity {
  pages?: number;
  publicationDate: string;
  authors: Array<string>;
  publisher: string;
  genres: Array<string>;
  plot: string;
  subtitle: string;
}

export interface Movie extends Entity {
  releaseDate: string;
  rating: string;
  runtime?: number;
  studios: Array<string>;
  genres: Array<string>;
  plot: string;
}