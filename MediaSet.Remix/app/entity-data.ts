import { baseUrl, Entities } from "./constants";
import { BookEntity, MovieEntity } from "./models";

namespace Type {
  export function isBook(entity: any): entity is BookEntity {
    return entity && "authors" in entity;
  }
  export function isMovie(entity: any): entity is MovieEntity {
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

export async function searchEntities<TEntity>(entityType: Entities, searchText: string | null, orderBy: string = ''): Promise<Array<TEntity>> {
  const response = await fetch(`${baseUrl}/${entityType}/search?searchText=${searchText ?? ''}&orderBy=${orderBy}`);
  if (!response.ok) {
    throw new Response('Error fetching data', { status: 500 });
  }
  return await response.json() as TEntity[];
}

export async function getEntity<TEntity>(entityType: Entities, id: string): Promise<TEntity> {
  const response = await fetch(`${baseUrl}/${entityType}/${id}`);
  if (response.status == 404) {
    throw new Response("Book not found", { status: 404 });
  }

  return await response.json() as TEntity;
}

export async function updateEntity<TEntity>(id: string, entity: TEntity) {
  const entityName = getEntityName(entity);
  const response = await fetch(`${baseUrl}/${entityName}/${id}`, {
    method: 'PUT',
    body: JSON.stringify(entity),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error updating a ${entityName}`, { status: 500 });
  }
}

export async function addEntity<TEntity>(entity: TEntity) {
  const entityName = getEntityName(entity);
  const response = await fetch(`${baseUrl}/${entityName}`, {
    method: 'POST',
    body: JSON.stringify(entity),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error creating a new ${entityName}`, { status: 500 });
  }

  return await response.json() as TEntity;
}

export async function deleteEntity<TEntity>(entity: Entities, id: string) {
  const response = await fetch(`${baseUrl}/${entity}/${id}`, { method: 'DELETE' });

  if (!response.ok) {
    throw new Response(`Error deleting ${entity} with id: ${id}`, { status: 500 });
  }
}