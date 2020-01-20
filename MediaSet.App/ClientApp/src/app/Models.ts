export interface Media {
  id: number;
  title: string;
  formatId: number;
  format: Format;
  mediaTypeId: number;
  mediaGenres: Array<MediaGenre>;
}

export interface BaseEntity {
  id: number;
  mediaId: number;
  media: Media;
}

export interface BaseChildEntity {
  id: number;
  name: string;
}

export interface Format extends BaseChildEntity { }

export interface MediaGenre {
  mediaId: number;
  media: Media;
  genreId: number;
  genre: Genre;
}

export interface Genre extends BaseChildEntity { }

export interface Book extends BaseEntity {
  plot: string;
  ISBN: string;
  subTitle: string;
  numberOfPages: string;
  publicationDate: string;
  dewey: string;
  bookAuthors: Array<BookAuthor>;
  bookPublishers: Publisher;
}

export interface BookAuthor {
  bookId: number;
  book: Book;
  authorId: number;
  author: Author;
}

export interface Author extends BaseChildEntity {}

export interface Game extends BaseEntity {
  barcode: string;
  platform: Platform;
  platformId: number;
  subTitle: string;
  releaseDate: string;
  publisher: Publisher;
  publisherId: number;
  developer: Developer;
  developerId: number;
}

export interface Developer extends BaseChildEntity { }

export interface Platform extends BaseChildEntity { }

export interface Publisher extends BaseChildEntity { }

export interface Movie extends BaseEntity {
  barcode: string;
  releaseDate: string;
  plot: string;
  imdbLink: string
  studioId: number;
  studio: Studio;
  movieDirectors: Array<MovieDirectors>;
  movieProducers: Array<MovieProducers>;
  movieWriters: Array<MovieWriters>;
}

export interface MovieDirectors {
  movieId: number;
  movie: Movie;
  directorId: number;
  director: Director;
}

export interface MovieProducers {
  movieId: number;
  movie: Movie;
  producerId: number;
  producer: Producer;
}

export interface MovieWriters {
  movieId: number;
  movie: Movie;
  writerId: number;
  writer: Writer;
}

export interface Director extends BaseChildEntity { }

export interface Producer extends BaseChildEntity { }

export interface Studio extends BaseChildEntity { }

export interface Writer extends BaseChildEntity { }

export interface PagedResult<T> {
  items: Array<T>;
  total: number;
}
