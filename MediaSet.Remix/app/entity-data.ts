import { baseUrl } from "./constants.server";
import { singular } from "./helpers";
import { BaseEntity, BookEntity, Entity, MovieEntity } from "./models";

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

function getEntityType<T>(entity: T): Entity {
  if (isOfType(Type.isBook, entity)) {
    return Entity.Books;
  }
  if (isOfType(Type.isMovie, entity)) {
    return Entity.Movies;
  }

  throw "No matching entity name";
}

export async function searchEntities<TEntity extends BaseEntity>(entityType: Entity, searchText: string | null, orderBy: string = ''): Promise<Array<TEntity>> {
  const response = await fetch(`${baseUrl}/${entityType}/search?searchText=${searchText ?? ''}&orderBy=${orderBy}`);
  if (!response.ok) {
    throw new Response('Error fetching data', { status: 500 });
  }
  return await response.json() as TEntity[];
}

export async function getEntity<TEntity extends BaseEntity>(entityType: Entity, id: string): Promise<TEntity> {
  const response = await fetch(`${baseUrl}/${entityType}/${id}`);
  if (response.status == 404) {
    throw new Response(`${singular(entityType)} not found.`, { status: 404 });
  }

  return await response.json() as TEntity;
}

export async function updateEntity<TEntity extends BaseEntity>(id: string, entity: TEntity) {
  const entityType = getEntityType(entity);
  const response = await fetch(`${baseUrl}/${entityType}/${id}`, {
    method: 'PUT',
    body: JSON.stringify(entity),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error updating a ${singular(entityType)}`, { status: 500 });
  }
}

export async function addEntity<TEntity extends BaseEntity>(entity: TEntity) {
  const entityType = getEntityType(entity);
  const response = await fetch(`${baseUrl}/${entityType}`, {
    method: 'POST',
    body: JSON.stringify(entity),
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Response(`Error creating a new ${singular(entityType)}`, { status: 500 });
  }

  return await response.json() as TEntity;
}

export async function deleteEntity(entity: Entity, id: string) {
  const response = await fetch(`${baseUrl}/${entity}/${id}`, { method: 'DELETE' });

  if (!response.ok) {
    throw new Response(`Error deleting ${singular(entity)} with id: ${id}`, { status: 500 });
  }
}