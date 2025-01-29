import { baseUrl, Book, Entities, Entity, Movie } from "./constants";

namespace Type {
  export function isBook(entity: any): entity is Book {
    return entity && "authors" in entity;
  }
  export function isMovie(entity: any): entity is Movie {
    return entity && "releaseDate" in entity;
  }
}

function isOfType<T>(guard: (entity: any) => entity is T, data: any): boolean {
  return guard(data);
}

function getEntityName<T>(entity: T): Entities {
  if (isOfType(Type.isBook, entity)) {
    return Entities.Books;
  }
  if (isOfType(Type.isMovie, entity)) {
    return Entities.Movies;
  }

  throw "No matching entity name";
}

export async function search<TEntity>(entityType: Entities, searchText: string | null, orderBy: string = ''): Promise<Array<TEntity>> {
  const response = await fetch(`${baseUrl}/${entityType}/search?searchText=${searchText ?? ''}&orderBy=${orderBy}`);
  if (!response.ok) {
    throw new Response('Error fetching data', { status: 500 });
  }
  return await response.json() as TEntity[];
}

export async function get<TEntity>(entityType: Entities, id: string): Promise<TEntity> {
  const response = await fetch(`${baseUrl}/${entityType}/${id}`);
  if (response.status == 404) {
    throw new Response("Book not found", { status: 404 });
  }

  return await response.json() as TEntity;
}

function convertFormToRecord<TEntity>(entity: TEntity): TEntity {
  return entity;
  /* const bookRecord = { ...book } as BookRecord;
  if (book.authors) {
    bookRecord.authors = book.authors.split(',');
  }
  if (book.genres) {
    bookRecord.genres = book.genres.split(',');
  }

  bookRecord.pages = book.pages === '' ? undefined : Number(book.pages);

  return bookRecord; */
}

export async function update<TEntity>(id: string, entity: TEntity) {
  const entityName = getEntityName(entity);
  const response = await fetch(`${baseUrl}/${entityName}/${id}`, {
    method: 'PUT',
    body: JSON.stringify(convertFormToRecord(entity)),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error updating a ${entityName}`, { status: 500 });
  }
}

export async function add<TEntity>(entity: TEntity) {
  const entityName = getEntityName(entity);
  const response = await fetch(`${baseUrl}/${entityName}`, {
    method: 'POST',
    body: JSON.stringify(convertFormToRecord(entity)),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error creating a new ${entityName}`, { status: 500 });
  }

  return await response.json() as TEntity;
}

export async function remove<TEntity>(entity: Entities, id: string) {
  const response = await fetch(`${baseUrl}/${entity}/${id}`, { method: 'DELETE' });

  if (!response.ok) {
    throw new Response(`Error deleting ${entity} with id: ${id}`, { status: 500 });
  }
}