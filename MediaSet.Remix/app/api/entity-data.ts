import { baseUrl } from '~/constants.server';
import { singular } from '~/utils/helpers';
import { BaseEntity, Entity } from '~/models';
import { serverLogger } from '~/utils/serverLogger';
import { apiFetch } from '~/utils/apiFetch.server';

export type PagedResult<TEntity> = {
  items: TEntity[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

function getEntityType<T extends BaseEntity>(entity: T): Entity {
  return entity.type;
}

export async function searchEntities<TEntity extends BaseEntity>(
  entityType: Entity,
  searchText: string | null,
  orderBy: string = ''
): Promise<Array<TEntity>> {
  serverLogger.info(`Searching for ${entityType} with searchText: ${searchText}, orderBy: ${orderBy}`, {
    operation: 'searchEntities',
    entityType,
    searchText,
    orderBy,
  });
  try {
    const searchParams = new URLSearchParams();
    searchParams.set('searchText', searchText ?? '');
    searchParams.set('orderBy', orderBy);
    const response = await apiFetch(`${baseUrl}/${entityType}/search?${searchParams.toString()}`);
    if (!response.ok) {
      serverLogger.error(`Failed to fetch ${entityType} search results`, {
        operation: 'searchEntities',
        entityType,
        status: response.status,
      });
      throw new Response('Error fetching data', { status: 500 });
    }
    const entities = (await response.json()) as TEntity[];
    serverLogger.info(`Successfully fetched ${entities.length} ${entityType}`, {
      operation: 'searchEntities',
      entityType,
      count: entities.length,
    });
    return entities;
  } catch (error) {
    serverLogger.error('Error searching entities', { operation: 'searchEntities', entityType, error: String(error) });
    throw error;
  }
}

export async function pagedSearchEntities<TEntity extends BaseEntity>(
  entityType: Entity,
  searchText: string | null,
  orderBy: string = '',
  page: number = 1,
  pageSize: number = 75
): Promise<PagedResult<TEntity>> {
  serverLogger.info(
    `Paged searching for ${entityType} with searchText: ${searchText}, orderBy: ${orderBy}, page: ${page}`,
    {
      operation: 'pagedSearchEntities',
      entityType,
      searchText,
      orderBy,
      page,
      pageSize,
    }
  );
  try {
    const searchParams = new URLSearchParams();
    searchParams.set('searchText', searchText ?? '');
    searchParams.set('orderBy', orderBy);
    searchParams.set('page', String(page));
    searchParams.set('pageSize', String(pageSize));
    const response = await apiFetch(`${baseUrl}/${entityType}/pagedsearch?${searchParams.toString()}`);
    if (!response.ok) {
      serverLogger.error(`Failed to fetch ${entityType} paged search results`, {
        operation: 'pagedSearchEntities',
        entityType,
        status: response.status,
      });
      throw new Response('Error fetching data', { status: 500 });
    }
    const result = (await response.json()) as PagedResult<TEntity>;
    serverLogger.info(`Successfully fetched ${result.items.length}/${result.totalCount} ${entityType}`, {
      operation: 'pagedSearchEntities',
      entityType,
      count: result.items.length,
      totalCount: result.totalCount,
    });
    return result;
  } catch (error) {
    serverLogger.error('Error paged searching entities', {
      operation: 'pagedSearchEntities',
      entityType,
      error: String(error),
    });
    throw error;
  }
}

export async function getEntity<TEntity extends BaseEntity>(entityType: Entity, id: string): Promise<TEntity> {
  serverLogger.info(`Fetching ${singular(entityType)} with id ${id}`, { operation: 'getEntity', entityType, id });
  try {
    const response = await apiFetch(`${baseUrl}/${entityType}/${id}`);
    if (response.status === 404) {
      serverLogger.warn(`${singular(entityType)} with id ${id} not found`, {
        operation: 'getEntity',
        entityType,
        id,
        status: 404,
      });
      throw new Response(`${singular(entityType)} not found.`, { status: 404 });
    }
    if (!response.ok) {
      serverLogger.error(`Failed to fetch ${singular(entityType)} with id ${id}`, {
        operation: 'getEntity',
        entityType,
        id,
        status: response.status,
      });
      throw new Response(`Error fetching ${singular(entityType)}`, { status: response.status });
    }
    const entity = (await response.json()) as TEntity;
    serverLogger.info(`Successfully fetched ${singular(entityType)} with id ${id}`, {
      operation: 'getEntity',
      entityType,
      id,
    });
    return entity;
  } catch (error) {
    serverLogger.error('Error fetching entity', { operation: 'getEntity', entityType, id, error: String(error) });
    throw error;
  }
}

export async function updateEntity<TEntity extends BaseEntity>(
  id: string,
  entity: TEntity,
  formData?: FormData
): Promise<void> {
  const entityType = getEntityType(entity);
  serverLogger.info(`Updating ${singular(entityType)} with id ${id}`, {
    operation: 'updateEntity',
    entityType,
    id,
    hasFormData: !!formData,
  });
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
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (!response.ok) {
      serverLogger.error(`Failed to update ${singular(entityType)} with id ${id}`, {
        operation: 'updateEntity',
        entityType,
        id,
        status: response.status,
      });
      throw new Response(`Error updating a ${singular(entityType)}`, { status: 500 });
    }
    serverLogger.info(`Successfully updated ${singular(entityType)} with id ${id}`, {
      operation: 'updateEntity',
      entityType,
      id,
    });
  } catch (error) {
    serverLogger.error('Error updating entity', { operation: 'updateEntity', entityType, id, error: String(error) });
    throw error;
  }
}

export async function addEntity<TEntity extends BaseEntity>(entity: TEntity, formData?: FormData): Promise<TEntity> {
  const entityType = getEntityType(entity);
  serverLogger.info(`Creating new ${singular(entityType)}`, {
    operation: 'addEntity',
    entityType,
    hasFormData: !!formData,
  });
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
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (!response.ok) {
      serverLogger.error(`Failed to create new ${singular(entityType)}`, {
        operation: 'addEntity',
        entityType,
        status: response.status,
      });
      throw new Response(`Error creating a new ${singular(entityType)}`, { status: 500 });
    }
    const newEntity = (await response.json()) as TEntity;
    serverLogger.info(`Successfully created new ${singular(entityType)} with id ${(newEntity as { id?: string }).id}`, {
      operation: 'addEntity',
      entityType,
      entityId: (newEntity as { id?: string }).id,
    });
    return newEntity;
  } catch (error) {
    serverLogger.error('Error creating entity', { operation: 'addEntity', entityType, error: String(error) });
    throw error;
  }
}

export async function deleteEntity(entity: Entity, id: string): Promise<void> {
  serverLogger.info(`Deleting ${singular(entity)} with id ${id}`, {
    operation: 'deleteEntity',
    entityType: entity,
    id,
  });
  try {
    const response = await apiFetch(`${baseUrl}/${entity}/${id}`, { method: 'DELETE' });

    if (!response.ok) {
      serverLogger.error(`Failed to delete ${singular(entity)} with id ${id}`, {
        operation: 'deleteEntity',
        entityType: entity,
        id,
        status: response.status,
      });
      throw new Response(`Error deleting ${singular(entity)} with id: ${id}`, { status: 500 });
    }
    serverLogger.info(`Successfully deleted ${singular(entity)} with id ${id}`, {
      operation: 'deleteEntity',
      entityType: entity,
      id,
    });
  } catch (error) {
    serverLogger.error(`Error deleting ${singular(entity)} with id ${id}`, {
      operation: 'deleteEntity',
      entityType: entity,
      id,
      error: String(error),
    });
    throw error;
  }
}
