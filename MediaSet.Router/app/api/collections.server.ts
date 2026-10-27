import { privateApiUrl } from '~/config.server';
import type { CollectionEntity } from '~/utils/collections';
import type { Entity } from '~/models';

export interface CollectionPage {
  items: CollectionEntity[];
  page: number;
  totalPages: number;
  totalCount: number;
}

async function readApi<T>(path: string, signal: AbortSignal): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${privateApiUrl().replace(/\/$/, '')}/${path}`, { signal });
  } catch (error) {
    if (signal.aborted) throw error;
    throw new Response('The collection service is unavailable. Please try again.', { status: 503 });
  }
  if (response.status === 404) throw new Response('Media not found.', { status: 404 });
  if (!response.ok) throw new Response('Unable to load the collection. Please try again.', { status: 502 });
  try {
    return (await response.json()) as T;
  } catch {
    throw new Response('The collection service returned an invalid response.', { status: 502 });
  }
}

export async function readCollection(
  type: Entity,
  query: { searchText: string; orderBy: string; page: number },
  signal: AbortSignal
): Promise<CollectionPage> {
  const params = new URLSearchParams({
    searchText: query.searchText,
    orderBy: query.orderBy,
    page: String(query.page),
    pageSize: '75',
  });
  return readApi<CollectionPage>(`${type}/pagedsearch?${params}`, signal);
}

export async function readEntity(type: Entity, id: string, signal: AbortSignal): Promise<CollectionEntity> {
  if (!/^[a-fA-F0-9]{24}$/.test(id)) throw new Response('Media not found.', { status: 404 });
  return readApi<CollectionEntity>(`${type}/${encodeURIComponent(id)}`, signal);
}
