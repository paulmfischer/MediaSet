import { baseUrl } from "./constants.server";
import { singular } from "./helpers";
import { BaseEntity, Entity } from "./models";
import { serverLogger } from "./utils/serverLogger";
import { apiFetch } from "./utils/apiFetch.server";

function getEntityType<T extends BaseEntity>(entity: T): Entity {
  return entity.type;
}

export async function searchEntities<TEntity extends BaseEntity>(entityType: Entity, searchText: string | null, orderBy: string = ''): Promise<Array<TEntity>> {
  serverLogger.info("Fetching entities", { operation: "searchEntities", entityType, searchText, orderBy });
  try {
    const response = await apiFetch(`${baseUrl}/${entityType}/search?searchText=${searchText ?? ''}&orderBy=${orderBy}`);
    if (!response.ok) {
      serverLogger.error("Failed to fetch entities", { operation: "searchEntities", entityType, status: response.status });
      throw new Response('Error fetching data', { status: 500 });
    }
    const entities = await response.json() as TEntity[];
    serverLogger.info("Successfully fetched entities", { operation: "searchEntities", entityType, count: entities.length });
    return entities;
  } catch (error) {
    serverLogger.error("Error searching entities", { operation: "searchEntities", entityType, error: String(error) });
    throw error;
  }
}

export async function getEntity<TEntity extends BaseEntity>(entityType: Entity, id: string): Promise<TEntity> {
  serverLogger.info("Fetching entity details", { operation: "getEntity", entityType, id });
  try {
    const response = await apiFetch(`${baseUrl}/${entityType}/${id}`);
    if (response.status === 404) {
      serverLogger.warn("Entity not found", { operation: "getEntity", entityType, id, status: 404 });
      throw new Response(`${singular(entityType)} not found.`, { status: 404 });
    }
    if (!response.ok) {
      serverLogger.error("Failed to fetch entity", { operation: "getEntity", entityType, id, status: response.status });
      throw new Response(`Error fetching ${singular(entityType)}`, { status: response.status });
    }
    const entity = await response.json() as TEntity;
    serverLogger.info("Successfully fetched entity", { operation: "getEntity", entityType, id });
    return entity;
  } catch (error) {
    serverLogger.error("Error fetching entity", { operation: "getEntity", entityType, id, error: String(error) });
    throw error;
  }
}

export async function updateEntity<TEntity extends BaseEntity>(id: string, entity: TEntity, formData?: FormData): Promise<void> {
  const entityType = getEntityType(entity);
  serverLogger.info("Updating entity", { operation: "updateEntity", entityType, id, hasFormData: !!formData });
  try {
    let response: Response;
    if (formData) {
      // Send as multipart/form-data when FormData is provided
      response = await apiFetch(`${baseUrl}/${entityType}/${id}`, {
        method: 'PUT',
        body: formData,
      });
    } else {
      // Send as JSON when no FormData is provided
      response = await apiFetch(`${baseUrl}/${entityType}/${id}`, {
        method: 'PUT',
        body: JSON.stringify(entity),
        headers: { 'Content-Type': 'application/json' }
      });
    }

    if (!response.ok) {
      serverLogger.error("Failed to update entity", { operation: "updateEntity", entityType, id, status: response.status });
      throw new Response(`Error updating a ${singular(entityType)}`, { status: 500 });
    }
    serverLogger.info("Successfully updated entity", { operation: "updateEntity", entityType, id });
  } catch (error) {
    serverLogger.error("Error updating entity", { operation: "updateEntity", entityType, id, error: String(error) });
    throw error;
  }
}

export async function addEntity<TEntity extends BaseEntity>(entity: TEntity, formData?: FormData): Promise<TEntity> {
  const entityType = getEntityType(entity);
  serverLogger.info("Creating new entity", { operation: "addEntity", entityType, hasFormData: !!formData });
  try {
    let response: Response;
    if (formData) {
      // Send as multipart/form-data when FormData is provided
      response = await apiFetch(`${baseUrl}/${entityType}`, {
        method: 'POST',
        body: formData,
      });
    } else {
      // Send as JSON when no FormData is provided
      response = await apiFetch(`${baseUrl}/${entityType}`, {
        method: 'POST',
        body: JSON.stringify(entity),
        headers: { 'Content-Type': 'application/json' }
      });
    }

    if (!response.ok) {
      serverLogger.error("Failed to create entity", { operation: "addEntity", entityType, status: response.status });
      throw new Response(`Error creating a new ${singular(entityType)}`, { status: 500 });
    }
    const newEntity = await response.json() as TEntity;
    serverLogger.info("Successfully created entity", { operation: "addEntity", entityType, entityId: (newEntity as any).id });
    return newEntity;
  } catch (error) {
    serverLogger.error("Error creating entity", { operation: "addEntity", entityType, error: String(error) });
    throw error;
  }
}

export async function deleteEntity(entity: Entity, id: string): Promise<void> {
  serverLogger.info("Deleting entity", { operation: "deleteEntity", entityType: entity, id });
  try {
    const response = await apiFetch(`${baseUrl}/${entity}/${id}`, { method: 'DELETE' });

    if (!response.ok) {
      serverLogger.error("Failed to delete entity", { operation: "deleteEntity", entityType: entity, id, status: response.status });
      throw new Response(`Error deleting ${singular(entity)} with id: ${id}`, { status: 500 });
    }
    serverLogger.info("Successfully deleted entity", { operation: "deleteEntity", entityType: entity, id });
  } catch (error) {
    serverLogger.error("Error deleting entity", { operation: "deleteEntity", entityType: entity, id, error: String(error) });
    throw error;
  }
}