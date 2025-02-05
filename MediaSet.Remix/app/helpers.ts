import { Params } from "@remix-run/react";
import { BaseEntity, Book, BookEntity, Entity, Movie, MovieEntity } from "./models";

export function toTitleCase(str: string | undefined) {
  if (str == undefined) {
    return '';
  }

  return str.replace(/\w\S*/g, (text: string) => text.charAt(0).toUpperCase() + text.substring(1).toLowerCase());
}

export function getEntityFromParams(params: Params<string>) {
  return Entity[toTitleCase(params.entity) as keyof typeof Entity];
}

export function singular(entityType: Entity) {
  return entityType.slice(0, entityType.length - 1);
}

function formDataToType<T>(formData: FormData): T {
  const data: Record<string, FormDataEntryValue> = {};
  for (let [key, value] of formData) {
    data[key] = value;
  }
  return data as T;
}

function getValue(val: any): any | undefined {
  return val == '' ? undefined : val;
}

function baseToBookEntity(data: BaseEntity): BookEntity {
  const book = data as Book;
  return {
    type: book.type,
    authors: book.authors ? book.authors.split(',') : undefined,
    format: getValue(book.format),
    genres: book.genres ? book.genres.split(',') : undefined,
    id: getValue(book.id),
    isbn: getValue(book.isbn),
    pages: getValue(book.pages),
    plot: getValue(book.plot),
    publicationDate: getValue(book.publicationDate),
    publisher: getValue(book.publisher),
    subtitle: getValue(book.subtitle),
    title: getValue(book.title),
  };
}

function baseToMovieEntity(data: BaseEntity): MovieEntity {
  const movie = data as Movie;
  return {
    type: movie.type,
    studios: movie.studios ? movie.studios.split(',') : undefined,
    format: getValue(movie.format),
    genres: movie.genres ? movie.genres.split(',') : undefined,
    id: getValue(movie.id),
    barcode: getValue(movie.barcode),
    releaseDate: getValue(movie.releaseDate),
    plot: getValue(movie.plot),
    runtime: getValue(movie.runtime),
    title: getValue(movie.title),
  };
}

export function formToDto(formData: FormData): BaseEntity | null {
  const data = formDataToType<BaseEntity>(formData);
  if (data.type === Entity.Books) {
    return baseToBookEntity(data);
  } else if (data.type === Entity.Movies) {
    return baseToMovieEntity(data);
  } else {
    return null;
  }
}