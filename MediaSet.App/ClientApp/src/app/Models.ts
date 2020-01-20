export interface Media {
  id: number;
  title: string;
  formatId: number;
  format: Format;
  mediaTypeId: number;
  mediaGenres: Array<MediaGenre>;
}

export interface Format {

}

export interface MediaGenre {

}

export interface Book {
  id: number;
  plot: string;
  ISBN: string;
  subTitle: string;
  numberOfPages: string;
  publicationDate: string;
  dewey: string;
  media: Media;
  bookAuthors: Author;
  bookPublishers: Publisher;
}

export interface Author {
  id: number;
  name: string;
}

export interface Game {
  id: number;
  mediaId: number;
  media: Media;
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

export interface Platform {

}

export interface Publisher {

}

export interface Developer {

}

export interface PagedResult<T> {
  items: Array<T>;
  total: number;
}
