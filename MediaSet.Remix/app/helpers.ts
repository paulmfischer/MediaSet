import { Params } from "@remix-run/react";
import { Book, BookEntity, Entity } from "./models";

export function toTitleCase(str: string | undefined) {
  if (str == undefined) {
    return '';
  }

  return str.replace(/\w\S*/g, (text: string) => text.charAt(0).toUpperCase() + text.substring(1).toLowerCase());
}

export function getEntityFromParams(params: Params<string>) {
  return Entity[toTitleCase(params.entity) as keyof typeof Entity];
}

export function singular(entityName: Entity) {
  return entityName.slice(0, entityName.length - 1);
}

function formDataToObject<T>(formData: FormData): T {
  const data: Record<string, FormDataEntryValue> = {};
  for (let [key, value] of formData.entries()) {
    data[key] = value;
  }
  return data as T;
}

export function bookFormToData(formData: FormData): BookEntity {
  const book = formDataToObject<Book>(formData);
  const bookEntity: BookEntity = {
    authors: book.authors ? book.authors.split(',') : undefined,
    format: book.format,
    genres: book.genres ? book.genres.split(',') : undefined,
    id: book.id,
    isbn: book.isbn,
    pages: book.pages,
    plot: book.plot,
    publicationDate: book.publicationDate,
    publisher: book.publisher,
    subtitle: book.subtitle,
    title: book.title,
  };
  
  return bookEntity;
}